using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    public enum RegionKind { Wild, Town, Camp, Shrine, Quarry, Course }

    /// What's planned for one region of the overworld: a town, a goblin camp, a shrine, a quarry, or just countryside.
    /// Everything is in world tiles.
    public sealed class RegionPlan
    {
        public int RX, RZ;
        public RegionKind Kind;
        public Area Bounds;
        public Tile Center;
        public TownLayout Town;                              // towns: the built-up tiles (local to Bounds)
        public readonly List<Building> Buildings = new List<Building>();
        public readonly List<Tile> Lamps = new List<Tile>();  // towns: lamp posts
        public Area Camp;                                    // camps: the trampled ground; shrines: the paving round the altar; quarries: the pit
        public readonly List<Tile> Tents = new List<Tile>();
        public Area Level;                                   // towns, camps, shrines, quarries: kept flat (hills rise beyond it)
        public Tile Altar;                                   // shrines: where the altar stands
        public readonly List<Tile> Stones = new List<Tile>(); // shrines: the standing stones round it
        public Tile CourseCorner;                            // agility courses: the south-west corner of the loop
        public List<ObstaclePlacement> Obstacles;            // agility courses: where each obstacle goes
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
            if (Kind == RegionKind.Course && AgilityCourse.OnLoop(CourseCorner, t) && !Blocked.Contains(t))
                return Terrain.Path;
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
                return t.StepsTo(Center) <= Overworld.TownClearRadius;
            if (Kind == RegionKind.Course)
                return Level.Contains(t);
            if (Kind == RegionKind.Camp || Kind == RegionKind.Shrine || Kind == RegionKind.Quarry)
                return t.X >= Camp.X - 3 && t.X < Camp.XMax + 3 && t.Z >= Camp.Z - 3 && t.Z < Camp.ZMax + 3;
            return false;
        }
    }

    /// The endless overworld, worked out from a seed: the same tile always comes out the same, so it can be
    /// built a chunk at a time as you walk. It's split into square regions; each is a town, a goblin camp,
    /// a shrine, a quarry or wild countryside (the one you start in is always a town). Over that lies the countryside itself:
    /// forests, rocky ground and flax fields where smooth noises are high, and ponds.
    public sealed class Overworld
    {
        public const int RegionSize = 64, ChunkSize = 16;
        public const int TownClearRadius = 15;  // no trees or ponds this close to a town square
        public readonly int Seed;
        readonly ValueNoise forest, ponds, rocky, fields, hills, bumps, ranges;
        readonly Dictionary<long, RegionPlan> plans = new Dictionary<long, RegionPlan>();

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
            // The start is a town; just east of it, a shrine (an air altar to start runecrafting); just west, an agility course.
            plan.Kind = rx == 0 && rz == 0 ? RegionKind.Town : rx == 1 && rz == 0 ? RegionKind.Shrine : rx == -1 && rz == 0 ? RegionKind.Course
                : roll < 0.12 ? RegionKind.Town : roll < 0.32 ? RegionKind.Camp : roll < 0.4 ? RegionKind.Shrine
                : roll < 0.48 ? RegionKind.Quarry : roll < 0.54 ? RegionKind.Course : RegionKind.Wild;
            if (plan.Kind == RegionKind.Town)
                PlanTown(plan);
            else if (plan.Kind == RegionKind.Camp)
                PlanCamp(plan);
            else if (plan.Kind == RegionKind.Shrine)
                PlanShrine(plan);
            else if (plan.Kind == RegionKind.Course)
                PlanCourse(plan);
            else if (plan.Kind == RegionKind.Quarry)
                PlanQuarry(plan);
            plans[key] = plan;
            return plan;
        }

        void PlanTown(RegionPlan plan)
        {
            int seed = (int)(Hash(plan.RX, plan.RZ, 2) * int.MaxValue);
            plan.Town = WorldGen.GenerateTown(RegionSize, seed);
            int x0 = plan.Center.X - TownClearRadius, z0 = plan.Center.Z - TownClearRadius, x1 = plan.Center.X + TownClearRadius, z1 = plan.Center.Z + TownClearRadius;
            for (int x = 0; x < RegionSize; x++)
                for (int z = 0; z < RegionSize; z++)
                    if (plan.Town.Map[x, z] != Terrain.Grass)
                    {
                        x0 = Math.Min(x0, plan.Bounds.X + x); x1 = Math.Max(x1, plan.Bounds.X + x);
                        z0 = Math.Min(z0, plan.Bounds.Z + z); z1 = Math.Max(z1, plan.Bounds.Z + z);
                    }
            plan.Level = new Area(x0 - 2, z0 - 2, x1 - x0 + 5, z1 - z0 + 5);
            foreach (var l in plan.Town.Lamps)
            {
                var lamp = new Tile(plan.Bounds.X + l.X, plan.Bounds.Z + l.Z);
                plan.Lamps.Add(lamp);
                plan.Blocked.Add(lamp);
            }
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

        /// A loop of path with five obstacles along it (which course: Spawning, by distance).
        void PlanCourse(RegionPlan plan)
        {
            int dx = (int)(Hash(plan.RX, plan.RZ, 7) * 16) - 8, dz = (int)(Hash(plan.RX, plan.RZ, 8) * 16) - 8;
            plan.CourseCorner = new Tile(plan.Center.X + dx - AgilityCourse.Width / 2, plan.Center.Z + dz - AgilityCourse.Depth / 2);
            plan.Obstacles = AgilityCourse.Layout(plan.CourseCorner);
            foreach (var o in plan.Obstacles)
                foreach (var t in o.Covered)
                    plan.Blocked.Add(t);
            plan.Level = new Area(plan.CourseCorner.X - 3, plan.CourseCorner.Z - 3, AgilityCourse.Width + 6, AgilityCourse.Depth + 6);
        }

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
            // Down to the water: nothing near a pond is raised (ponds form where their noise passes 0.82,
            // and only in low country: see IsPond).
            double h = Hills(x, z);
            double dry = Math.Max(Smooth((0.8 - ponds.Sample(x / 16.0, z / 16.0)) / 0.5), Smooth((h - PondMaxHills) / 4));
            return h * (1 - flat) * dry;
        }

        /// The hills before anything is levelled.
        double Hills(double x, double z)
        {
            double n = (hills.Sample(x / 48.0, z / 48.0) - 0.42) / 0.58;
            double h = n <= 0 ? 0 : n * n * (3 - 2 * n) * HillHeight * (0.6 + 1.2 * ranges.Sample(x / 220.0, z / 220.0));
            return h + bumps.Sample(x / 10.0, z / 10.0) * BumpHeight;
        }

        const double PondMaxHills = 2.5;  // no ponds up in the hills

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

        /// What a tile is.
        public Terrain At(Tile t)
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

        /// Ponds: blobs where a smooth noise is high, away from towns, camps and roads. Dry specks inside a pond
        /// (land with water on most sides) are filled in, so no land is cut off.
        bool IsPond(Tile t)
        {
            var plan = PlanAt(t);
            if (plan.KeepsClear(t) || plan.At(t) != Terrain.Grass || Hills(t.X, t.Z) > PondMaxHills)
                return false;
            // Roads stay dry with a margin (a road can run diagonally, which water either side would cut).
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var n = new Tile(t.X + dx, t.Z + dz);
                    if (PlanAt(n).At(n) != Terrain.Grass)
                        return false;
                }
            if (RawPond(t.X, t.Z))
                return true;
            int wet = 0;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if (RawPond(t.X + dx, t.Z + dz))
                        wet++;
            return wet >= 5;
        }

        bool RawPond(int x, int z) => ponds.Sample(x / 16.0, z / 16.0) > 0.82;

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
            double roll = Hash(t.X, t.Z, 7);
            if (rocky.Sample(t.X / 20.0, t.Z / 20.0) > 0.74)
                return roll < 0.5 ? Terrain.Rock : Terrain.Grass;
            double n0 = forest.Sample(t.X / 14.0, t.Z / 14.0);
            // Flax fields in open country (not in forests).
            if (n0 < 0.42 && fields.Sample(t.X / 12.0, t.Z / 12.0) > 0.7)
                return roll < 0.75 ? Terrain.Flax : Terrain.Grass;
            double chance = n0 > 0.58 ? 0.85 : n0 > 0.42 ? 0.3 : 0.04;
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
}
