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
        Bridge, // a wooden bridge over a river
        Stall,  // a market stall in a town square (which one: Spawning, by distance)
        Fence,  // round a farm's field
        Field,  // the tilled ground of a farm's field (its patches stand on it)
    }

    public enum BuildingKind { Bank, Shop, Chapel, House, Smithy, Workshop, Farm }

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
        public Tile Feature;    // inside, against the back wall: the bank booth, shop keeper or altar (houses: a table; a farm: its farmer)
        public bool HasFeature2;
        public Tile Feature2;   // a smithy's anvil (next to its furnace), a workshop's tanning rack (next to its spinning wheel)
        public List<FurniturePiece> Furniture = new List<FurniturePiece>();  // what else is inside (Furnisher)
        public Area Field;      // a farm: its fenced field beside the farmhouse (fence round the edge)
        public List<Tile> Gates = new List<Tile>();    // a farm: the gaps in the field's fence
        public List<Tile> Patches = new List<Tile>();  // a farm: its farming patches, in the field (even: herb, odd: allotment)

        /// What the building takes up: its walls, and a farm's field too.
        public Area Extent
        {
            get
            {
                if (Kind != BuildingKind.Farm)
                    return Footprint;
                int x0 = Math.Min(Footprint.X, Field.X), z0 = Math.Min(Footprint.Z, Field.Z);
                return new Area(x0, z0, Math.Max(Footprint.XMax, Field.XMax) - x0, Math.Max(Footprint.ZMax, Field.ZMax) - z0);
            }
        }

        /// The same building moved by (dx, dz) tiles.
        public Building Moved(int dx, int dz) => new Building
        {
            Kind = Kind, Footprint = new Area(Footprint.X + dx, Footprint.Z + dz, Footprint.Width, Footprint.Height),
            Door = new Tile(Door.X + dx, Door.Z + dz), Feature = new Tile(Feature.X + dx, Feature.Z + dz),
            HasFeature2 = HasFeature2, Feature2 = new Tile(Feature2.X + dx, Feature2.Z + dz),
            Furniture = Furniture.ConvertAll(f => f.Moved(dx, dz)),
            Field = new Area(Field.X + dx, Field.Z + dz, Field.Width, Field.Height),
            Gates = Gates.ConvertAll(t => new Tile(t.X + dx, t.Z + dz)),
            Patches = Patches.ConvertAll(t => new Tile(t.X + dx, t.Z + dz)),
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
        public readonly List<Tile> Lamps = new List<Tile>();  // lamp posts along the roads (they light up at night)
        public readonly List<Tile> Patches = new List<Tile>();  // the farm's farming patches (even: herb, odd: allotment)

        public bool InBounds(Tile t) => t.X >= 0 && t.Z >= 0 && t.X < Width && t.Z < Height;
        public static bool IsWalkable(Terrain t) => t == Terrain.Grass || t == Terrain.Floor || t == Terrain.Door || t == Terrain.Path || t == Terrain.Camp || t == Terrain.Stone || t == Terrain.Quarry || t == Terrain.Bridge || t == Terrain.Field;
    }

    /// Builds towns from a seed: a square in the middle with a bank, general store, chapel and houses
    /// round it, joined by dirt roads, with roads running out to the four edges. A city has more houses,
    /// spread wider. The same seed always gives the same town.
    public static class WorldGen
    {
        public static TownLayout GenerateTown(int size, int seed, bool city = false)
        {
            var rng = new Random(seed);
            var w = new TownLayout { Width = size, Height = size, Seed = seed, Map = new Terrain[size, size] };
            w.Spawn = new Tile(size / 2, size / 2);
            PlaceBuildings(w, rng, city);
            LayRoads(w);
            // Two market stalls at corners of the square (the square is paved, so you can always walk round them).
            int stalls = 0;
            foreach (var c in new[] { new Tile(w.Spawn.X - 3, w.Spawn.Z + 3), new Tile(w.Spawn.X + 3, w.Spawn.Z + 3),
                new Tile(w.Spawn.X - 3, w.Spawn.Z - 3), new Tile(w.Spawn.X + 3, w.Spawn.Z - 3) })
                if (stalls < 2 && (w.Map[c.X, c.Z] == Terrain.Grass || w.Map[c.X, c.Z] == Terrain.Path))
                {
                    w.Map[c.X, c.Z] = Terrain.Stall;
                    stalls++;
                }
            PlaceLamps(w);
            foreach (var b in w.Buildings)
                w.Patches.AddRange(b.Patches);
            foreach (var b in w.Buildings)
                b.Furniture = Furnisher.Furnish(b, seed);
            return w;
        }

        public const int MaxLamps = 12, LampSpacing = 7;

        /// Lamp posts on the grass beside the roads, the ones nearest the square first, at least LampSpacing
        /// tiles apart, never against a building.
        static void PlaceLamps(TownLayout w)
        {
            var spots = RoadsideSpots(w);
            foreach (var s in spots)
            {
                if (w.Lamps.Count >= MaxLamps)
                    break;
                bool clear = true;
                foreach (var l in w.Lamps)
                    if (l.StepsTo(s) < LampSpacing)
                        clear = false;
                if (clear)
                    w.Lamps.Add(s);
            }
        }

        /// A farm's field: Field x FieldDepth tiles inside its fence, patches in two rows with a path between.
        public const int FieldLength = 5, FieldDepth = 3, PatchesPerTown = 6;

        /// Grass tiles straight beside a road and not against a building, nearest the square first.
        static List<Tile> RoadsideSpots(TownLayout w)
        {
            var spots = new List<Tile>();
            for (int x = 1; x < w.Width - 1; x++)
                for (int z = 1; z < w.Height - 1; z++)
                {
                    if (w.Map[x, z] != Terrain.Grass)
                        continue;
                    bool byRoad = false, byBuilding = false;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            var n = w.Map[x + dx, z + dz];
                            if (n == Terrain.Wall || n == Terrain.Door || n == Terrain.Floor || n == Terrain.Stall || n == Terrain.Fence)
                                byBuilding = true;
                            if (n == Terrain.Path && (dx == 0 || dz == 0))
                                byRoad = true;
                        }
                    if (byRoad && !byBuilding)
                        spots.Add(new Tile(x, z));
                }
            spots.Sort((a, b) =>
            {
                int c = a.StepsTo(w.Spawn).CompareTo(b.StepsTo(w.Spawn));
                return c != 0 ? c : a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z);
            });
            return spots;
        }

        // ---- Town ----

        static void PlaceBuildings(TownLayout w, Random rng, bool city)
        {
            var kinds = new List<BuildingKind> { BuildingKind.Bank, BuildingKind.Shop, BuildingKind.Chapel, BuildingKind.Smithy, BuildingKind.Workshop, BuildingKind.Farm };
            int houses = city ? 8 + rng.Next(4) : 3 + rng.Next(3);
            for (int i = 0; i < houses; i++)
                kinds.Add(BuildingKind.House);

            var square = new Area(w.Spawn.X - 3, w.Spawn.Z - 3, 7, 7);  // kept open
            foreach (var kind in kinds)
            {
                for (int tries = 0; tries < 400; tries++)
                {
                    int bw = kind == BuildingKind.Chapel ? 7 : kind == BuildingKind.House || kind == BuildingKind.Farm ? 5 + rng.Next(3) : 7;
                    int bh = kind == BuildingKind.Chapel ? 8 : kind == BuildingKind.House || kind == BuildingKind.Farm ? 5 + rng.Next(2) : 6;
                    double angle = rng.NextDouble() * Math.PI * 2;
                    // The farm stands out on the edge of town, where there's room for its field.
                    double dist = kind == BuildingKind.Farm ? 15 + rng.NextDouble() * (city ? 10 : 6)
                        : 7 + rng.NextDouble() * (kind == BuildingKind.House ? (city ? 19 : 14) : 8);
                    int cx = w.Spawn.X + (int)Math.Round(Math.Cos(angle) * dist);
                    int cz = w.Spawn.Z + (int)Math.Round(Math.Sin(angle) * dist);
                    var area = new Area(cx - bw / 2, cz - bh / 2, bw, bh);
                    if (area.X < 3 || area.Z < 3 || area.XMax > w.Width - 3 || area.ZMax > w.Height - 3)
                        continue;
                    if (area.Overlaps(square, 1))
                        continue;
                    bool clash = false;
                    foreach (var other in w.Buildings)
                        if (area.Overlaps(other.Extent, 3))
                            clash = true;
                    if (clash)
                        continue;
                    var field = default(Area);
                    if (kind == BuildingKind.Farm && !FieldFor(w, area, square, rng, out field))
                        continue;
                    var b = MakeBuilding(w, kind, area);
                    if (kind == BuildingKind.Farm)
                        MakeField(w, b, field);
                    w.Buildings.Add(b);
                    break;
                }
            }
        }

        /// Where a farm's field can go: beside the farmhouse (a tile's gap between), not on the side facing the
        /// square (its door and road are there), clear of the square and the other buildings and their fields.
        static bool FieldFor(TownLayout w, Area house, Area square, Random rng, out Area field)
        {
            int outerLong = FieldLength + 2, outerDeep = FieldDepth + 2;
            var c = house.Center;
            var sides = new List<Area>
            {
                new Area(c.X - outerLong / 2, house.Z - 1 - outerDeep, outerLong, outerDeep),
                new Area(c.X - outerLong / 2, house.ZMax + 1, outerLong, outerDeep),
                new Area(house.X - 1 - outerDeep, c.Z - outerLong / 2, outerDeep, outerLong),
                new Area(house.XMax + 1, c.Z - outerLong / 2, outerDeep, outerLong),
            };
            for (int i = sides.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = sides[i];
                sides[i] = sides[j];
                sides[j] = t;
            }
            int dx = w.Spawn.X - c.X, dz = w.Spawn.Z - c.Z;
            foreach (var f in sides)
            {
                // Not between the house and the square.
                int fx = f.Center.X - c.X, fz = f.Center.Z - c.Z;
                if (fx * dx + fz * dz > 0)
                    continue;
                if (f.X < 3 || f.Z < 3 || f.XMax > w.Width - 3 || f.ZMax > w.Height - 3 || f.Overlaps(square, 2))
                    continue;
                bool clash = false;
                foreach (var other in w.Buildings)
                    if (f.Overlaps(other.Extent, 3))
                        clash = true;
                if (clash)
                    continue;
                field = f;
                return true;
            }
            field = default(Area);
            return false;
        }

        /// Marks out a farm's field: a fence round it with a gate at each end of the path down its middle, and
        /// two rows of patches either side of the path (alternately herb patches and allotments).
        static void MakeField(TownLayout w, Building b, Area f)
        {
            b.Field = f;
            bool alongX = f.Width > f.Height;
            for (int x = f.X; x < f.XMax; x++)
                for (int z = f.Z; z < f.ZMax; z++)
                {
                    bool edge = x == f.X || z == f.Z || x == f.XMax - 1 || z == f.ZMax - 1;
                    w.Map[x, z] = edge ? Terrain.Fence : Terrain.Field;
                }
            // The field's own frame: `i` along its length (0..FieldLength-1), `j` across it (0..FieldDepth-1).
            Func<int, int, Tile> at = (i, j) => alongX ? new Tile(f.X + 1 + i, f.Z + 1 + j) : new Tile(f.X + 1 + j, f.Z + 1 + i);
            int mid = FieldDepth / 2;
            foreach (var gate in new[] { alongX ? new Tile(f.X, f.Z + 1 + mid) : new Tile(f.X + 1 + mid, f.Z),
                alongX ? new Tile(f.XMax - 1, f.Z + 1 + mid) : new Tile(f.X + 1 + mid, f.ZMax - 1) })
            {
                w.Map[gate.X, gate.Z] = Terrain.Field;
                b.Gates.Add(gate);
            }
            for (int i = 0; i < FieldLength; i += 2)
                foreach (int j in new[] { 0, FieldDepth - 1 })
                    b.Patches.Add(at(i, j));
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
                    g.SetBlocked(new Tile(x, z), t == Terrain.Wall || t == Terrain.Floor || t == Terrain.Door || t == Terrain.Fence || t == Terrain.Field);
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

    /// Smooth 2D value noise. The value at each whole-number point comes from hashing the point with the
    /// seed, so it never repeats however far out you go. Same seed, same noise.
    public sealed class ValueNoise
    {
        readonly ulong seed;

        public ValueNoise(int seed)
        {
            unchecked { this.seed = ((ulong)(uint)seed + 0x632BE59BD9B4E019UL) * 0x9E3779B97F4A7C15UL; }
        }

        /// The value at a whole-number point (0..1).
        public double At(int x, int z)
        {
            unchecked
            {
                ulong h = seed ^ ((ulong)(uint)x * 0xBF58476D1CE4E5B9UL);
                h = (h ^ (h >> 31)) * 0x94D049BB133111EBUL;
                h ^= (ulong)(uint)z * 0xD6E8FEB86659FD93UL;
                h = (h ^ (h >> 29)) * 0xBF58476D1CE4E5B9UL;
                h ^= h >> 32;
                return (h & 0xFFFFFF) / (double)0x1000000;
            }
        }

        /// 0..1, smooth between whole-number points.
        public double Sample(double x, double z)
        {
            int x0 = (int)Math.Floor(x), z0 = (int)Math.Floor(z);
            double fx = x - x0, fz = z - z0;
            fx = fx * fx * fx * (fx * (fx * 6 - 15) + 10);  // smoother than smoothstep: no creases at the lattice
            fz = fz * fz * fz * (fz * (fz * 6 - 15) + 10);
            double a = At(x0, z0), b = At(x0 + 1, z0), c = At(x0, z0 + 1), d = At(x0 + 1, z0 + 1);
            return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fz;
        }

        /// Several octaves of Sample, each twice as fine and half as strong, so shapes have detail at every
        /// size. Stretched back out to spread like a single octave (adding octaves pulls values in to 0.5),
        /// so the same thresholds give about as much forest, water and so on. 0..1.
        public double Fractal(double x, double z, int octaves = 4)
        {
            double sum = 0, amp = 1, norm = 0, f = 1;
            for (int o = 0; o < octaves; o++)
            {
                sum += Sample(x * f + o * 17.31, z * f - o * 29.17) * amp;
                norm += amp;
                amp *= 0.5;
                f *= 2.03;
            }
            double v = 0.5 + (sum / norm - 0.5) * Spread(octaves);
            return v < 0 ? 0 : v > 1 ? 1 : v;
        }

        static double Spread(int octaves) => octaves <= 1 ? 1 : octaves == 2 ? 1.35 : octaves == 3 ? 1.53 : 1.63;
    }
}
