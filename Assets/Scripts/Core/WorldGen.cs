using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// What a tile of the main world is.
    public enum Terrain : byte
    {
        Grass,
        Tree,   // a tree stands here
        Water,
        Wall,   // a building wall or the world's edge fence
        Floor,  // inside a building
        Door,
        Path,   // dirt road
        Camp,   // trampled ground in the goblin camp
    }

    public enum BuildingKind { Bank, Shop, Chapel, House }

    /// A rectangle of tiles: lower-left corner and size.
    public struct Area
    {
        public int X, Z, Width, Height;
        public Area(int x, int z, int w, int h) { X = x; Z = z; Width = w; Height = h; }
        public int XMax => X + Width;
        public int ZMax => Z + Height;
        public bool Contains(Tile t) => t.X >= X && t.X < XMax && t.Z >= Z && t.Z < ZMax;
        public Tile Center => new Tile(X + Width / 2, Z + Height / 2);
        /// Overlaps another area grown by `margin` tiles on every side.
        public bool Overlaps(Area o, int margin) =>
            X < o.XMax + margin && o.X < XMax + margin && Z < o.ZMax + margin && o.Z < ZMax + margin;
    }

    public sealed class Building
    {
        public BuildingKind Kind;
        public Area Footprint;  // walls run round its edge
        public Tile Door;       // a gap in the wall, facing the town square
        public Tile Feature;    // inside, against the back wall: the bank booth, shop keeper or altar (houses: a table)
    }

    /// The generated main world: a map of tiles plus where things go. Coordinates are local (0..Width-1);
    /// the game places it wherever it likes in the big grid.
    public sealed class MainWorldLayout
    {
        public int Width, Height, Seed;
        public Terrain[,] Map;
        public Tile Spawn;                      // the town square
        public readonly List<Building> Buildings = new List<Building>();
        public readonly List<List<Tile>> PondShores = new List<List<Tile>>();  // per pond: water tiles you can fish from land
        public Area GoblinCamp;

        public Terrain this[Tile t] => Map[t.X, t.Z];
        public bool InBounds(Tile t) => t.X >= 0 && t.Z >= 0 && t.X < Width && t.Z < Height;

        public static bool IsWalkable(Terrain t) => t == Terrain.Grass || t == Terrain.Floor || t == Terrain.Door || t == Terrain.Path || t == Terrain.Camp;

        /// A grid with every blocked tile marked (features inside buildings included).
        public TileGrid ToGrid()
        {
            var g = new TileGrid(Width, Height);
            for (int x = 0; x < Width; x++)
                for (int z = 0; z < Height; z++)
                    g.SetBlocked(new Tile(x, z), !IsWalkable(Map[x, z]));
            foreach (var b in Buildings)
                g.SetBlocked(b.Feature, true);
            return g;
        }
    }

    /// Builds the main world from a seed: a town in the middle (bank, general store, chapel and houses
    /// joined by dirt roads), forests, ponds and a goblin camp, with a fence round the edge.
    /// The same seed always gives the same world.
    public static class WorldGen
    {
        public const int TownClearRadius = 15;  // no trees or ponds this close to the town square

        public static MainWorldLayout Generate(int width, int height, int seed)
        {
            var rng = new Random(seed);
            var w = new MainWorldLayout { Width = width, Height = height, Seed = seed, Map = new Terrain[width, height] };
            w.Spawn = new Tile(width / 2, height / 2);

            for (int x = 0; x < width; x++)
            {
                w.Map[x, 0] = w.Map[x, height - 1] = Terrain.Wall;
            }
            for (int z = 0; z < height; z++)
            {
                w.Map[0, z] = w.Map[width - 1, z] = Terrain.Wall;
            }

            PlaceBuildings(w, rng);
            LayRoads(w);
            PlaceCamp(w, rng);
            PlacePonds(w, rng);
            PlaceTrees(w, rng, seed);
            OpenUpEnclosures(w);
            FindShores(w);
            return w;
        }

        // ---- Town ----

        static void PlaceBuildings(MainWorldLayout w, Random rng)
        {
            var kinds = new List<BuildingKind> { BuildingKind.Bank, BuildingKind.Shop, BuildingKind.Chapel };
            int houses = 3 + rng.Next(3);
            for (int i = 0; i < houses; i++)
                kinds.Add(BuildingKind.House);

            var square = new Area(w.Spawn.X - 3, w.Spawn.Z - 3, 7, 7);  // kept open
            foreach (var kind in kinds)
            {
                for (int tries = 0; tries < 400; tries++)
                {
                    int bw = kind == BuildingKind.Chapel ? 7 : kind == BuildingKind.House ? 5 + rng.Next(3) : 7;
                    int bh = kind == BuildingKind.Chapel ? 8 : kind == BuildingKind.House ? 5 + rng.Next(2) : 6;
                    double angle = rng.NextDouble() * Math.PI * 2;
                    double dist = 7 + rng.NextDouble() * (kind == BuildingKind.House ? 14 : 8);
                    int cx = w.Spawn.X + (int)Math.Round(Math.Cos(angle) * dist);
                    int cz = w.Spawn.Z + (int)Math.Round(Math.Sin(angle) * dist);
                    var area = new Area(cx - bw / 2, cz - bh / 2, bw, bh);
                    if (area.X < 3 || area.Z < 3 || area.XMax > w.Width - 3 || area.ZMax > w.Height - 3)
                        continue;
                    if (area.Overlaps(square, 1))
                        continue;
                    bool clash = false;
                    foreach (var other in w.Buildings)
                        if (area.Overlaps(other.Footprint, 3))
                            clash = true;
                    if (clash)
                        continue;
                    w.Buildings.Add(MakeBuilding(w, kind, area));
                    break;
                }
            }
        }

        /// Walls round the edge, a door on the side facing the town square, and the feature
        /// against the opposite wall.
        static Building MakeBuilding(MainWorldLayout w, BuildingKind kind, Area a)
        {
            for (int x = a.X; x < a.XMax; x++)
                for (int z = a.Z; z < a.ZMax; z++)
                {
                    bool edge = x == a.X || z == a.Z || x == a.XMax - 1 || z == a.ZMax - 1;
                    w.Map[x, z] = edge ? Terrain.Wall : Terrain.Floor;
                }
            int dx = w.Spawn.X - a.Center.X, dz = w.Spawn.Z - a.Center.Z;
            Tile door, feature;
            if (Math.Abs(dx) >= Math.Abs(dz))
            {
                int doorX = dx > 0 ? a.XMax - 1 : a.X;
                int backX = dx > 0 ? a.X + 1 : a.XMax - 2;
                door = new Tile(doorX, a.Center.Z);
                feature = new Tile(backX, a.Center.Z);
            }
            else
            {
                int doorZ = dz > 0 ? a.ZMax - 1 : a.Z;
                int backZ = dz > 0 ? a.Z + 1 : a.ZMax - 2;
                door = new Tile(a.Center.X, doorZ);
                feature = new Tile(a.Center.X, backZ);
            }
            w.Map[door.X, door.Z] = Terrain.Door;
            return new Building { Kind = kind, Footprint = a, Door = door, Feature = feature };
        }

        /// Dirt roads: from each door to the square, and out from the square to the four edges.
        static void LayRoads(MainWorldLayout w)
        {
            foreach (var b in w.Buildings)
            {
                // Start just outside the door.
                var start = OutsideDoor(b);
                DigPath(w, start, w.Spawn);
            }
            DigPath(w, w.Spawn, new Tile(w.Spawn.X, 2));
            DigPath(w, w.Spawn, new Tile(w.Spawn.X, w.Height - 3));
            DigPath(w, w.Spawn, new Tile(2, w.Spawn.Z));
            DigPath(w, w.Spawn, new Tile(w.Width - 3, w.Spawn.Z));
            for (int x = w.Spawn.X - 2; x <= w.Spawn.X + 2; x++)
                for (int z = w.Spawn.Z - 2; z <= w.Spawn.Z + 2; z++)
                    w.Map[x, z] = Terrain.Path;
        }

        public static Tile OutsideDoor(Building b)
        {
            var a = b.Footprint;
            if (b.Door.X == a.X) return new Tile(b.Door.X - 1, b.Door.Z);
            if (b.Door.X == a.XMax - 1) return new Tile(b.Door.X + 1, b.Door.Z);
            if (b.Door.Z == a.Z) return new Tile(b.Door.X, b.Door.Z - 1);
            return new Tile(b.Door.X, b.Door.Z + 1);
        }

        /// An L-shaped road that goes round buildings: steps along x then z, sidestepping walls.
        static void DigPath(MainWorldLayout w, Tile from, Tile to)
        {
            var g = new TileGrid(w.Width, w.Height);
            for (int x = 0; x < w.Width; x++)
                for (int z = 0; z < w.Height; z++)
                {
                    var t = w.Map[x, z];
                    g.SetBlocked(new Tile(x, z), t == Terrain.Wall || t == Terrain.Floor || t == Terrain.Door);
                }
            g.SetBlocked(from, false);
            g.SetBlocked(to, false);
            var path = Pathfinder.PathTo(g, from, to);
            Mark(w, from);
            foreach (var t in path)
                Mark(w, t);
        }

        static void Mark(MainWorldLayout w, Tile t)
        {
            if (w.Map[t.X, t.Z] == Terrain.Grass)
                w.Map[t.X, t.Z] = Terrain.Path;
        }

        // ---- Goblin camp ----

        static void PlaceCamp(MainWorldLayout w, Random rng)
        {
            for (int tries = 0; tries < 300; tries++)
            {
                double angle = rng.NextDouble() * Math.PI * 2;
                double dist = 28 + rng.NextDouble() * 18;
                int cx = w.Spawn.X + (int)(Math.Cos(angle) * dist), cz = w.Spawn.Z + (int)(Math.Sin(angle) * dist);
                var area = new Area(cx - 6, cz - 5, 12, 10);
                if (area.X < 3 || area.Z < 3 || area.XMax > w.Width - 3 || area.ZMax > w.Height - 3)
                    continue;
                bool clash = false;
                foreach (var b in w.Buildings)
                    if (area.Overlaps(b.Footprint, 3))
                        clash = true;
                if (clash)
                    continue;
                for (int x = area.X; x < area.XMax; x++)
                    for (int z = area.Z; z < area.ZMax; z++)
                        if (w.Map[x, z] == Terrain.Grass || w.Map[x, z] == Terrain.Path)
                            w.Map[x, z] = Terrain.Camp;
                w.GoblinCamp = area;
                return;
            }
        }

        // ---- Ponds ----

        static void PlacePonds(MainWorldLayout w, Random rng)
        {
            int ponds = 5 + rng.Next(3);
            int placed = 0;
            for (int tries = 0; tries < 400 && placed < ponds; tries++)
            {
                int cx = 8 + rng.Next(w.Width - 16), cz = 8 + rng.Next(w.Height - 16);
                double rx = 2.5 + rng.NextDouble() * 4, rz = 2.5 + rng.NextDouble() * 4;
                var bounds = new Area(cx - (int)rx - 2, cz - (int)rz - 2, (int)(rx * 2) + 5, (int)(rz * 2) + 5);
                if (Dist(new Tile(cx, cz), w.Spawn) < TownClearRadius + Math.Max(rx, rz))
                    continue;
                bool ok = bounds.X > 2 && bounds.Z > 2 && bounds.XMax < w.Width - 2 && bounds.ZMax < w.Height - 2
                    && !bounds.Overlaps(w.GoblinCamp, 2);
                // Only on open grass, with a grass margin, so roads and buildings stay dry.
                for (int x = bounds.X; ok && x < bounds.XMax; x++)
                    for (int z = bounds.Z; ok && z < bounds.ZMax; z++)
                        if (w.Map[x, z] != Terrain.Grass)
                            ok = false;
                if (!ok)
                    continue;
                double wobble = rng.NextDouble() * 10;
                for (int x = bounds.X; x < bounds.XMax; x++)
                    for (int z = bounds.Z; z < bounds.ZMax; z++)
                    {
                        double ex = (x - cx) / rx, ez = (z - cz) / rz;
                        double edge = 1 + 0.18 * Math.Sin(Math.Atan2(ez, ex) * 3 + wobble);
                        if (ex * ex + ez * ez <= edge * edge)
                            w.Map[x, z] = Terrain.Water;
                    }
                placed++;
            }
        }

        // ---- Forests ----

        /// Trees are dense where a smooth noise is high (forests) and rare elsewhere. No tree touches
        /// another, even diagonally, so you can always walk between them.
        static void PlaceTrees(MainWorldLayout w, Random rng, int seed)
        {
            var noise = new ValueNoise(seed);
            for (int x = 2; x < w.Width - 2; x++)
                for (int z = 2; z < w.Height - 2; z++)
                {
                    if (w.Map[x, z] != Terrain.Grass || Dist(new Tile(x, z), w.Spawn) < TownClearRadius)
                        continue;
                    if (NearAny(w, x, z, Terrain.Tree, 1) || NearAny(w, x, z, Terrain.Door, 2) || NearAny(w, x, z, Terrain.Path, 0))
                        continue;
                    double n = noise.Sample(x / 14.0, z / 14.0);
                    double chance = n > 0.58 ? 0.6 : n > 0.42 ? 0.18 : 0.02;
                    if (rng.NextDouble() < chance)
                        w.Map[x, z] = Terrain.Tree;
                }
        }

        static bool NearAny(MainWorldLayout w, int x, int z, Terrain t, int r)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    if (dx == 0 && dz == 0)
                        continue;
                    int nx = x + dx, nz = z + dz;
                    if (nx >= 0 && nz >= 0 && nx < w.Width && nz < w.Height && w.Map[nx, nz] == t)
                        return true;
                }
            return false;
        }

        /// Anything walkable that can't be reached from the town square (shut in by trees and water)
        /// gets the trees round it cut down until it can be.
        static void OpenUpEnclosures(MainWorldLayout w)
        {
            for (int round = 0; round < 200; round++)
            {
                var reach = Reachable(w);
                Tile? stranded = null;
                for (int x = 1; x < w.Width - 1 && !stranded.HasValue; x++)
                    for (int z = 1; z < w.Height - 1 && !stranded.HasValue; z++)
                        if (MainWorldLayout.IsWalkable(w.Map[x, z]) && !reach[x, z])
                            stranded = new Tile(x, z);
                if (!stranded.HasValue)
                    return;
                // Clear trees in a widening ring until the stranded tile touches the reachable world.
                var s = stranded.Value;
                for (int r = 1; r < 6; r++)
                {
                    bool cleared = false;
                    for (int dx = -r; dx <= r; dx++)
                        for (int dz = -r; dz <= r; dz++)
                        {
                            int nx = s.X + dx, nz = s.Z + dz;
                            if (nx > 0 && nz > 0 && nx < w.Width - 1 && nz < w.Height - 1 && w.Map[nx, nz] == Terrain.Tree)
                            {
                                w.Map[nx, nz] = Terrain.Grass;
                                cleared = true;
                            }
                        }
                    if (cleared)
                        break;
                }
            }
        }

        /// Which tiles you can walk to from the town square.
        public static bool[,] Reachable(MainWorldLayout w)
        {
            var grid = w.ToGrid();
            var seen = new bool[w.Width, w.Height];
            var queue = new Queue<Tile>();
            queue.Enqueue(w.Spawn);
            seen[w.Spawn.X, w.Spawn.Z] = true;
            int[] ddx = { 1, -1, 0, 0, 1, 1, -1, -1 }, ddz = { 0, 0, 1, -1, 1, -1, 1, -1 };
            while (queue.Count > 0)
            {
                var t = queue.Dequeue();
                for (int i = 0; i < 8; i++)
                {
                    if (!grid.CanStep(t, ddx[i], ddz[i]))
                        continue;
                    var n = new Tile(t.X + ddx[i], t.Z + ddz[i]);
                    if (seen[n.X, n.Z])
                        continue;
                    seen[n.X, n.Z] = true;
                    queue.Enqueue(n);
                }
            }
            return seen;
        }

        /// Water tiles with reachable land straight next to them: where fishing spots can be.
        static void FindShores(MainWorldLayout w)
        {
            var reach = Reachable(w);
            var done = new bool[w.Width, w.Height];
            for (int x = 0; x < w.Width; x++)
                for (int z = 0; z < w.Height; z++)
                {
                    if (done[x, z] || w.Map[x, z] != Terrain.Water)
                        continue;
                    // Flood one pond, collecting its shore tiles.
                    var shore = new List<Tile>();
                    var queue = new Queue<Tile>();
                    queue.Enqueue(new Tile(x, z));
                    done[x, z] = true;
                    while (queue.Count > 0)
                    {
                        var t = queue.Dequeue();
                        bool isShore = false;
                        foreach (var n in new[] { new Tile(t.X + 1, t.Z), new Tile(t.X - 1, t.Z), new Tile(t.X, t.Z + 1), new Tile(t.X, t.Z - 1) })
                        {
                            if (!w.InBounds(n))
                                continue;
                            if (w.Map[n.X, n.Z] == Terrain.Water && !done[n.X, n.Z])
                            {
                                done[n.X, n.Z] = true;
                                queue.Enqueue(n);
                            }
                            else if (reach[n.X, n.Z])
                            {
                                isShore = true;
                            }
                        }
                        if (isShore)
                            shore.Add(t);
                    }
                    if (shore.Count > 0)
                        w.PondShores.Add(shore);
                }
        }

        static double Dist(Tile a, Tile b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
    }

    /// Smooth 2D noise (bilinear value noise) for where forests grow. Same seed, same noise.
    public sealed class ValueNoise
    {
        readonly double[] values = new double[256];
        readonly int[] perm = new int[512];

        public ValueNoise(int seed)
        {
            var rng = new Random(seed * 31 + 7);
            for (int i = 0; i < 256; i++)
            {
                values[i] = rng.NextDouble();
                perm[i] = i;
            }
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = perm[i]; perm[i] = perm[j]; perm[j] = t;
            }
            for (int i = 0; i < 256; i++)
                perm[256 + i] = perm[i];
        }

        double At(int x, int z) => values[perm[(perm[x & 255] + z) & 511] & 255];

        /// 0..1, smooth between whole-number points.
        public double Sample(double x, double z)
        {
            int x0 = (int)Math.Floor(x), z0 = (int)Math.Floor(z);
            double fx = x - x0, fz = z - z0;
            fx = fx * fx * (3 - 2 * fx);
            fz = fz * fz * (3 - 2 * fz);
            double a = At(x0, z0), b = At(x0 + 1, z0), c = At(x0, z0 + 1), d = At(x0 + 1, z0 + 1);
            return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fz;
        }
    }
}
