using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// Which node grows on an overworld tile, and which monsters live in a region, from the spawn rules
    /// content gives (NodeDef.Spawns, NpcDef.Spawns). Picks depend only on the seed and the place, so they
    /// come out the same every time. Deeper into the wilderness (Overworld.Danger), better resources and
    /// fiercer monsters are in the running; some rules hold only in one biome. Towns are always safe, so
    /// what's in them (market stalls, their people) goes by distance from the start instead: frontier towns
    /// have richer stalls and tougher guards.
    public static class Spawning
    {
        /// Tiles from the start (straight-line).
        public static double Distance(Overworld ow, Tile t)
        {
            double dx = t.X - ow.Spawn.X, dz = t.Z - ow.Spawn.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// The node on a Tree, Rock, Flax, Water, Essence or Stall tile (null if no rule covers it). Most of a
        /// quarry's rocks are its own ore (QuarryOre); the rest are whatever grows at that wilderness level.
        public static NodeDef NodeAt(Overworld ow, Tile t, Terrain on)
        {
            if (on == Terrain.Rock)
            {
                var plan = ow.PlanAt(t);
                if (plan.Kind == RegionKind.Quarry && plan.Rocks.Contains(t) && ow.Hash(t.X, t.Z, 62) < 0.65)
                    return QuarryOre(ow, plan);
            }
            return Pick(ow, t, on, ow.Hash(t.X, t.Z, 21 + (int)on));
        }

        /// The rock a quarry is mostly made of (by wilderness level, like rocky ground), or null.
        public static NodeDef QuarryOre(Overworld ow, RegionPlan plan) => Pick(ow, plan.Center, Terrain.Rock, ow.Hash(plan.RX, plan.RZ, 63));

        /// The node whose spawn rules for `on` cover `t`, chosen by weight with `roll` (0..1).
        static NodeDef Pick(Overworld ow, Tile t, Terrain on, double roll)
        {
            int d = ow.Danger(t);
            double far = Distance(ow, t);
            var biome = ow.BiomeAt(t);
            double total = 0;
            var db = ContentDb.Current;
            foreach (var n in db.Nodes)
                foreach (var r in n.Spawns)
                    if (Holds(r, on, d, far, biome))
                        total += r.Weight;
            if (total <= 0)
                return null;
            double pick = roll * total;
            foreach (var n in db.Nodes)
                foreach (var r in n.Spawns)
                    if (Holds(r, on, d, far, biome))
                    {
                        if (pick < r.Weight)
                            return n;
                        pick -= r.Weight;
                    }
            return null;
        }

        static bool Holds(SpawnRule r, Terrain on, int danger, double far, Biome biome) =>
            r.On == on && danger >= r.MinDanger && danger < r.MaxDanger && far >= r.MinDistance && far < r.MaxDistance
            && (r.Biome == null || r.Biome == biome);

        static bool Holds(StationSpawnRule r, int danger) => danger >= r.MinDanger && danger < r.MaxDanger;

        /// The altar a shrine region holds (StationDef.Spawns by wilderness level), or null.
        public static StationDef ShrineAltar(Overworld ow, RegionPlan plan)
        {
            int d = ow.Danger(plan.Center);
            double total = 0;
            var db = ContentDb.Current;
            if (plan.RX == 1 && plan.RZ == 0)
                foreach (var s in db.Stations)
                    foreach (var r in s.Spawns)
                        if (r.Starter)
                            return s;
            foreach (var s in db.Stations)
                foreach (var r in s.Spawns)
                    if (Holds(r, d))
                        total += r.Weight;
            if (total <= 0)
                return null;
            double pick = ow.Hash(plan.RX, plan.RZ, 50) * total;
            foreach (var s in db.Stations)
                foreach (var r in s.Spawns)
                    if (Holds(r, d))
                    {
                        if (pick < r.Weight)
                            return s;
                        pick -= r.Weight;
                    }
            return null;
        }

        /// The agility course a course region holds (CourseDef.Spawns by wilderness level), or null.
        public static CourseDef CourseIn(Overworld ow, RegionPlan plan)
        {
            int d = ow.Danger(plan.Center);
            double total = 0;
            var db = ContentDb.Current;
            if (plan.RX == -1 && plan.RZ == 0)
                foreach (var c in db.Courses)
                    foreach (var r in c.Spawns)
                        if (r.Starter)
                            return c;
            foreach (var c in db.Courses)
                foreach (var r in c.Spawns)
                    if (Holds(r, d))
                        total += r.Weight;
            if (total <= 0)
                return null;
            double pick = ow.Hash(plan.RX, plan.RZ, 51) * total;
            foreach (var c in db.Courses)
                foreach (var r in c.Spawns)
                    if (Holds(r, d))
                    {
                        if (pick < r.Weight)
                            return c;
                        pick -= r.Weight;
                    }
            return null;
        }

        public struct Group
        {
            public NpcDef Npc;
            public int Count;
        }

        /// The monster groups living in a region: a camp has one (its occupants); wild country up to two, or
        /// three deep in the wilderness; a town a few chickens and the like, and two groups of its people;
        /// shrines, quarries and agility courses none.
        public static List<Group> GroupsIn(Overworld ow, RegionPlan plan)
        {
            var groups = new List<Group>();
            int count = plan.Kind == RegionKind.Shrine || plan.Kind == RegionKind.Quarry || plan.Kind == RegionKind.Course || plan.Kind == RegionKind.Cave ? 0
                : plan.Kind == RegionKind.Camp ? 1 : plan.Kind == RegionKind.Town ? 1 : 1 + (ow.Hash(plan.RX, plan.RZ, 30) < 0.5 ? 1 : 0);
            int d = ow.Danger(plan.Center);
            double far = Distance(ow, plan.Center);
            if (plan.Kind == RegionKind.Wild && d >= 15)
                count++;
            for (int g = 0; g < count; g++)
                Pick(ow, plan, d, far, g, false, groups);
            // Towns also have their people (residents: the ones you can pickpocket), in two groups.
            if (plan.Kind == RegionKind.Town)
                for (int g = 0; g < 2; g++)
                    Pick(ow, plan, d, far, 10 + g, true, groups);
            return groups;
        }

        static bool Holds(NpcSpawnRule r, RegionPlan plan, int danger, double far, bool residents) =>
            r.Region == plan.Kind && r.Resident == residents && danger >= r.MinDanger && danger < r.MaxDanger
            && far >= r.MinDistance && far < r.MaxDistance;

        static void Pick(Overworld ow, RegionPlan plan, int d, double far, int g, bool residents, List<Group> groups)
        {
            double total = 0;
            foreach (var n in ContentDb.Current.Npcs)
                foreach (var r in n.Spawns)
                    if (Holds(r, plan, d, far, residents))
                        total += r.Weight;
            if (total <= 0)
                return;
            double pick = ow.Hash(plan.RX, plan.RZ, 31 + g) * total;
            foreach (var n in ContentDb.Current.Npcs)
                foreach (var r in n.Spawns)
                    if (Holds(r, plan, d, far, residents))
                    {
                        if (pick < r.Weight)
                        {
                            int size = r.MinGroup + (int)(ow.Hash(plan.RX, plan.RZ, 41 + g) * (r.MaxGroup - r.MinGroup + 1));
                            groups.Add(new Group { Npc = n, Count = Math.Min(size, r.MaxGroup) });
                            return;
                        }
                        pick -= r.Weight;
                    }
        }
    }
}
