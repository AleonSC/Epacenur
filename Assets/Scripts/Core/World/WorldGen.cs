using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// What a tile of the world is.
    public enum Terrain : byte
    {
        Grass,
        Tree,   // a tree stands here
        Water,
        Wall,   // a building wall
        Floor,  // inside a building
        Door,
        Path,   // dirt road
        Camp,   // trampled ground in a goblin camp
        Rock,   // a rock with ore in it
        Flax,   // a plant to pick (fields in the countryside)
        Stone,  // paving round a shrine's altar
        Essence, // a rune essence boulder round a shrine
        Quarry, // the gravel floor of a quarry
    }

    public enum BuildingKind { Bank, Shop, Chapel, House, Smithy, Workshop }

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
        public bool HasFeature2;
        public Tile Feature2;   // a smithy's anvil (next to its furnace), a workshop's tanning rack (next to its spinning wheel)

        /// The same building moved by (dx, dz) tiles.
        public Building Moved(int dx, int dz) => new Building
        {
            Kind = Kind, Footprint = new Area(Footprint.X + dx, Footprint.Z + dz, Footprint.Width, Footprint.Height),
            Door = new Tile(Door.X + dx, Door.Z + dz), Feature = new Tile(Feature.X + dx, Feature.Z + dz),
            HasFeature2 = HasFeature2, Feature2 = new Tile(Feature2.X + dx, Feature2.Z + dz),
        };
    }

    /// A generated town: a map of tiles plus its buildings. Coordinates are local (0..Width-1), with
    /// Grass meaning "nothing built here" (the overworld fills that in).
    public sealed class TownLayout
    {
        public int Width, Height, Seed;
        public Terrain[,] Map;
        public Tile Spawn;                      // the town square
        public readonly List<Building> Buildings = new List<Building>();

        public bool InBounds(Tile t) => t.X >= 0 && t.Z >= 0 && t.X < Width && t.Z < Height;
        public static bool IsWalkable(Terrain t) => t == Terrain.Grass || t == Terrain.Floor || t == Terrain.Door || t == Terrain.Path || t == Terrain.Camp || t == Terrain.Stone || t == Terrain.Quarry;
    }

    /// Builds towns from a seed: a square in the middle with a bank, general store, chapel and houses
    /// round it, joined by dirt roads, with roads running out to the four edges. The same seed always
    /// gives the same town.
    public static class WorldGen
    {
        public static TownLayout GenerateTown(int size, int seed)
        {
            var rng = new Random(seed);
            var w = new TownLayout { Width = size, Height = size, Seed = seed, Map = new Terrain[size, size] };
            w.Spawn = new Tile(size / 2, size / 2);
            PlaceBuildings(w, rng);
            LayRoads(w);
            return w;
        }

        // ---- Town ----

        static void PlaceBuildings(TownLayout w, Random rng)
        {
            var kinds = new List<BuildingKind> { BuildingKind.Bank, BuildingKind.Shop, BuildingKind.Chapel, BuildingKind.Smithy, BuildingKind.Workshop };
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
        static Building MakeBuilding(TownLayout w, BuildingKind kind, Area a)
        {
            for (int x = a.X; x < a.XMax; x++)
                for (int z = a.Z; z < a.ZMax; z++)
                {
                    bool edge = x == a.X || z == a.Z || x == a.XMax - 1 || z == a.ZMax - 1;
                    w.Map[x, z] = edge ? Terrain.Wall : Terrain.Floor;
                }
            int dx = w.Spawn.X - a.Center.X, dz = w.Spawn.Z - a.Center.Z;
            Tile door, feature, feature2;
            if (Math.Abs(dx) >= Math.Abs(dz))
            {
                int doorX = dx > 0 ? a.XMax - 1 : a.X;
                int backX = dx > 0 ? a.X + 1 : a.XMax - 2;
                door = new Tile(doorX, a.Center.Z);
                feature = new Tile(backX, a.Center.Z);
                feature2 = new Tile(backX, a.Center.Z - 2);
            }
            else
            {
                int doorZ = dz > 0 ? a.ZMax - 1 : a.Z;
                int backZ = dz > 0 ? a.Z + 1 : a.ZMax - 2;
                door = new Tile(a.Center.X, doorZ);
                feature = new Tile(a.Center.X, backZ);
                feature2 = new Tile(a.Center.X + 2, backZ);
            }
            w.Map[door.X, door.Z] = Terrain.Door;
            // A smithy and a workshop each hold two things, side by side against the back wall.
            bool two = kind == BuildingKind.Smithy || kind == BuildingKind.Workshop;
            return new Building { Kind = kind, Footprint = a, Door = door, Feature = feature, HasFeature2 = two, Feature2 = feature2 };
        }

        /// Dirt roads: from each door to the square, and out from the square to the four edges (where
        /// they carry on as tracks through the countryside).
        static void LayRoads(TownLayout w)
        {
            foreach (var b in w.Buildings)
            {
                // Start just outside the door.
                var start = OutsideDoor(b);
                DigPath(w, start, w.Spawn);
            }
            DigPath(w, w.Spawn, new Tile(w.Spawn.X, 0));
            DigPath(w, w.Spawn, new Tile(w.Spawn.X, w.Height - 1));
            DigPath(w, w.Spawn, new Tile(0, w.Spawn.Z));
            DigPath(w, w.Spawn, new Tile(w.Width - 1, w.Spawn.Z));
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
        static void DigPath(TownLayout w, Tile from, Tile to)
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

        static void Mark(TownLayout w, Tile t)
        {
            if (w.Map[t.X, t.Z] == Terrain.Grass)
                w.Map[t.X, t.Z] = Terrain.Path;
        }
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
