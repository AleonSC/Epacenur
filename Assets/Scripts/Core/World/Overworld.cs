using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    public enum RegionKind { Wild, Town, Camp, Shrine, Quarry }

    /// The kind of country a tile is in: what grows there, how wet it is, the colour of the ground.
    public enum Biome { Meadow, Woodland, Marsh, Highlands, Wasteland }

    /// A town or city, and the safe ground round it. Further out than SafeRadius, the wilderness begins.
    public sealed class Settlement
    {
        public int RX, RZ;                // its region
        public bool City;
        public Tile Center;               // the square
        public int SafeRadius;            // tiles from the square that are safe
    }

    /// What's planned for one region of the overworld: a town, a goblin camp, a shrine, a quarry, or just countryside.
    /// Everything is in world tiles.
    public sealed class RegionPlan
    {
        public int RX, RZ;
        public RegionKind Kind;
        public bool City;                                    // towns: a city (bigger, with a wider safe ring)
        public Area Bounds;
        public Tile Center;
        public TownLayout Town;                              // towns: the built-up tiles (local to Bounds)
        public readonly List<Building> Buildings = new List<Building>();
        public Area Camp;                                    // camps: the trampled ground; shrines: the paving round the altar; quarries: the pit
        public readonly List<Tile> Tents = new List<Tile>();
        public Area Level;                                   // towns, camps, shrines, quarries: kept flat (hills rise beyond it)
        public Tile Altar;                                   // shrines: where the altar stands
        public readonly List<Tile> Stones = new List<Tile>(); // shrines: the standing stones round it
        public readonly HashSet<Tile> Essence = new HashSet<Tile>();  // shrines: rune essence boulders round the paving
        public readonly HashSet<Tile> Rocks = new HashSet<Tile>();    // quarries: the ore rocks in the pit
        public readonly List<Tile> Rubble = new List<Tile>();         // quarries: heaps of spoil in the pit's corners
        public Tile Cart;                                    // quarries: a mine cart full of ore
        public readonly HashSet<Tile> Blocked = new HashSet<Tile>();  // features and tents stand here

        /// The planned terrain of a tile in this region, or Grass where nothing's planned.
        public Terrain At(Tile t)
        {
            if (Town != null)
                return Town.Map[t.X - Bounds.X, t.Z - Bounds.Z];
            if (Kind == RegionKind.Camp && Camp.Contains(t))
                return Terrain.Camp;
            if (Kind == RegionKind.Shrine && Camp.Contains(t))
                return Terrain.Stone;
            if (Kind == RegionKind.Shrine && Essence.Contains(t))
                return Terrain.Essence;
            if (Kind == RegionKind.Quarry && Camp.Contains(t))
                return Rocks.Contains(t) ? Terrain.Rock : Terrain.Quarry;
            return Terrain.Grass;
        }

        /// No trees, rocks or water this close to what's built.
        public bool KeepsClear(Tile t)
        {
            if (Kind == RegionKind.Town)
                return t.StepsTo(Center) <= (City ? Overworld.CityClearRadius : Overworld.TownClearRadius);
            if (Kind == RegionKind.Camp || Kind == RegionKind.Shrine || Kind == RegionKind.Quarry)
                return t.X >= Camp.X - 3 && t.X < Camp.XMax + 3 && t.Z >= Camp.Z - 3 && t.Z < Camp.ZMax + 3;
            return false;
        }
    }

    /// The endless overworld, worked out from a seed: the same tile always comes out the same, so it can be
    /// built a chunk at a time as you walk. It's split into square regions. Towns and cities lie far apart
    /// (one at most in each cell of 5 x 5 regions, the start always a city); round each is a ring of safe
    /// ground, and beyond it the wilderness, more dangerous the further you are from any town (Danger),
    /// until you near the next. Other regions are goblin camps, shrines, quarries or wild countryside.
    /// Over that lies the countryside itself, in biomes (meadow, woodland, marsh, highlands, and wasteland
    /// deep in the wilderness): forests, rocky ground and flax fields where smooth noises are high, and ponds.
    public sealed class Overworld
    {
        public const int RegionSize = 64, ChunkSize = 16;
        public const int TownClearRadius = 15, CityClearRadius = 22;  // no trees or ponds this close to the square
        public const int SettlementCell = 5;                             // regions across a settlement's cell
        public const int TownSafeRadius = 48, CitySafeRadius = 80;
        public const double TilesPerLevel = 6;                           // a wilderness level every this many tiles
        public const int MaxDanger = 60;
        public const int DeepWilderness = 24;                            // from this level the land is wasteland
        public readonly int Seed;
        readonly ValueNoise forest, ponds, rocky, fields, hills, bumps, ranges, moisture, warp, edge;
        readonly Dictionary<long, RegionPlan> plans = new Dictionary<long, RegionPlan>();
        readonly Dictionary<long, Settlement> settlements = new Dictionary<long, Settlement>();
        // Slow-changing fields are worked out on a coarse lattice and blended between its points.
        readonly CoarseField moistureField, dangerField;
        readonly Dictionary<long, double> pondCorners = new Dictionary<long, double>();

        public Overworld(int seed)
        {
            Seed = seed;
            forest = new ValueNoise(seed);
            ponds = new ValueNoise(seed + 101);
            rocky = new ValueNoise(seed + 202);
            fields = new ValueNoise(seed + 303);
            hills = new ValueNoise(seed + 404);
            bumps = new ValueNoise(seed + 505);
            ranges = new ValueNoise(seed + 606);
            moisture = new ValueNoise(seed + 707);
            warp = new ValueNoise(seed + 808);
            edge = new ValueNoise(seed + 909);
            moistureField = new CoarseField(8, ExactMoisture);
            dangerField = new CoarseField(4, ExactDanger);
        }

        /// The town square you start in.
        public Tile Spawn => new Tile(RegionSize / 2, RegionSize / 2);

        public static int RegionOf(int tileCoord) => tileCoord >= 0 ? tileCoord / RegionSize : (tileCoord + 1) / RegionSize - 1;

        public RegionPlan PlanAt(Tile t) => Plan(RegionOf(t.X), RegionOf(t.Z));

        /// A number 0..1 that only depends on the seed and its inputs.
        public double Hash(int x, int z, int salt)
        {
            unchecked
            {
                ulong h = (ulong)Seed * 0x9E3779B97F4A7C15UL;
                h ^= (ulong)(uint)x * 0xBF58476D1CE4E5B9UL;
                h = (h ^ (h >> 31)) * 0x94D049BB133111EBUL;
                h ^= (ulong)(uint)z * 0xD6E8FEB86659FD93UL;
                h = (h ^ (h >> 29)) * 0xBF58476D1CE4E5B9UL;
                h ^= (ulong)(uint)salt * 0x9E3779B97F4A7C15UL;
                h = (h ^ (h >> 31)) * 0x94D049BB133111EBUL;  // mixed again after the salt, so different salts don't agree
                h ^= h >> 32;
                return (h & 0xFFFFFFFFFFFFUL) / (double)0x1000000000000UL;
            }
        }

        public RegionPlan Plan(int rx, int rz)
        {
            long key = ChunkedGrid.Key(rx, rz);
            if (plans.TryGetValue(key, out var plan))
                return plan;
            plan = new RegionPlan { RX = rx, RZ = rz, Bounds = new Area(rx * RegionSize, rz * RegionSize, RegionSize, RegionSize) };
            plan.Center = new Tile(plan.Bounds.X + RegionSize / 2, plan.Bounds.Z + RegionSize / 2);
            double roll = Hash(rx, rz, 1);
            // Towns and cities where the settlements are (the start's a city); just east of the start, a shrine
            // (an air altar to start runecrafting); elsewhere camps, shrines, quarries and wild country.
            var settlement = SettlementOf(rx, rz);
            bool here = settlement != null && settlement.RX == rx && settlement.RZ == rz;
            plan.City = here && settlement.City;
            plan.Kind = here ? RegionKind.Town : rx == 1 && rz == 0 ? RegionKind.Shrine
                : roll < 0.22 ? RegionKind.Camp : roll < 0.31 ? RegionKind.Shrine : roll < 0.4 ? RegionKind.Quarry : RegionKind.Wild;
            // Not in a lake: a camp, shrine or quarry needs dry ground under its middle. (Ponds have no pits, so
            // dry ground there always runs on out of the lake, and its ground is kept clear of water.)
            if ((plan.Kind == RegionKind.Camp || plan.Kind == RegionKind.Quarry || (plan.Kind == RegionKind.Shrine && !(rx == 1 && rz == 0)))
                && PondNoise(plan.Center.X, plan.Center.Z) > PondLevel(plan.Center.X, plan.Center.Z) - 0.1)
                plan.Kind = RegionKind.Wild;
            if (plan.Kind == RegionKind.Town)
                PlanTown(plan);
            else if (plan.Kind == RegionKind.Camp)
                PlanCamp(plan);
            else if (plan.Kind == RegionKind.Shrine)
                PlanShrine(plan);
            else if (plan.Kind == RegionKind.Quarry)
                PlanQuarry(plan);
            plans[key] = plan;
            return plan;
        }

        void PlanTown(RegionPlan plan)
        {
            int seed = (int)(Hash(plan.RX, plan.RZ, 2) * int.MaxValue);
            plan.Town = WorldGen.GenerateTown(RegionSize, seed, plan.City);
            int clear = plan.City ? CityClearRadius : TownClearRadius;
            int x0 = plan.Center.X - clear, z0 = plan.Center.Z - clear, x1 = plan.Center.X + clear, z1 = plan.Center.Z + clear;
            for (int x = 0; x < RegionSize; x++)
                for (int z = 0; z < RegionSize; z++)
                    if (plan.Town.Map[x, z] != Terrain.Grass)
                    {
                        x0 = Math.Min(x0, plan.Bounds.X + x); x1 = Math.Max(x1, plan.Bounds.X + x);
                        z0 = Math.Min(z0, plan.Bounds.Z + z); z1 = Math.Max(z1, plan.Bounds.Z + z);
                    }
            plan.Level = new Area(x0 - 2, z0 - 2, x1 - x0 + 5, z1 - z0 + 5);
            foreach (var b in plan.Town.Buildings)
            {
                var moved = b.Moved(plan.Bounds.X, plan.Bounds.Z);
                plan.Buildings.Add(moved);
                plan.Blocked.Add(moved.Feature);
                if (moved.HasFeature2)
                    plan.Blocked.Add(moved.Feature2);
            }
        }

        void PlanCamp(RegionPlan plan)
        {
            int dx = (int)(Hash(plan.RX, plan.RZ, 3) * 20) - 10, dz = (int)(Hash(plan.RX, plan.RZ, 4) * 20) - 10;
            plan.Camp = new Area(plan.Center.X + dx - 6, plan.Center.Z + dz - 5, 12, 10);
            plan.Level = new Area(plan.Camp.X - 3, plan.Camp.Z - 3, plan.Camp.Width + 6, plan.Camp.Height + 6);
            for (int i = 0; i < 2; i++)
            {
                var tent = new Tile(plan.Camp.X + 2 + i * (plan.Camp.Width - 5), plan.Camp.ZMax - 2);
                plan.Tents.Add(tent);
                plan.Blocked.Add(tent);
            }
        }

        /// A ring of standing stones on paving, with the altar in the middle, and rune essence boulders round it
    /// (in pairs either side of each way in, so it can always be reached straight on).
        void PlanShrine(RegionPlan plan)
        {
            int dx = (int)(Hash(plan.RX, plan.RZ, 5) * 16) - 8, dz = (int)(Hash(plan.RX, plan.RZ, 6) * 16) - 8;
            var c = new Tile(plan.Center.X + dx, plan.Center.Z + dz);
            plan.Camp = new Area(c.X - 3, c.Z - 3, 7, 7);
            plan.Level = new Area(plan.Camp.X - 3, plan.Camp.Z - 3, plan.Camp.Width + 6, plan.Camp.Height + 6);
            plan.Altar = c;
            plan.Blocked.Add(c);
            for (int x = -3; x <= 3; x += 3)
                for (int z = -3; z <= 3; z += 3)
                    if (x != 0 || z != 0)
                    {
                        var stone = new Tile(c.X + x, c.Z + z);
                        plan.Stones.Add(stone);
                        plan.Blocked.Add(stone);
                    }
            var essence = new[] { new Tile(5, 2), new Tile(5, -2), new Tile(-5, 2), new Tile(-5, -2), new Tile(2, 5), new Tile(-2, 5), new Tile(2, -5), new Tile(-2, -5) };
            for (int i = 0; i < essence.Length; i++)
                if (i < 2 || Hash(plan.RX, plan.RZ, 60 + i) < 0.75)
                    plan.Essence.Add(new Tile(c.X + essence[i].X, c.Z + essence[i].Z));
        }

        /// An open pit of gravel with ore rocks in rows (never touching, so you can walk between them all),
        /// heaps of spoil in its corners and a mine cart at the top.
        void PlanQuarry(RegionPlan plan)
        {
            int dx = (int)(Hash(plan.RX, plan.RZ, 3) * 16) - 8, dz = (int)(Hash(plan.RX, plan.RZ, 4) * 16) - 8;
            var q = plan.Camp = new Area(plan.Center.X + dx - 7, plan.Center.Z + dz - 6, 14, 12);
            for (int x = q.X + 2; x < q.XMax - 2; x += 3)
                for (int z = q.Z + 2; z < q.ZMax - 2; z += 3)
                    if (Hash(x, z, 61) < 0.85)
                        plan.Rocks.Add(new Tile(x, z));
            foreach (var corner in new[] { new Tile(q.X, q.Z), new Tile(q.XMax - 1, q.Z), new Tile(q.X, q.ZMax - 1), new Tile(q.XMax - 1, q.ZMax - 1) })
            {
                plan.Rubble.Add(corner);
                plan.Blocked.Add(corner);
            }
            plan.Cart = new Tile(q.X + q.Width / 2, q.ZMax - 1);
            plan.Blocked.Add(plan.Cart);
            plan.Level = new Area(q.X - 3, q.Z - 3, q.Width + 6, q.Height + 6);
        }

        // ---- Towns, cities and the wilderness ----

        /// The settlement in the cell holding region (rx, rz), or null if that cell has none.
        public Settlement SettlementOf(int rx, int rz) => SettlementInCell(CellOf(rx), CellOf(rz));

        static int CellOf(int r) => FloorDiv(r + SettlementCell / 2, SettlementCell);

        /// Each cell of 5 x 5 regions has a town or city at a region near its middle (1 in 5 has none, leaving
        /// wide wilds); the cell round the start has the start's city. Neighbours are at least 3 regions apart.
        public Settlement SettlementInCell(int cx, int cz)
        {
            long key = ChunkedGrid.Key(cx, cz);
            if (settlements.TryGetValue(key, out var s))
                return s;
            bool home = cx == 0 && cz == 0;
            if (home || Hash(cx, cz, 70) < 0.8)
            {
                int rx = home ? 0 : cx * SettlementCell - 1 + (int)(Hash(cx, cz, 71) * 3);
                int rz = home ? 0 : cz * SettlementCell - 1 + (int)(Hash(cx, cz, 72) * 3);
                bool city = home || Hash(cx, cz, 73) < 0.25;
                s = new Settlement
                {
                    RX = rx, RZ = rz, City = city, SafeRadius = city ? CitySafeRadius : TownSafeRadius,
                    Center = new Tile(rx * RegionSize + RegionSize / 2, rz * RegionSize + RegionSize / 2),
                };
            }
            settlements[key] = s;
            return s;
        }

        /// The town or city nearest a tile, measured by how far you are into its wilderness (so a city's wider
        /// safe ring counts), and that distance: how many tiles past its safe ring you are (0 or less: safe).
        public Settlement NearestSettlement(double x, double z, out double beyond)
        {
            Settlement best = null;
            beyond = double.MaxValue;
            int cx = CellOf(RegionOf((int)Math.Floor(x))), cz = CellOf(RegionOf((int)Math.Floor(z)));
            // The nearest is almost always in the 3 x 3 cells round you; look further only if it isn't.
            for (int ring = 1; ring <= 3 && (best == null || ring == 2 && beyond > CellReach); ring++)
                for (int dx = -ring; dx <= ring; dx++)
                    for (int dz = -ring; dz <= ring; dz++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring && ring > 1)
                            continue;
                        var s = SettlementInCell(cx + dx, cz + dz);
                        if (s == null)
                            continue;
                        double ox = x - s.Center.X, oz = z - s.Center.Z;
                        double b = Math.Sqrt(ox * ox + oz * oz) - s.SafeRadius;
                        if (b < beyond)
                        {
                            beyond = b;
                            best = s;
                        }
                    }
            return best;
        }

        public Settlement NearestSettlement(Tile t) => NearestSettlement(t.X, t.Z, out _);

        const double CellReach = 280;  // past this, a settlement two cells away might be nearer

        /// How dangerous a place is: 0 in the safe ring round a town or city, then the wilderness level,
        /// 1 just outside and one more every TilesPerLevel tiles further from safety, up to MaxDanger.
        /// The rings' edges wander a little, so they aren't perfect circles.
        public double DangerAt(double x, double z) => dangerField.At(x, z);

        double ExactDanger(double x, double z)
        {
            NearestSettlement(x, z, out double beyond);
            beyond += (edge.Fractal(x / 40.0, z / 40.0, 2) - 0.5) * 24;
            if (beyond <= 0)
                return 0;
            return Math.Min(MaxDanger, 1 + beyond / TilesPerLevel);
        }

        /// The wilderness level of a tile (0: safe).
        public int Danger(Tile t) => (int)DangerAt(t.X, t.Z);

        // ---- Biomes ----

        /// How wet the country is (0..1): a slow, wandering noise. Marshes where it's high, meadows where low.
        public double Moisture(double x, double z) => moistureField.At(x, z);

        double ExactMoisture(double x, double z)
        {
            Warp(x, z, 120.0, 70.0, out double wx, out double wz);
            return moisture.Fractal(wx / 320.0, wz / 320.0, 3);
        }

        /// Nudges a point by a smooth noise, so shapes sampled there wind instead of being round blobs.
        void Warp(double x, double z, double scale, double strength, out double wx, out double wz)
        {
            wx = x + (warp.Fractal(x / scale, z / scale, 2) - 0.5) * strength;
            wz = z + (warp.Fractal(x / scale + 91.7, z / scale - 53.3, 2) - 0.5) * strength;
        }

        /// The biome at a tile: wasteland deep in the wilderness (unless it's wet), highlands up in the hills,
        /// then marsh, woodland or meadow by how wet it is.
        public Biome BiomeAt(Tile t) => BiomeFor(Moisture(t.X, t.Z), Hills(t.X, t.Z), DangerAt(t.X, t.Z));

        static Biome BiomeFor(double wet, double height, double danger)
        {
            if (danger >= DeepWilderness && wet < 0.62)
                return Biome.Wasteland;
            if (height > 3.2)
                return Biome.Highlands;
            if (wet > 0.64)
                return Biome.Marsh;
            if (wet > 0.46)
                return Biome.Woodland;
            return Biome.Meadow;
        }

        /// Where ponds form (their noise above this): lower, so more water, the wetter the country.
        double PondLevel(double x, double z) => 0.84 - 0.12 * Smooth((Moisture(x, z) - 0.55) / 0.15);

        // ---- Elevation ----

        public const double HillHeight = 6, BumpHeight = 0.8;
        const double LevelBlend = 12;  // tiles from flat ground to the full height of the hills

        /// How high the ground is (world units) at a point in tile coordinates (a tile's centre is a whole
        /// number). Rolling hills, higher where a very slow noise (ranges) is high, with small bumps on top;
        /// flat at 0 in and round towns, camps, shrines, quarries and ponds, so buildings, paving and water sit level.
        public double Elevation(double x, double z)
        {
            double flat = Flatness(x, z);
            if (flat >= 1)
                return 0;
            // Down to the water: nothing near a pond is raised (ponds form where their noise passes PondLevel;
            // the hills hold it down, so there are none up in them).
            double h = Hills(x, z);
            double dry = Smooth((PondLevel(x, z) - 0.02 - PondNoise(x, z)) / 0.7);
            return h * (1 - flat) * dry;
        }

        /// The hills before anything is levelled.
        double Hills(double x, double z)
        {
            double n = (hills.Sample(x / 48.0, z / 48.0) - 0.42) / 0.58;
            double h = n <= 0 ? 0 : n * n * (3 - 2 * n) * HillHeight * (0.6 + 1.2 * ranges.Sample(x / 220.0, z / 220.0));
            return h + bumps.Sample(x / 10.0, z / 10.0) * BumpHeight;
        }


        public double Elevation(Tile t) => Elevation(t.X, t.Z);

        /// 1 in a town, camp, shrine or quarry (and a little way round it), falling smoothly to 0 away from them.
        double Flatness(double x, double z)
        {
            int rx = RegionOf((int)Math.Round(x)), rz = RegionOf((int)Math.Round(z));
            double best = 0;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var plan = Plan(rx + dx, rz + dz);
                    if (plan.Kind == RegionKind.Wild)
                        continue;
                    var a = plan.Level;
                    double ox = Math.Max(0, Math.Max(a.X - 0.5 - x, x - (a.XMax - 0.5)));
                    double oz = Math.Max(0, Math.Max(a.Z - 0.5 - z, z - (a.ZMax - 0.5)));
                    double f = 1 - Smooth(Math.Sqrt(ox * ox + oz * oz) / LevelBlend);
                    if (f > best)
                        best = f;
                }
            return best;
        }

        /// 0 below 0, 1 above 1, smooth between.
        static double Smooth(double t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            return t * t * (3 - 2 * t);
        }

        // ---- Tiles ----

        /// What a tile is. Worked out once, then remembered (by chunk), since the game asks about the same
        /// tiles many times over (building, walking, the maps).
        public Terrain At(Tile t)
        {
            int cx = FloorDiv(t.X, ChunkSize), cz = FloorDiv(t.Z, ChunkSize);
            long key = ChunkedGrid.Key(cx, cz);
            if (!known.TryGetValue(key, out var tiles))
            {
                if (known.Count > 4096)
                    known.Clear();
                tiles = new byte[ChunkSize * ChunkSize];
                for (int i = 0; i < tiles.Length; i++)
                    tiles[i] = Unknown;
                known[key] = tiles;
            }
            int k = (t.X - cx * ChunkSize) * ChunkSize + (t.Z - cz * ChunkSize);
            if (tiles[k] == Unknown)
                tiles[k] = (byte)WorkOut(t);
            return (Terrain)tiles[k];
        }

        const byte Unknown = 255;
        readonly Dictionary<long, byte[]> known = new Dictionary<long, byte[]>();

        Terrain WorkOut(Tile t)
        {
            var plan = PlanAt(t);
            var planned = plan.At(t);
            if (planned != Terrain.Grass || plan.KeepsClear(t))
                return planned;
            if (IsPond(t))
                return Terrain.Water;
            return Growth(t, plan);
        }

        /// Whether a tile can't be walked on (before anything the game adds, like fires).
        public bool IsBlocked(Tile t) => !TownLayout.IsWalkable(At(t)) || PlanAt(t).Blocked.Contains(t);

        /// Ponds: blobs where a smooth noise is high, away from towns, camps and roads, shaped so that no dry land
        /// is ever shut in by water.
        bool IsPond(Tile t)
        {
            var plan = PlanAt(t);
            if (plan.KeepsClear(t) || plan.At(t) != Terrain.Grass)
                return false;
            // Roads stay dry with a margin (a road can run diagonally, which water either side would cut).
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var n = new Tile(t.X + dx, t.Z + dz);
                    if (PlanAt(n).At(n) != Terrain.Grass)
                        return false;
                }
            // Water only where this tile and all four next to it are under the pond: that pulls each shore in
            // by a tile, so where dry land narrows to a neck thinner than a tile (or a channel between two lakes
            // nearly closes) it stays dry and nothing is cut off. (The noise has no pits; see PondNoise.)
            if (!RawPond(t.X, t.Z))
                return false;
            return RawPond(t.X + 1, t.Z) && RawPond(t.X - 1, t.Z) && RawPond(t.X, t.Z + 1) && RawPond(t.X, t.Z - 1);
        }

        bool RawPond(int x, int z) => PondNoise(x, z) > PondLevel(x, z);

        /// Ponds' noise: smooth blobs, held down in the hills, bent by a gentle warp into winding shapes.
        /// It has no pits (see PondCorner), so it never leaves an island of dry land shut in by water: only one
        /// octave (finer detail would leave islands), and a warp gentle enough to bend shapes without tearing them.
        double PondNoise(double x, double z)
        {
            double wx = x + (warp.Sample(x / 40.0, z / 40.0) - 0.5) * 14, wz = z + (warp.Sample(x / 40.0 + 91.7, z / 40.0 - 53.3) - 0.5) * 14;
            double gx = wx / PondScale, gz = wz / PondScale;
            int i = (int)Math.Floor(gx), j = (int)Math.Floor(gz);
            double fx = gx - i, fz = gz - j;
            fx = fx * fx * fx * (fx * (fx * 6 - 15) + 10);
            fz = fz * fz * fz * (fz * (fz * 6 - 15) + 10);
            double a = PondCorner(i, j), b = PondCorner(i + 1, j), c = PondCorner(i, j + 1), d = PondCorner(i + 1, j + 1);
            return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fz;
        }

        const double PondScale = 16;

        /// The pond noise at a point of its lattice: the noise, less where the hills are high, and never lower
        /// than all eight points round it (a pit is raised to the lowest of them). Between points the noise is a
        /// blend that can't dip below its corners, so its lowest places are at the points; with no pits, a stretch
        /// below any level always runs on to another, never shut in.
        double PondCorner(int i, int j)
        {
            long key = ChunkedGrid.Key(i, j);
            if (pondCorners.TryGetValue(key, out double v))
                return v;
            if (pondCorners.Count > 1 << 18)
                pondCorners.Clear();
            double lowest = double.MaxValue;
            for (int di = -1; di <= 1; di++)
                for (int dj = -1; dj <= 1; dj++)
                    if (di != 0 || dj != 0)
                        lowest = Math.Min(lowest, PondPoint(i + di, j + dj));
            return pondCorners[key] = Math.Max(PondPoint(i, j), lowest);
        }

        double PondPoint(int i, int j) => ponds.At(i, j) - 0.4 * Math.Max(0, Hills(i * PondScale, j * PondScale) - 1.0);

        /// Trees and rocks stand on a loose lattice (at most one in each 3 x 3 cell, at one of its four
        /// lower-left tiles), so no two ever touch, even diagonally: you can always walk between them.
        Terrain Growth(Tile t, RegionPlan plan)
        {
            int cx = FloorDiv(t.X, 3), cz = FloorDiv(t.Z, 3);
            int ox = Hash(cx, cz, 5) < 0.5 ? 0 : 1, oz = Hash(cx, cz, 6) < 0.5 ? 0 : 1;
            if (t.X != cx * 3 + ox || t.Z != cz * 3 + oz)
                return Terrain.Grass;
            // Not next to water (shores stay walkable), roads, or within two of a door.
            for (int dx = -2; dx <= 2; dx++)
                for (int dz = -2; dz <= 2; dz++)
                {
                    var n = new Tile(t.X + dx, t.Z + dz);
                    bool near = Math.Abs(dx) <= 1 && Math.Abs(dz) <= 1;
                    var np = PlanAt(n);
                    var nt = np.At(n);
                    if (nt == Terrain.Door || (near && (nt == Terrain.Path || nt == Terrain.Wall || nt == Terrain.Camp || IsPond(n))))
                        return Terrain.Grass;
                    if (near && np.KeepsClear(n))
                        return Terrain.Grass;
                }
            // What grows depends on the biome: highlands and wasteland are rocky, woodland thick with trees,
            // meadows open with flax fields, the wasteland's trees few and far between.
            double roll = Hash(t.X, t.Z, 7);
            var biome = BiomeAt(t);
            double rockLevel = biome == Biome.Highlands ? 0.62 : biome == Biome.Wasteland ? 0.66 : biome == Biome.Marsh ? 0.8 : 0.74;
            if (rocky.Fractal(t.X / 20.0, t.Z / 20.0, 3) > rockLevel)
                return roll < 0.5 ? Terrain.Rock : Terrain.Grass;
            Warp(t.X, t.Z, 50.0, 20.0, out double wx, out double wz);
            double n0 = forest.Fractal(wx / 14.0, wz / 14.0, 4);
            // Flax fields in open meadows (not in forests).
            if (biome == Biome.Meadow && n0 < 0.48 && fields.Fractal(t.X / 12.0, t.Z / 12.0, 2) > 0.66)
                return roll < 0.75 ? Terrain.Flax : Terrain.Grass;
            double shift = biome == Biome.Woodland ? -0.1 : biome == Biome.Meadow ? 0.02 : biome == Biome.Highlands ? 0.08 : 0;
            double chance = n0 > 0.58 + shift ? 0.85 : n0 > 0.42 + shift ? 0.3 : 0.04;
            if (biome == Biome.Wasteland)
                chance *= 0.35;
            return roll < chance ? Terrain.Tree : Terrain.Grass;
        }

        static int FloorDiv(int a, int b) => a >= 0 ? a / b : (a + 1) / b - 1;

        /// Water tiles of a pond with dry land straight next to them, among these tiles: where fishing spots go.
        public List<Tile> Shore(IEnumerable<Tile> tiles)
        {
            var shore = new List<Tile>();
            foreach (var t in tiles)
            {
                if (At(t) != Terrain.Water)
                    continue;
                foreach (var n in new[] { new Tile(t.X + 1, t.Z), new Tile(t.X - 1, t.Z), new Tile(t.X, t.Z + 1), new Tile(t.X, t.Z - 1) })
                    if (!IsBlocked(n))
                    {
                        shore.Add(t);
                        break;
                    }
            }
            return shore;
        }
    }

    /// A smooth function worked out at the points of a coarse lattice (`spacing` tiles apart), remembered,
    /// and blended in between: much cheaper than working it out at every tile, for things that change slowly.
    public sealed class CoarseField
    {
        readonly int spacing;
        readonly Func<double, double, double> exact;
        readonly Dictionary<long, double> values = new Dictionary<long, double>();

        public CoarseField(int spacing, Func<double, double, double> exact)
        {
            this.spacing = spacing;
            this.exact = exact;
        }

        double Point(int i, int j)
        {
            long key = ChunkedGrid.Key(i, j);
            if (values.TryGetValue(key, out double v))
                return v;
            if (values.Count > 1 << 20)
                values.Clear();
            return values[key] = exact((double)i * spacing, (double)j * spacing);
        }

        public double At(double x, double z)
        {
            double gx = x / spacing, gz = z / spacing;
            int i = (int)Math.Floor(gx), j = (int)Math.Floor(gz);
            double fx = gx - i, fz = gz - j;
            double a = Point(i, j), b = Point(i + 1, j), c = Point(i, j + 1), d = Point(i + 1, j + 1);
            return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fz;
        }
    }
}
