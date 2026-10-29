using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    public enum RegionKind { Wild, Town, Camp }

    /// What's planned for one region of the overworld: a town, a goblin camp, or just countryside.
    /// Everything is in world tiles.
    public sealed class RegionPlan
    {
        public int RX, RZ;
        public RegionKind Kind;
        public Area Bounds;
        public Tile Center;
        public TownLayout Town;                              // towns: the built-up tiles (local to Bounds)
        public readonly List<Building> Buildings = new List<Building>();
        public Area Camp;                                    // camps: the trampled ground
        public readonly List<Tile> Tents = new List<Tile>();
        public readonly HashSet<Tile> Blocked = new HashSet<Tile>();  // features and tents stand here

        /// The planned terrain of a tile in this region, or Grass where nothing's planned.
        public Terrain At(Tile t)
        {
            if (Town != null)
                return Town.Map[t.X - Bounds.X, t.Z - Bounds.Z];
            if (Kind == RegionKind.Camp && Camp.Contains(t))
                return Terrain.Camp;
            return Terrain.Grass;
        }

        /// No trees, rocks or water this close to what's built.
        public bool KeepsClear(Tile t)
        {
            if (Kind == RegionKind.Town)
                return t.StepsTo(Center) <= Overworld.TownClearRadius;
            if (Kind == RegionKind.Camp)
                return t.X >= Camp.X - 3 && t.X < Camp.XMax + 3 && t.Z >= Camp.Z - 3 && t.Z < Camp.ZMax + 3;
            return false;
        }
    }

    /// The endless overworld, worked out from a seed: the same tile always comes out the same, so it can be
    /// built a chunk at a time as you walk. It's split into square regions; each is a town, a goblin camp
    /// or wild countryside (the one you start in is always a town). Over that lies the countryside itself:
    /// forests and rocky ground where smooth noise is high, and ponds.
    public sealed class Overworld
    {
        public const int RegionSize = 64, ChunkSize = 16;
        public const int TownClearRadius = 15;  // no trees or ponds this close to a town square
        public readonly int Seed;
        readonly ValueNoise forest, ponds, rocky;
        readonly Dictionary<long, RegionPlan> plans = new Dictionary<long, RegionPlan>();

        public Overworld(int seed)
        {
            Seed = seed;
            forest = new ValueNoise(seed);
            ponds = new ValueNoise(seed + 101);
            rocky = new ValueNoise(seed + 202);
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
            plan.Kind = rx == 0 && rz == 0 ? RegionKind.Town : roll < 0.12 ? RegionKind.Town : roll < 0.32 ? RegionKind.Camp : RegionKind.Wild;
            if (plan.Kind == RegionKind.Town)
                PlanTown(plan);
            else if (plan.Kind == RegionKind.Camp)
                PlanCamp(plan);
            plans[key] = plan;
            return plan;
        }

        void PlanTown(RegionPlan plan)
        {
            int seed = (int)(Hash(plan.RX, plan.RZ, 2) * int.MaxValue);
            plan.Town = WorldGen.GenerateTown(RegionSize, seed);
            foreach (var b in plan.Town.Buildings)
            {
                var moved = b.Moved(plan.Bounds.X, plan.Bounds.Z);
                plan.Buildings.Add(moved);
                plan.Blocked.Add(moved.Feature);
            }
        }

        void PlanCamp(RegionPlan plan)
        {
            int dx = (int)(Hash(plan.RX, plan.RZ, 3) * 20) - 10, dz = (int)(Hash(plan.RX, plan.RZ, 4) * 20) - 10;
            plan.Camp = new Area(plan.Center.X + dx - 6, plan.Center.Z + dz - 5, 12, 10);
            for (int i = 0; i < 2; i++)
            {
                var tent = new Tile(plan.Camp.X + 2 + i * (plan.Camp.Width - 5), plan.Camp.ZMax - 2);
                plan.Tents.Add(tent);
                plan.Blocked.Add(tent);
            }
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

        /// For a Rock tile: tin (true) or copper.
        public bool IsTin(Tile t) => Hash(t.X, t.Z, 9) < 0.5;

        /// Ponds: blobs where a smooth noise is high, away from towns, camps and roads. Dry specks inside a pond
        /// (land with water on most sides) are filled in, so no land is cut off.
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
