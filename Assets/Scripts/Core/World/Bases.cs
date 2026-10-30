using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// A piece of furniture in a base's cell: which piece, which way it faces (quarter turns clockwise from
    /// the base's +j), and for a chest, what's in it.
    public sealed class Furnishing
    {
        public string Piece;
        public int Facing;
        public Bank Store;
    }

    /// A building: a set of cells on a grid of its own, placed anywhere and turned to any angle. Its
    /// foundations are floor; walls, doorways and windows go on the cells' edges; roofs over them; furniture in
    /// them. Cell (i, j)'s centre is at map-space (X, Z) moved i along the base's +i and j along its +j.
    public sealed class Structure
    {
        public int Id;                    // never reused: its cells' tiles (BaseSet.CellTile) are made from it
        public double X, Z;               // map-space position of cell (0, 0)'s centre
        public double Angle;              // degrees, clockwise seen from above (a Unity y rotation)
        public double Floor;              // the height of its floors
        public readonly Dictionary<long, string> Foundations = new Dictionary<long, string>();  // Key(i, j) -> piece
        public readonly Dictionary<long, string> Edges = new Dictionary<long, string>();        // EdgeKey -> piece
        public readonly Dictionary<long, string> Roofs = new Dictionary<long, string>();        // Key(i, j) -> piece
        public readonly Dictionary<long, Furnishing> Furniture = new Dictionary<long, Furnishing>();
        /// How far its furthest cell's corner is from (X, Z): a quick test for whether a point might be in it.
        public double Radius { get; private set; }

        public static long Key(int i, int j) => ChunkedGrid.Key(i, j);
        public static void Unkey(long k, out int i, out int j) { i = (int)(k >> 32); j = (int)(uint)k; }

        /// Each cell owns its north edge (side 0, towards j + 1) and its east edge (side 1, towards i + 1).
        public static long EdgeKey(int i, int j, int side) => ChunkedGrid.Key(i * 2 + side, j);
        public static void UnEdgeKey(long k, out int i, out int j, out int side)
        {
            int v = (int)(k >> 32);
            side = v & 1;
            i = (v - side) / 2;
            j = (int)(uint)k;
        }

        /// The edge between cell (i, j) and its neighbour (i + dx, j + dz) (one of dx, dz is 0, the other +-1).
        public static long EdgeBetween(int i, int j, int dx, int dz) =>
            dz == 1 ? EdgeKey(i, j, 0) : dz == -1 ? EdgeKey(i, j - 1, 0) : dx == 1 ? EdgeKey(i, j, 1) : EdgeKey(i - 1, j, 1);

        public bool HasFoundation(int i, int j) => Foundations.ContainsKey(Key(i, j));

        /// The piece on an edge, or null.
        public string EdgePiece(long edge) => Edges.TryGetValue(edge, out var p) ? p : null;

        /// Whether you can walk across the edge between (i, j) and its neighbour: nothing on it, or a doorway.
        public bool Open(int i, int j, int dx, int dz)
        {
            var piece = ContentDb.Current.Piece(EdgePiece(EdgeBetween(i, j, dx, dz)));
            return piece == null || piece.Kind == PieceKind.Doorway;
        }

        /// A point on the base's grid (cell units) in map space.
        public void ToMap(double i, double j, out double x, out double z)
        {
            double a = Angle * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            x = X + i * c + j * s;
            z = Z - i * s + j * c;
        }

        /// A map-space point on the base's grid (cell units: cell (i, j) is round about (i, j)).
        public void ToLocal(double x, double z, out double i, out double j)
        {
            double a = Angle * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a), dx = x - X, dz = z - Z;
            i = dx * c - dz * s;
            j = dx * s + dz * c;
        }

        /// Works out Radius again (after a foundation is added or removed).
        public void Measure()
        {
            double r = 0;
            foreach (var k in Foundations.Keys)
            {
                Unkey(k, out int i, out int j);
                r = Math.Max(r, Math.Sqrt((Math.Abs(i) + 0.5) * (Math.Abs(i) + 0.5) + (Math.Abs(j) + 0.5) * (Math.Abs(j) + 0.5)));
            }
            Radius = r;
        }
    }

    /// Every base in a world, and how walking works through them.
    ///
    /// A base's cells don't line up with the world's tiles (it can be anywhere, at any angle), so each cell has
    /// a tile of its own, far out where the world never reaches (CellTile): BaseSpace + the base's id * Span +
    /// the cell's i, and BaseSpace + its j. Everything that works with tiles (paths, standing next to things,
    /// items on the ground, stations) then works in a base too; World turns those tiles into positions.
    /// Besides its foundations a base has an apron: the cells just outside it, on the ground, which joins it
    /// to the world's tiles (Links) wherever its edge is open or has a doorway. The world's tiles under a base
    /// (its footprint) can't be walked on, so paths and monsters go round it or in through its doorways.
    public sealed class BaseSet
    {
        public const int BaseSpace = 1 << 29, Span = 1024, Half = Span / 2;
        public const double Margin = 0.2;        // the footprint: world tiles centred within this of a foundation
        public const double MaxStep = 0.8;       // the biggest step up or down between the ground and a floor
        public const double MaxSupport = 2.5;    // the tallest posts under a foundation
        public const double FloorAbove = 0.12;   // a new base's floor, above the highest ground under it
        public const int MaxCells = Half - 8;    // how far from cell (0, 0) a base can spread

        readonly Dictionary<int, Structure> byId = new Dictionary<int, Structure>();
        public readonly List<Structure> All = new List<Structure>();
        readonly Func<Tile, bool> walkable, buildable;
        readonly Func<double, double, double> ground;
        readonly HashSet<Tile> footprint = new HashSet<Tile>();
        readonly Dictionary<Tile, List<Tile>> links = new Dictionary<Tile, List<Tile>>();
        static readonly List<Tile> NoLinks = new List<Tile>();
        int nextId = 1;

        /// `walkable`: a world tile you could walk on, bases aside. `buildable`: one a foundation may cover
        /// (open ground outside towns). `ground`: the ground's height at a map-space point.
        public BaseSet(Func<Tile, bool> walkable, Func<Tile, bool> buildable, Func<double, double, double> ground)
        {
            this.walkable = walkable;
            this.buildable = buildable;
            this.ground = ground;
        }

        public Structure Get(int id) => byId.TryGetValue(id, out var s) ? s : null;

        /// A new, empty base (id 0: the next free one; a saved base keeps its own).
        public Structure Create(double x, double z, double angle, double floor, int id = 0)
        {
            var s = new Structure { Id = id > 0 ? id : nextId, X = x, Z = z, Angle = angle, Floor = floor };
            nextId = Math.Max(nextId, s.Id + 1);
            byId[s.Id] = s;
            All.Add(s);
            return s;
        }

        public void Remove(Structure s)
        {
            byId.Remove(s.Id);
            All.Remove(s);
            Refresh();
        }

        // ---- Cells' tiles ----

        public static bool IsCell(Tile t) => t.X >= BaseSpace;

        public static Tile CellTile(Structure s, int i, int j) => new Tile(BaseSpace + s.Id * Span + Half + i, BaseSpace + Half + j);

        /// The base and cell a cell's tile stands for (false for a world tile, or a base that's gone).
        public bool TryCell(Tile t, out Structure s, out int i, out int j)
        {
            s = null;
            i = j = 0;
            if (!IsCell(t))
                return false;
            int id = (t.X - BaseSpace) / Span;
            i = t.X - BaseSpace - id * Span - Half;
            j = t.Z - BaseSpace - Half;
            return byId.TryGetValue(id, out s);
        }

        /// Where a tile is: a world tile's centre on the ground, or a cell's centre (on its floor, or on the
        /// ground for an apron cell). Map space.
        public void Centre(Tile t, out double x, out double z, out double y)
        {
            if (TryCell(t, out var s, out int i, out int j))
            {
                s.ToMap(i, j, out x, out z);
                y = s.HasFoundation(i, j) ? s.Floor : ground(x, z);
                return;
            }
            x = t.X;
            z = t.Z;
            y = ground(x, z);
        }

        /// The world tile under a tile (itself, for a world tile).
        public Tile GroundTile(Tile t)
        {
            if (!IsCell(t))
                return t;
            Centre(t, out double x, out double z, out _);
            return new Tile((int)Math.Round(x), (int)Math.Round(z));
        }

        /// The foundation cell's tile a map-space point stands on, or null (on the ground, or under a floor).
        public Tile? CellAt(double x, double z, double y)
        {
            foreach (var s in All)
            {
                double dx = x - s.X, dz = z - s.Z;
                if (dx * dx + dz * dz > (s.Radius + 0.5) * (s.Radius + 0.5) || y < s.Floor - 0.7)
                    continue;
                s.ToLocal(x, z, out double fi, out double fj);
                int i = (int)Math.Round(fi), j = (int)Math.Round(fj);
                if (s.HasFoundation(i, j))
                    return CellTile(s, i, j);
            }
            return null;
        }

        /// Whether a map-space point is on any base's foundation (seen from above).
        public bool OnFoundation(double x, double z, Structure except = null)
        {
            foreach (var s in All)
            {
                if (s == except)
                    continue;
                double dx = x - s.X, dz = z - s.Z;
                if (dx * dx + dz * dz > (s.Radius + 0.5) * (s.Radius + 0.5))
                    continue;
                s.ToLocal(x, z, out double fi, out double fj);
                if (s.HasFoundation((int)Math.Round(fi), (int)Math.Round(fj)))
                    return true;
            }
            return false;
        }

        /// The base with a foundation nearest a point, if it's within `reach` (map units) of the point.
        public Structure Near(double x, double z, double reach)
        {
            Structure best = null;
            double bestD = reach;
            foreach (var s in All)
            {
                double dx = x - s.X, dz = z - s.Z;
                if (Math.Sqrt(dx * dx + dz * dz) > s.Radius + reach + 1)
                    continue;
                s.ToLocal(x, z, out double fi, out double fj);
                foreach (var k in s.Foundations.Keys)
                {
                    Structure.Unkey(k, out int i, out int j);
                    // Distance from the point to the cell's square.
                    double ox = Math.Max(0, Math.Abs(fi - i) - 0.5), oz = Math.Max(0, Math.Abs(fj - j) - 0.5);
                    double d = Math.Sqrt(ox * ox + oz * oz);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = s;
                    }
                }
            }
            return best;
        }

        // ---- Walking ----

        public bool InFootprint(Tile world) => footprint.Contains(world);

        /// Whether a cell's tile can be walked on: a foundation with nothing in it, or an apron cell (beside a
        /// foundation, with walkable ground not far below or above the floor).
        public bool CellWalkable(Tile t)
        {
            if (!TryCell(t, out var s, out int i, out int j))
                return false;
            long k = Structure.Key(i, j);
            if (s.Foundations.ContainsKey(k))
                return !s.Furniture.ContainsKey(k);
            if (!s.HasFoundation(i + 1, j) && !s.HasFoundation(i - 1, j) && !s.HasFoundation(i, j + 1) && !s.HasFoundation(i, j - 1))
                return false;
            s.ToMap(i, j, out double x, out double z);
            return Math.Abs(s.Floor - ground(x, z)) <= MaxStep && walkable(new Tile((int)Math.Round(x), (int)Math.Round(z))) && !OnFoundation(x, z);
        }

        /// Whether a step from one of a base's cells to the next crosses no wall or window.
        public bool Open(Tile from, int dx, int dz) => TryCell(from, out var s, out int i, out int j) && s.Open(i, j, dx, dz);

        /// The tiles joined to this one other than its neighbours: an apron cell's world tiles, or a world
        /// tile's apron cells.
        public List<Tile> LinksOf(Tile t) => links.TryGetValue(t, out var l) ? l : NoLinks;

        /// About how many steps apart two tiles are, by where they are.
        public int Estimate(Tile a, Tile b)
        {
            if (!IsCell(a) && !IsCell(b))
                return a.StepsTo(b);
            Centre(a, out double ax, out double az, out _);
            Centre(b, out double bx, out double bz, out _);
            return (int)Math.Max(Math.Abs(ax - bx), Math.Abs(az - bz));
        }

        /// Works out the footprints and the links between aprons and world tiles again, after a change.
        public void Refresh()
        {
            footprint.Clear();
            links.Clear();
            foreach (var s in All)
            {
                s.Measure();
                foreach (var k in s.Foundations.Keys)
                {
                    Structure.Unkey(k, out int i, out int j);
                    foreach (var w in CoveredTiles(s, i, j))
                        footprint.Add(w);
                }
            }
            foreach (var s in All)
                foreach (var k in s.Foundations.Keys)
                {
                    Structure.Unkey(k, out int i, out int j);
                    for (int d = 0; d < 4; d++)
                    {
                        int ai = i + (d == 0 ? 1 : d == 1 ? -1 : 0), aj = j + (d == 2 ? 1 : d == 3 ? -1 : 0);
                        if (s.HasFoundation(ai, aj))
                            continue;
                        var apron = CellTile(s, ai, aj);
                        if (links.ContainsKey(apron))
                            continue;
                        var list = new List<Tile>();
                        links[apron] = list;
                        s.ToMap(ai, aj, out double x, out double z);
                        int rx = (int)Math.Round(x), rz = (int)Math.Round(z);
                        for (int ox = -1; ox <= 1; ox++)
                            for (int oz = -1; oz <= 1; oz++)
                            {
                                var w = new Tile(rx + ox, rz + oz);
                                double ddx = w.X - x, ddz = w.Z - z;
                                if (footprint.Contains(w) || ddx * ddx + ddz * ddz > 0.85 * 0.85)
                                    continue;
                                list.Add(w);
                                if (!links.TryGetValue(w, out var back))
                                    links[w] = back = new List<Tile>();
                                back.Add(apron);
                            }
                    }
                }
        }

        /// The world tiles whose centres are on a cell (or within Margin of it): what a foundation there covers.
        public static IEnumerable<Tile> CoveredTiles(Structure s, int i, int j)
        {
            s.ToMap(i, j, out double cx, out double cz);
            const double reach = 0.5 + Margin;
            int x0 = (int)Math.Floor(cx - 1), x1 = (int)Math.Ceiling(cx + 1), z0 = (int)Math.Floor(cz - 1), z1 = (int)Math.Ceiling(cz + 1);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    s.ToLocal(x, z, out double fi, out double fj);
                    if (Math.Abs(fi - i) <= reach && Math.Abs(fj - j) <= reach)
                        yield return new Tile(x, z);
                }
        }

        // ---- Building rules (each says what's wrong, or null when it can go there) ----

        /// The ground's highest and lowest points under a cell (its centre and corners).
        void GroundUnder(Structure s, int i, int j, out double high, out double low)
        {
            high = double.MinValue;
            low = double.MaxValue;
            for (int c = 0; c < 5; c++)
            {
                double di = c == 0 ? 0 : (c & 1) == 1 ? -0.5 : 0.5, dj = c == 0 ? 0 : c <= 2 ? -0.5 : 0.5;
                s.ToMap(i + di, j + dj, out double x, out double z);
                double h = ground(x, z);
                high = Math.Max(high, h);
                low = Math.Min(low, h);
            }
        }

        /// A new base's first foundation, centred at a map-space point and turned `angle`: its floor height comes
        /// from the ground there.
        public string NewBaseProblem(double x, double z, double angle, out double floor)
        {
            var probe = new Structure { Id = -1, X = x, Z = z, Angle = angle };
            GroundUnder(probe, 0, 0, out double high, out double low);
            floor = high + FloorAbove;
            probe.Floor = floor;
            if (floor - low > MaxSupport)
                return "The ground's too steep to build on here.";
            return CoverProblem(probe, 0, 0);
        }

        /// Another foundation in a base, at cell (i, j): beside one it has, on ground that fits its floor.
        public string FoundationProblem(Structure s, int i, int j)
        {
            if (Math.Abs(i) > MaxCells || Math.Abs(j) > MaxCells)
                return "That's too far from the rest of the building.";
            if (s.HasFoundation(i, j))
                return "There's already a foundation there.";
            if (!s.HasFoundation(i + 1, j) && !s.HasFoundation(i - 1, j) && !s.HasFoundation(i, j + 1) && !s.HasFoundation(i, j - 1))
                return "A foundation has to go beside another one.";
            GroundUnder(s, i, j, out double high, out double low);
            if (high > s.Floor + 0.35)
                return "The ground's too high here: the floor would be buried.";
            if (s.Floor - low > MaxSupport)
                return "The ground falls away too steeply here.";
            return CoverProblem(s, i, j);
        }

        /// What a foundation at (i, j) would cover must be open ground outside towns, and not under another base.
        string CoverProblem(Structure s, int i, int j)
        {
            s.ToMap(i, j, out double x, out double z);
            if (OnFoundation(x, z, s.Id > 0 ? s : null))
                return "There's already a building there.";
            foreach (var w in CoveredTiles(s, i, j))
            {
                if (footprint.Contains(w) && (s.Id <= 0 || !FootprintOf(s, w)))
                    return "There's already a building there.";
                if (!buildable(w))
                    return "You can't build there.";
            }
            return null;
        }

        /// Whether a world tile is under one of this base's own foundations.
        bool FootprintOf(Structure s, Tile w)
        {
            s.ToLocal(w.X, w.Z, out double fi, out double fj);
            const double reach = 0.5 + Margin;
            for (int di = -1; di <= 1; di++)
                for (int dj = -1; dj <= 1; dj++)
                {
                    int i = (int)Math.Round(fi) + di, j = (int)Math.Round(fj) + dj;
                    if (s.HasFoundation(i, j) && Math.Abs(fi - i) <= reach && Math.Abs(fj - j) <= reach)
                        return true;
                }
            return false;
        }

        /// A wall, doorway or window on an edge: it needs a foundation on at least one side.
        public static string EdgeProblem(Structure s, int i, int j, int side)
        {
            if (s.Edges.ContainsKey(Structure.EdgeKey(i, j, side)))
                return "There's already something there.";
            if (!s.HasFoundation(i, j) && !s.HasFoundation(side == 0 ? i : i + 1, side == 0 ? j + 1 : j))
                return "That needs a foundation beside it.";
            return null;
        }

        /// A roof over a cell: on a foundation, held up by a wall, doorway or window round it or a roof beside it.
        public static string RoofProblem(Structure s, int i, int j)
        {
            if (!s.HasFoundation(i, j))
                return "A roof goes over a foundation.";
            if (s.Roofs.ContainsKey(Structure.Key(i, j)))
                return "There's already a roof there.";
            bool held = s.Edges.ContainsKey(Structure.EdgeKey(i, j, 0)) || s.Edges.ContainsKey(Structure.EdgeKey(i, j, 1))
                || s.Edges.ContainsKey(Structure.EdgeKey(i, j - 1, 0)) || s.Edges.ContainsKey(Structure.EdgeKey(i - 1, j, 1))
                || s.Roofs.ContainsKey(Structure.Key(i + 1, j)) || s.Roofs.ContainsKey(Structure.Key(i - 1, j))
                || s.Roofs.ContainsKey(Structure.Key(i, j + 1)) || s.Roofs.ContainsKey(Structure.Key(i, j - 1));
            return held ? null : "A roof needs a wall under it, or another roof beside it.";
        }

        /// Furniture in a cell: on a foundation, with nothing else in it.
        public static string FurnitureProblem(Structure s, int i, int j)
        {
            if (!s.HasFoundation(i, j))
                return "That goes on a foundation.";
            if (s.Furniture.ContainsKey(Structure.Key(i, j)))
                return "There's already something there.";
            return null;
        }

        /// Taking a foundation away: only once nothing stands on it or leans on it.
        public static string RemoveFoundationProblem(Structure s, int i, int j)
        {
            long k = Structure.Key(i, j);
            if (s.Roofs.ContainsKey(k) || s.Furniture.ContainsKey(k))
                return "Take away what's on it first.";
            for (int d = 0; d < 4; d++)
            {
                int dx = d == 0 ? 1 : d == 1 ? -1 : 0, dz = d == 2 ? 1 : d == 3 ? -1 : 0;
                if (s.Edges.ContainsKey(Structure.EdgeBetween(i, j, dx, dz)) && !s.HasFoundation(i + dx, j + dz))
                    return "Take away the walls round its edge first.";
            }
            return null;
        }

        /// What placing a piece costs that you haven't got (null if you have it all), e.g. "4 planks".
        public static string Missing(PieceDef piece, Inventory inv)
        {
            foreach (var input in piece.Inputs)
                if (inv.Count(input.ItemId) < input.Count)
                {
                    var def = ContentDb.Current.Item(input.ItemId);
                    return input.Count + " " + (def != null ? def.Name.ToLowerInvariant() : input.ItemId);
                }
            return null;
        }
    }

    /// A walk grid with bases on it: the world's tiles (less the bases' footprints) and the bases' own cells,
    /// joined at the bases' aprons. Walls and windows block steps between cells.
    public sealed class BaseWalkGrid : WalkGrid
    {
        public readonly WalkGrid Inner;
        readonly BaseSet bases;
        readonly HashSet<Tile> blockedCells = new HashSet<Tile>();

        public BaseWalkGrid(WalkGrid inner, BaseSet bases)
        {
            Inner = inner;
            this.bases = bases;
        }

        public override bool IsWalkable(Tile t) => BaseSet.IsCell(t)
            ? bases.CellWalkable(t) && !blockedCells.Contains(t)
            : Inner.IsWalkable(t) && !bases.InFootprint(t);

        public override void SetBlocked(Tile t, bool value)
        {
            if (!BaseSet.IsCell(t))
                Inner.SetBlocked(t, value);
            else if (value)
                blockedCells.Add(t);
            else
                blockedCells.Remove(t);
        }

        public override bool CanStep(Tile from, int dx, int dz)
        {
            if (!BaseSet.IsCell(from))
                return base.CanStep(from, dx, dz);
            var to = new Tile(from.X + dx, from.Z + dz);
            if (!bases.TryCell(from, out var s, out _, out _) || !bases.TryCell(to, out var s2, out _, out _) || s != s2 || !IsWalkable(to))
                return false;
            if (dx == 0 || dz == 0)
                return bases.Open(from, dx, dz);
            // A diagonal step needs both ways round it clear: both cells it cuts past, and every edge between.
            var a = new Tile(from.X + dx, from.Z);
            var b = new Tile(from.X, from.Z + dz);
            return IsWalkable(a) && IsWalkable(b) && bases.Open(from, dx, 0) && bases.Open(a, 0, dz) && bases.Open(from, 0, dz) && bases.Open(b, dx, 0);
        }

        public override List<Tile> Links(Tile t) => bases.LinksOf(t);
        public override int Estimate(Tile a, Tile b) => bases.Estimate(a, b);
    }
}
