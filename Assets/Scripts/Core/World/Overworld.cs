using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// Dungeon is never a region's kind: monsters use it for "underground, in a dungeon" (NpcSpawnRule.Region).
    public enum RegionKind { Wild, Town, Camp, Shrine, Quarry, Course, Cave, Dungeon }

    /// The kind of country a tile is in: what grows there, how wet it is, the colour of the ground.
    public enum Biome { Meadow, Woodland, Marsh, Highlands, Wasteland }

    /// What covers the ground on a tile, besides its biome's own grass: earth and rock on steep slopes and high up, snow on the peaks.
    public enum Ground { Grass, Earth, Rock, Snow }

    /// A town or city, and the safe ground round it. Further out than SafeRadius, the wilderness begins.
    public sealed class Settlement
    {
        public int RX, RZ;                // its region
        public bool City;
        public Tile Center;               // the square
        public int SafeRadius;            // tiles from the square that are safe
    }

    /// What's planned for one region of the overworld: a town, a goblin camp, a shrine, a quarry, an agility course,
    /// or just countryside.
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
        public readonly List<Tile> Lamps = new List<Tile>();  // towns: lamp posts
        public readonly List<Tile> Patches = new List<Tile>();  // towns: the farm's farming patches (even: herb, odd: allotment)
        public Area Camp;                                    // camps: the trampled ground; shrines: the paving round the altar; quarries: the pit
        public readonly List<Tile> Tents = new List<Tile>();
        public Area Level;
        public double LevelHeight; public bool HasLevelHeight;  // the height it's levelled at (Overworld.LevelHeight)                                   // towns, camps, shrines, quarries, courses: kept flat (hills rise beyond it)
        public Tile Altar;                                   // shrines: where the altar stands
        public readonly List<Tile> Stones = new List<Tile>(); // shrines: the standing stones round it
        public Tile CaveMouth;                               // caves: the way into the dungeon
        public Tile CourseCorner;                            // agility courses: the south-west corner of the loop
        public List<ObstaclePlacement> Obstacles;            // agility courses: where each obstacle goes
        public readonly HashSet<Tile> Essence = new HashSet<Tile>();  // shrines: rune essence boulders round the paving
        public readonly HashSet<Tile> Rocks = new HashSet<Tile>();    // quarries: the ore rocks in the pit
        public readonly List<Tile> Rubble = new List<Tile>();         // quarries: heaps of spoil in the pit's corners
        public Tile Cart;                                    // quarries: a mine cart full of ore
        public bool HasTradingPost; public Tile TradingPost;  // cities: the trading post's clerk, on the edge of the square
        public bool HasWell; public Tile Well;               // towns: the well
        public readonly List<Signpost> Signposts = new List<Signpost>();  // towns: where the roads out lead
        public Area Walls; public bool StoneWalls;           // towns: the ring of town walls (none if it's empty)
        public readonly List<Tile> Gates = new List<Tile>();  // towns: where roads go through the walls
        public LandmarkKind Landmark; public Area LandmarkArea;  // wilds: a ruin or landmark, and where it stands
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
            if (Kind == RegionKind.Cave && Camp.Contains(t) && !Blocked.Contains(t))
                return Terrain.Camp;
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
            if (Kind == RegionKind.Course || Kind == RegionKind.Cave)
                return Level.Contains(t);
            if (Kind == RegionKind.Camp || Kind == RegionKind.Shrine || Kind == RegionKind.Quarry)
                return t.X >= Camp.X - 3 && t.X < Camp.XMax + 3 && t.Z >= Camp.Z - 3 && t.Z < Camp.ZMax + 3;
            return Overworld.NearLandmark(this, t);
        }
    }

    /// The endless overworld, worked out from a seed: the same tile always comes out the same, so it can be
    /// built a chunk at a time as you walk. It's split into square regions. Towns and cities lie far apart
    /// (one at most in each cell of 5 x 5 regions, the start always a city); round each is a ring of safe
    /// ground, and beyond it the wilderness, more dangerous the further you are from any town (Danger),
    /// until you near the next. Other regions are goblin camps, shrines, quarries or wild countryside.
    /// Over that lies the countryside itself, in biomes (meadow, woodland, marsh, highlands, and wasteland
    /// deep in the wilderness): forests and flax fields where smooth noises are high, and ponds. Ore is only found in quarries.
    public sealed partial class Overworld
    {
        public const int RegionSize = 64, ChunkSize = 16;
        public const int TownClearRadius = 15, CityClearRadius = 22;  // no trees or ponds this close to the square
        public const int SettlementCell = 5;                             // regions across a settlement's cell
        public const int TownSafeRadius = 48, CitySafeRadius = 80;
        public const double TilesPerLevel = 6;                           // a wilderness level every this many tiles
        public const int MaxDanger = 60;
        public const int DeepWilderness = 24;                            // from this level the land is wasteland
        public readonly int Seed;
        readonly ValueNoise forest, ponds, fields, hills, bumps, ranges, moisture, warp, edge, ridges, basin, rivers, knolls;
        readonly Dictionary<long, RegionPlan> plans = new Dictionary<long, RegionPlan>();
        readonly Dictionary<long, Settlement> settlements = new Dictionary<long, Settlement>();
        // Slow-changing fields are worked out on a coarse lattice and blended between its points.
        readonly CoarseField moistureField, dangerField, baseField, riverField, hillsField, riverAllowField;
        readonly Dictionary<long, double> pondCorners = new Dictionary<long, double>();

        public Overworld(int seed)
        {
            Seed = seed;
            forest = new ValueNoise(seed);
            ponds = new ValueNoise(seed + 101);
            fields = new ValueNoise(seed + 303);
            hills = new ValueNoise(seed + 404);
            bumps = new ValueNoise(seed + 505);
            ranges = new ValueNoise(seed + 606);
            moisture = new ValueNoise(seed + 707);
            warp = new ValueNoise(seed + 808);
            edge = new ValueNoise(seed + 909);
            ridges = new ValueNoise(seed + 1010);
            basin = new ValueNoise(seed + 1111);
            rivers = new ValueNoise(seed + 1212);
            knolls = new ValueNoise(seed + 1313);
            moistureField = new CoarseField(8, ExactMoisture);
            dangerField = new CoarseField(4, ExactDanger);
            baseField = new CoarseField(8, ExactBase);
            riverField = new CoarseField(4, ExactRiver);
            hillsField = new CoarseField(2, Hills);
            riverAllowField = new CoarseField(8, ExactRiverAllowed);  // the hills as biomes see them: close enough, and far cheaper
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
            // (an air altar to start runecrafting), just west an agility course; elsewhere camps, shrines, quarries,
            // courses and wild country.
            var settlement = SettlementOf(rx, rz);
            bool here = settlement != null && settlement.RX == rx && settlement.RZ == rz;
            bool starter = (rz == 0 && (rx == 1 || rx == -1)) || (rx == 0 && (rz == 1 || rz == -1) && !here);
            plan.City = here && settlement.City;
            // Round the start: a shrine to the east, an agility course to the west and a quarry (copper and tin) to the north.
            // Quarries are the only places with ore, so there are more of them than of the rest.
            plan.Kind = here ? RegionKind.Town : rx == 1 && rz == 0 ? RegionKind.Shrine : rx == -1 && rz == 0 ? RegionKind.Course
                : rx == 0 && rz == 1 ? RegionKind.Quarry : rx == 0 && rz == -1 ? RegionKind.Cave
                : roll < 0.22 ? RegionKind.Camp : roll < 0.31 ? RegionKind.Shrine : roll < 0.44 ? RegionKind.Quarry : roll < 0.5 ? RegionKind.Course
                : RegionKind.Wild;
            // Caves up in the mountains: a mouth into a dungeon.
            if (plan.Kind == RegionKind.Wild && !here && hillsField.At(plan.Center.X, plan.Center.Z) > 7 && Hash(rx, rz, 9) < 0.45)
                plan.Kind = RegionKind.Cave;
            // Not on a river: camps, shrines, quarries, courses and caves keep well clear of them.
            if (!here && !starter && plan.Kind != RegionKind.Wild && RiverNear(plan.Center))
                plan.Kind = RegionKind.Wild;
            // Not in a lake: a camp, shrine, quarry or course needs dry ground under its middle. (Ponds have no pits,
            // so dry ground there always runs on out of the lake, and its ground is kept clear of water.)
            if (!here && !starter && plan.Kind != RegionKind.Wild && PondNoise(plan.Center.X, plan.Center.Z) > PondLevel(plan.Center.X, plan.Center.Z) - 0.1)
                plan.Kind = RegionKind.Wild;
            // Not on a road between towns.
            if (!here && !starter && plan.Kind != RegionKind.Wild && RoadDistance(plan.Center.X, plan.Center.Z) < 30)
                plan.Kind = RegionKind.Wild;
            if (plan.Kind == RegionKind.Town)
                PlanTown(plan);
            else if (plan.Kind == RegionKind.Camp)
                PlanCamp(plan);
            else if (plan.Kind == RegionKind.Shrine)
                PlanShrine(plan);
            else if (plan.Kind == RegionKind.Course)
                PlanCourse(plan);
            else if (plan.Kind == RegionKind.Cave)
                PlanCave(plan);
            else if (plan.Kind == RegionKind.Quarry)
                PlanQuarry(plan);
            else if (plan.Kind == RegionKind.Wild)
                PlanLandmark(plan);
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
            if (plan.City)
            {
                plan.HasTradingPost = true;
                plan.TradingPost = new Tile(plan.Center.X + 2, plan.Center.Z + 2);
                plan.Blocked.Add(plan.TradingPost);
            }
            foreach (var fp in plan.Town.Patches)
            {
                var patch = new Tile(plan.Bounds.X + fp.X, plan.Bounds.Z + fp.Z);
                plan.Patches.Add(patch);
                plan.Blocked.Add(patch);
            }
            foreach (var l in plan.Town.Lamps)
            {
                var lamp = new Tile(plan.Bounds.X + l.X, plan.Bounds.Z + l.Z);
                plan.Lamps.Add(lamp);
                plan.Blocked.Add(lamp);
            }
            if (plan.Town.HasWell)
            {
                plan.HasWell = true;
                plan.Well = new Tile(plan.Bounds.X + plan.Town.Well.X, plan.Bounds.Z + plan.Town.Well.Z);
                plan.Blocked.Add(plan.Well);
            }
            foreach (var b in plan.Town.Buildings)
            {
                var moved = b.Moved(plan.Bounds.X, plan.Bounds.Z);
                plan.Buildings.Add(moved);
                plan.Blocked.Add(moved.Feature);
                if (moved.HasFeature2)
                    plan.Blocked.Add(moved.Feature2);
                foreach (var f in moved.Furniture)
                    foreach (var t in f.Tiles)
                        plan.Blocked.Add(t);
            }
            PlanWalls(plan);
            PlanSignposts(plan);
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

        /// A mine head: a gravel yard with the shaft in the middle (a winch over a hole: climb down to the mine,
        /// Mines), heaps of spoil in two corners and a mine cart beside the shaft. No ore up here.
        void PlanQuarry(RegionPlan plan)
        {
            int dx = (int)(Hash(plan.RX, plan.RZ, 3) * 16) - 8, dz = (int)(Hash(plan.RX, plan.RZ, 4) * 16) - 8;
            var q = plan.Camp = new Area(plan.Center.X + dx - 4, plan.Center.Z + dz - 4, 9, 8);
            plan.CaveMouth = new Tile(q.X + 4, q.Z + 4);
            plan.Blocked.Add(plan.CaveMouth);
            foreach (var corner in new[] { new Tile(q.X, q.ZMax - 1), new Tile(q.XMax - 1, q.ZMax - 1) })
            {
                plan.Rubble.Add(corner);
                plan.Blocked.Add(corner);
            }
            plan.Cart = new Tile(plan.CaveMouth.X + 2, plan.CaveMouth.Z);
            plan.Blocked.Add(plan.Cart);
            plan.Level = new Area(q.X - 3, q.Z - 3, q.Width + 6, q.Height + 6);
        }

        /// A cave mouth (a blocked tile you enter by from the south) on a patch of trampled ground.
        void PlanCave(RegionPlan plan)
        {
            int dx = (int)(Hash(plan.RX, plan.RZ, 10) * 16) - 8, dz = (int)(Hash(plan.RX, plan.RZ, 11) * 16) - 8;
            plan.CaveMouth = new Tile(plan.Center.X + dx, plan.Center.Z + dz);
            plan.Blocked.Add(plan.CaveMouth);
            foreach (var side in new[] { new Tile(-1, 0), new Tile(1, 0), new Tile(-1, 1), new Tile(0, 1), new Tile(1, 1) })
                plan.Blocked.Add(new Tile(plan.CaveMouth.X + side.X, plan.CaveMouth.Z + side.Z));  // the rocks round it
            plan.Camp = new Area(plan.CaveMouth.X - 3, plan.CaveMouth.Z - 4, 7, 6);
            plan.Level = new Area(plan.CaveMouth.X - 5, plan.CaveMouth.Z - 6, 11, 10);
        }

        /// Where you stand to go into a cave: just south of its mouth.
        public static Tile CaveApproach(RegionPlan plan) => new Tile(plan.CaveMouth.X, plan.CaveMouth.Z - 1);

        /// The dungeon under a cave, or the mine down a mine shaft: its layout (the same every time) and how
        /// dangerous it is (the wilderness level at its mouth).
        public DungeonLayout Dungeon(RegionPlan plan, out int danger)
        {
            danger = Danger(plan.CaveMouth);
            int seed = (int)(Hash(plan.RX, plan.RZ, 81) * int.MaxValue);
            return plan.Kind == RegionKind.Quarry ? Mines.Generate(seed, Mines.TierOf(this, plan)) : Dungeons.Generate(seed);
        }

        /// A loop of path with five obstacles along it (which course: Spawning, by wilderness level).
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
        public Biome BiomeAt(Tile t) => BiomeFor(Moisture(t.X, t.Z), hillsField.At(t.X, t.Z), DangerAt(t.X, t.Z));

        static Biome BiomeFor(double wet, double height, double danger)
        {
            if (danger >= DeepWilderness && wet < 0.62)
                return Biome.Wasteland;
            if (height > 11)
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

        public const double MountainHeight = 30, RollingHeight = 12, MidHeight = 3.5, BaseHeight = 16, BumpHeight = 1.2;
        public const double WaterDepth = 0.4;     // how far a pond or river's bed lies under its surface
        public const double SnowLine = 38, RockLine = 27;
        const double LevelBlend = 20;              // tiles from levelled ground to the full lie of the land

        /// How high the ground is (world units) at a point in tile coordinates (a tile's centre is a whole
        /// number): the slow base (broad valleys and uplands), with hills and mountains on top (Hills), carved
        /// down along rivers and under ponds. Towns, camps, shrines, quarries and courses are levelled at the
        /// height the land had at their middle, blending into the hills round them.
        public double Elevation(double x, double z)
        {
            // Points on the half-tile lattice (tile centres and corners: the ground mesh, the maps, the game) are
            // asked for again and again: remember them.
            double hx = x * 2, hz = z * 2;
            if (hx == Math.Floor(hx) && hz == Math.Floor(hz) && Math.Abs(hx) < int.MaxValue && Math.Abs(hz) < int.MaxValue)
            {
                long key = ChunkedGrid.Key((int)hx, (int)hz);
                if (elevations.TryGetValue(key, out double known))
                    return known;
                if (elevations.Count > 1 << 20)
                    elevations.Clear();
                return elevations[key] = WorkOutElevation(x, z);
            }
            return WorkOutElevation(x, z);
        }

        readonly Dictionary<long, double> elevations = new Dictionary<long, double>();

        double WorkOutElevation(double x, double z)
        {
            var best = Levelling(x, z, out double flat, out double nearest, out double levelAt);
            bool town = best != null && best.Kind == RegionKind.Town;
            if (flat >= 1 && !town)
                return levelAt;
            double natural = Natural(x, z, nearest, out double depth);
            double h = (natural - depth) * (1 - flat) + levelAt * flat;
            if (town && flat > 0)
            {
                // Towns aren't flat: their ground rises and falls gently with the land, each building on a level
                // pad of its own (and the square too).
                h += TownRelief(natural - levelAt) * flat;
                h = Pads(best, x, z, h);
            }
            return h;
        }

        /// How much of the land's rise and fall shows in a town: a share of it, never more than a few units.
        static double TownRelief(double rise)
        {
            double r = rise * 0.45;
            return 5 * Math.Tanh(r / 5);
        }

        /// The town's own ground at a point, before the pads (where a building's pad takes its height from).
        double TownGround(RegionPlan plan, double x, double z)
        {
            double level = LevelHeight(plan);
            return level + TownRelief(Natural(x, z, 0, out _) - level);
        }

        readonly Dictionary<Building, double> padHeights = new Dictionary<Building, double>();
        const double PadBlend = 3;  // tiles from a pad's edge to the town's own ground

        /// The height a building's floor is levelled at: the town's ground at its middle.
        public double PadHeight(RegionPlan plan, Building b)
        {
            if (!padHeights.TryGetValue(b, out double h))
            {
                var a = b.Footprint;
                padHeights[b] = h = TownGround(plan, a.X + (a.Width - 1) / 2.0, a.Z + (a.Height - 1) / 2.0);
            }
            return h;
        }

        /// The town square's height: the town's ground at its middle.
        public double SquareHeight(RegionPlan plan) => TownGround(plan, plan.Center.X, plan.Center.Z);

        /// Pulls a town's ground level round its buildings (a tile beyond their walls) and its square.
        double Pads(RegionPlan plan, double x, double z, double h)
        {
            double weights = 0, sum = 0, most = 0;
            foreach (var b in plan.Buildings)
            {
                var fp = b.Footprint;
                if (x > fp.X - 1.5 && x < fp.XMax + 0.5 && z > fp.Z - 1.5 && z < fp.ZMax + 0.5)
                    return PadHeight(plan, b);  // on the pad itself: exactly level
            }
            foreach (var b in plan.Buildings)
                Pad(new Area(b.Footprint.X - 1, b.Footprint.Z - 1, b.Footprint.Width + 2, b.Footprint.Height + 2), PadHeight(plan, b), x, z,
                    ref weights, ref sum, ref most);
            Pad(new Area(plan.Center.X - 4, plan.Center.Z - 4, 9, 9), SquareHeight(plan), x, z, ref weights, ref sum, ref most);
            if (weights <= 0)
                return h;
            return h + (sum / weights - h) * most;
        }

        static void Pad(Area a, double height, double x, double z, ref double weights, ref double sum, ref double most)
        {
            double ox = Math.Max(0, Math.Max(a.X - 0.5 - x, x - (a.XMax - 0.5)));
            double oz = Math.Max(0, Math.Max(a.Z - 0.5 - z, z - (a.ZMax - 0.5)));
            double f = 1 - Smooth(Math.Sqrt(ox * ox + oz * oz) / PadBlend);
            if (f <= 0)
                return;
            double w = f * f / (1.000001 - f);  // on a pad, its own height wins outright
            weights += w;
            sum += w * height;
            most = Math.Max(most, f);
        }

        public double Elevation(Tile t) => Elevation(t.X, t.Z);

        /// The height things stand at on a tile: the water's surface on a pond or river (fishing spots float on
        /// it), the deck of a bridge, otherwise the ground.
        public double SurfaceHeight(Tile t)
        {
            var k = At(t);
            if (k == Terrain.Water)
                return WaterSurface(t.X, t.Z);
            if (k == Terrain.Bridge)
                return WaterSurface(t.X, t.Z) + BridgeDeck;
            return Elevation(t);
        }

        public const double BridgeDeck = 0.22;

        /// Where a pond's or river's surface lies: the slow base height (water fills the valley floors).
        public double WaterSurface(double x, double z) => Base(x, z) + 0.04;

        /// The lie of the land before anything is levelled, and how far below it a pond or river's bed is.
        double Natural(double x, double z, double nearestLevel, out double depth)
        {
            double h = Hills(x, z);
            // Ponds: nothing near one is raised (ponds form where their noise passes PondLevel; the hills hold it
            // down, so there are few up in them).
            double margin = PondLevel(x, z) - PondNoise(x, z);
            double dry = Smooth((margin - 0.02) / 0.7);
            // Rivers carve valleys: the hills fall away to nothing at the water's edge. (No rivers near levelled ground.)
            double dist = RiverDistance(x, z, out double strength);
            double allow = RiverAllowed(x, z);
            double river = allow * strength;
            double width = RiverWidth * river;
            // The valley deepens gradually away from towns (water only runs where it's carved all the way down).
            double valley = 1 - Smooth(allow / RiverFull) * (1 - Smooth((ValleyDistance(x, z) - 2 * width) / RiverValley));  // fully carved all across the water
            double wetPond = 1 - Smooth((margin + 0.05) / 0.1);
            double wetRiver = Smooth(river / RiverFull) * (1 - Smooth(dist / (width + 0.5)));
            depth = WaterDepth * Math.Max(wetPond, wetRiver);
            return Base(x, z) + h * dry * valley;
        }

        /// Broad valleys and uplands: a very slow noise, so lakes and rivers on it are all but level.
        double Base(double x, double z) => baseField.At(x, z);

        double ExactBase(double x, double z) => basin.Fractal(x / 520.0, z / 520.0, 2) * BaseHeight;

        /// The hills and mountains before anything is levelled or carved: big rolling hills everywhere (layered and
        /// warped, so they wind into valleys and spurs), knolls and hollows on top of them, ridged mountain ranges
        /// with foothills where a very slow noise is high (stepped into terraces in places), and small bumps.
        /// The biome shapes them: marshes almost flat, meadows gentler, the deep wilderness craggy.
        public double Hills(double x, double z)
        {
            Warp(x, z, 160.0, 70.0, out double wx, out double wz);
            double rolling = Smooth((hills.Fractal(wx / 80.0, wz / 80.0, 5) - 0.3) / 0.55) * RollingHeight;
            double mid = (knolls.Fractal(wx / 26.0, wz / 26.0, 3) - 0.5) * 2 * MidHeight;
            double rangeMask = Smooth((ranges.Fractal(x / 300.0, z / 300.0, 2) - 0.42) / 0.2);
            double mountains = 0;
            if (rangeMask > 0)
            {
                double r = 1 - Math.Abs(2 * ridges.Fractal(wx / 150.0, wz / 150.0, 4) - 1);
                mountains = rangeMask * (Math.Pow(r, 2.2) * MountainHeight + rolling * 0.6);  // crests, and foothills round them
            }
            double wet = Moisture(x, z);
            double calm = (1 - 0.8 * Smooth((wet - 0.56) / 0.12)) * (0.7 + 0.3 * Smooth((wet - 0.3) / 0.2));
            double h = (rolling + Math.Max(-rolling, mid) + mountains) * calm;
            // Terraces on the high slopes of the ranges: the ground steps up in shelves.
            if (rangeMask > 0 && h > 8)
                h += (Terrace(h, 4.0) - h) * 0.45 * rangeMask * Smooth((h - 8) / 6);
            double danger = DangerAt(x, z);
            if (danger > 18)
                h += Smooth((danger - 18) / 12) * Math.Max(0, bumps.Fractal(x / 13.0, z / 13.0, 2) - 0.45) * 5;  // crags
            return h + (bumps.Fractal(x / 8.0, z / 8.0, 2) - 0.5) * 2 * BumpHeight;
        }

        /// Heights stepped into shelves `step` apart, with a smooth rise between each.
        static double Terrace(double h, double step)
        {
            double t = h / step, k = Math.Floor(t);
            return (k + Smooth((t - k - 0.3) / 0.4)) * step;
        }

        /// How fully rivers run here (0..1), from how far the nearest levelled ground is: none within LevelBlend
        /// of it, narrowing to nothing (and their valleys flattening out) as they near it.
        /// How fully rivers run here (0..1): not in towns, growing to full strength over the 40 tiles past a town's
        /// safe ring. (Other places are never put where a river runs: see Plan.)
        public double RiverAllowed(double x, double z) => riverAllowField.At(x, z);

        double ExactRiverAllowed(double x, double z)
        {
            NearestSettlement(x, z, out double beyond);
            return Smooth(beyond / 40);
        }

        /// A river this strong (RiverPresence x strength) has carved its valley all the way down; water runs only there.
        const double RiverFull = 0.85;

        /// The levelled place (town, camp...) that most holds a point flat, and how much (1 on it, falling to 0 away).
        RegionPlan Levelling(double x, double z, out double flat) => Levelling(x, z, out flat, out _, out _);

        /// ...how far it is to the nearest levelled ground (in tiles; 0 on it), and the height to level to here:
        /// where two places' levelling overlaps, a blend of their heights weighted by how much each holds it.
        RegionPlan Levelling(double x, double z, out double flat, out double nearest, out double height)
        {
            int rx = RegionOf((int)Math.Round(x)), rz = RegionOf((int)Math.Round(z));
            RegionPlan best = null;
            flat = 0;
            nearest = double.MaxValue;
            double weights = 0, sum = 0;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var plan = Plan(rx + dx, rz + dz);
                    if (plan.Kind == RegionKind.Wild)
                        continue;
                    var a = plan.Level;
                    double ox = Math.Max(0, Math.Max(a.X - 0.5 - x, x - (a.XMax - 0.5)));
                    double oz = Math.Max(0, Math.Max(a.Z - 0.5 - z, z - (a.ZMax - 0.5)));
                    double dist = Math.Sqrt(ox * ox + oz * oz);
                    nearest = Math.Min(nearest, dist);
                    double f = 1 - Smooth(dist / LevelBlend);
                    if (f > 0)
                    {
                        // On a place's own ground its weight dwarfs any other's, so it's level at its own height.
                        double w = f * f / (1.000001 - f);
                        weights += w;
                        sum += w * LevelHeight(plan);
                    }
                    if (f > flat)
                    {
                        flat = f;
                        best = plan;
                    }
                }
            height = flat >= 1 ? LevelHeight(best) : weights > 0 ? sum / weights : 0;
            return best;
        }

        /// The height a town, camp, shrine, quarry or course is levelled at: the land's own height at its middle.
        public double LevelHeight(RegionPlan plan)
        {
            if (!plan.HasLevelHeight)
            {
                var c = plan.Level;
                double cx = c.X + c.Width / 2.0, cz = c.Z + c.Height / 2.0;
                plan.LevelHeight = Natural(cx, cz, 0, out _);
                plan.HasLevelHeight = true;
            }
            return plan.LevelHeight;
        }

        /// What covers the ground: grass (or the biome's ground), bare earth on steep slopes, rock on steeper
        /// ones and high up, snow on the peaks. `slope`: the most the height changes across the tile.
        public static Ground GroundFor(double height, double slope, double jitter)
        {
            if (height > SnowLine + jitter * 3)
                return Ground.Snow;
            if (slope > 1.15 || height > RockLine + jitter * 3)
                return Ground.Rock;
            if (slope > 0.7)
                return Ground.Earth;
            return Ground.Grass;
        }

        /// The ground on a tile, worked out from its corners' heights.
        public Ground GroundAt(Tile t) => GroundAt(t, Elevation(t.X - 0.5, t.Z - 0.5), Elevation(t.X + 0.5, t.Z - 0.5),
            Elevation(t.X - 0.5, t.Z + 0.5), Elevation(t.X + 0.5, t.Z + 0.5));

        /// The ground on a tile whose corners are at these heights. Levelled ground (towns, camps...) stays grass.
        public Ground GroundAt(Tile t, double a, double b, double c, double d)
        {
            var k = At(t);
            if (k != Terrain.Grass && k != Terrain.Tree && k != Terrain.Flax)
                return Ground.Grass;
            Levelling(t.X, t.Z, out double flat);
            if (flat > 0.5)
                return Ground.Grass;
            double lo = Math.Min(Math.Min(a, b), Math.Min(c, d)), hi = Math.Max(Math.Max(a, b), Math.Max(c, d));
            return GroundFor((a + b + c + d) / 4, hi - lo, Hash(t.X, t.Z, 90));
        }

        // ---- Rivers ----

        public const double RiverWidth = 1.7;      // half the width of a river, in tiles (about 3-4 wide)
        const double RiverValley = 40;             // tiles from a river's bank to the full height of the hills
        public const int BridgeSpacing = 32;       // a bridge where a river crosses each line of this lattice

        /// Rivers wind along where a slow, warped noise passes one value: long lines that never end, so they
        /// run on for ever, through valleys they carve. Returns how far a point is from the nearest river's
        /// middle (in tiles, roughly), and how strong the river is there (0..1: none where the noise is nearly
        /// flat, round its peaks and dips, so no river loops round tightly; it fades out smoothly instead).
        public double RiverDistance(double x, double z, out double strength)
        {
            double v = riverField.At(x, z) - 0.5;
            double gx = (riverField.At(x + 8, z) - riverField.At(x - 8, z)) / 16, gz = (riverField.At(x, z + 8) - riverField.At(x, z - 8)) / 16;
            double g = Math.Sqrt(gx * gx + gz * gz);
            strength = Smooth((g - 0.0006) / 0.0014);
            return Math.Abs(v) / Math.Max(g, 0.0004);
        }

        public const double RiverSpeed = 1.2;  // tiles a second, in a river's middle

        /// Which way and how fast a river's water runs at a point (tiles a second): along the river (one way
        /// all along it), fastest in its middle, still in ponds and away from rivers.
        public void RiverFlow(double x, double z, out double fx, out double fz)
        {
            fx = fz = 0;
            double gx = (riverField.At(x + 8, z) - riverField.At(x - 8, z)) / 16, gz = (riverField.At(x, z + 8) - riverField.At(x, z - 8)) / 16;
            double g = Math.Sqrt(gx * gx + gz * gz);
            if (g < 1e-7)
                return;
            double dist = RiverDistance(x, z, out double strength);
            double p = strength * RiverAllowed(x, z) * Math.Max(0, 1 - dist / (RiverWidth * 2.5));
            fx = -gz / g * p * RiverSpeed;
            fz = gx / g * p * RiverSpeed;
        }

        /// A smoother measure of the same for shaping valleys: how far the noise is from the river's value, in
        /// tiles at a typical slope of the noise (it never changes faster than the noise does, so no cliffs).
        double ValleyDistance(double x, double z) => Math.Abs(riverField.At(x, z) - 0.5) / 0.004;

        double ExactRiver(double x, double z)
        {
            Warp(x, z, 180.0, 90.0, out double wx, out double wz);
            return rivers.Fractal(wx / 240.0, wz / 240.0, 2);
        }

        /// Whether a tile is part of a river (not near levelled ground, not in a pond).
        bool IsRiver(Tile t)
        {
            var plan = PlanAt(t);
            if (plan.KeepsClear(t) || plan.At(t) != Terrain.Grass)
                return false;
            Levelling(t.X, t.Z, out _, out double nearest, out _);
            if (nearest < LevelBlend)
                return false;  // never on or beside levelled ground
            double dist = RiverDistance(t.X, t.Z, out double strength);
            double presence = RiverAllowed(t.X, t.Z) * strength;
            if (presence < RiverFull)
                return false;  // its valley isn't carved all the way down yet
            return dist < RiverWidth * presence;
        }

        /// Whether a river runs within reach of a place's middle (anything levelled there would dam it).
        bool RiverNear(Tile c)
        {
            for (int dx = -40; dx <= 40; dx += 10)
                for (int dz = -40; dz <= 40; dz += 10)
                {
                    double d = RiverDistance(c.X + dx, c.Z + dz, out double strength);
                    if (strength * RiverAllowed(c.X + dx, c.Z + dz) > 0.1 && d < 8)
                        return true;
                }
            return false;
        }

        /// Bridges cross rivers along the lines of a lattice, two tiles wide, so a river never cuts off any land.
        static bool OnBridgeLine(Tile t) => FloorMod(t.X, BridgeSpacing) < 2 || FloorMod(t.Z, BridgeSpacing) < 2;

        static int FloorMod(int a, int b) => ((a % b) + b) % b;

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

        const byte Unknown = 255, Dry = 0, WetPond = 1, WetRiver = 2;
        readonly Dictionary<long, byte[]> wetness = new Dictionary<long, byte[]>();

        /// Whether a tile is under a pond or a river (or dry), worked out once and remembered by chunk: trees
        /// ask it of all their neighbours.
        byte Wetness(Tile t)
        {
            int cx = FloorDiv(t.X, ChunkSize), cz = FloorDiv(t.Z, ChunkSize);
            long key = ChunkedGrid.Key(cx, cz);
            if (!wetness.TryGetValue(key, out var tiles))
            {
                if (wetness.Count > 4096)
                    wetness.Clear();
                tiles = new byte[ChunkSize * ChunkSize];
                for (int i = 0; i < tiles.Length; i++)
                    tiles[i] = Unknown;
                wetness[key] = tiles;
            }
            int k = (t.X - cx * ChunkSize) * ChunkSize + (t.Z - cz * ChunkSize);
            if (tiles[k] == Unknown)
                tiles[k] = IsPond(t) ? WetPond : IsRiver(t) ? WetRiver : Dry;
            return tiles[k];
        }

        bool IsWet(Tile t) => Wetness(t) != Dry;
        readonly Dictionary<long, byte[]> known = new Dictionary<long, byte[]>();

        Terrain WorkOut(Tile t)
        {
            var plan = PlanAt(t);
            var planned = plan.At(t);
            if (planned != Terrain.Grass)
                return planned;
            if (plan.KeepsClear(t))
                return IsRoad(t) ? Terrain.Path : planned;
            byte wet = Wetness(t);
            if (wet == WetPond)
                return Terrain.Water;
            // Roads cross rivers by bridges of their own.
            if (wet == WetRiver)
                return OnBridgeLine(t) || IsRoad(t) ? Terrain.Bridge : Terrain.Water;
            if (IsRoad(t))
                return Terrain.Path;
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
            // None on or near levelled ground (its water would sit at the wrong height).
            Levelling(t.X, t.Z, out double flat);
            if (flat > 0)
                return false;
            // Roads stay dry with a margin (a road can run diagonally, which water either side would cut).
            if (RoadDistance(t.X, t.Z) < RoadHalfWidth + 2)
                return false;
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

        double PondPoint(int i, int j) => ponds.At(i, j) - 0.4 * Math.Max(0, Hills(i * PondScale, j * PondScale) - 2.0) / 2.0;

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
                    if (nt == Terrain.Door || (near && (nt == Terrain.Path || nt == Terrain.Wall || nt == Terrain.TownWall || nt == Terrain.Fence
                        || nt == Terrain.Camp || IsWet(n) || IsRoad(n) || np.Blocked.Contains(n))))
                        return Terrain.Grass;
                    if (near && np.KeepsClear(n))
                        return Terrain.Grass;
                }
            // What grows depends on the biome: woodland thick with trees, meadows open with flax fields, the
            // wasteland's trees few and far between. (No ore out here: rocks to mine are only in quarries.)
            double roll = Hash(t.X, t.Z, 7);
            var biome = BiomeAt(t);
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
