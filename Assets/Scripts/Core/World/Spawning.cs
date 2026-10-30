using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// Which node grows on an overworld tile, and which monsters live in a region, from the spawn rules
    /// content gives (NodeDef.Spawns, NpcDef.Spawns). Picks depend only on the seed and the place, so they
    /// come out the same every time. Further from the start, better things are in the running.
    public static class Spawning
    {
        /// Tiles from the start (straight-line).
        public static double Distance(Overworld ow, Tile t)
        {
            double dx = t.X - ow.Spawn.X, dz = t.Z - ow.Spawn.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// The node on a Tree, Rock or Water tile (null if no rule covers it).
        public static NodeDef NodeAt(Overworld ow, Tile t, Terrain on)
        {
            double d = Distance(ow, t), total = 0;
            var db = ContentDb.Current;
            foreach (var n in db.Nodes)
                foreach (var r in n.Spawns)
                    if (r.On == on && d >= r.MinDistance && d < r.MaxDistance)
                        total += r.Weight;
            if (total <= 0)
                return null;
            double pick = ow.Hash(t.X, t.Z, 21 + (int)on) * total;
            foreach (var n in db.Nodes)
                foreach (var r in n.Spawns)
                    if (r.On == on && d >= r.MinDistance && d < r.MaxDistance)
                    {
                        if (pick < r.Weight)
                            return n;
                        pick -= r.Weight;
                    }
            return null;
        }

        /// The altar a shrine region holds (StationDef.Spawns by distance), or null.
        public static StationDef ShrineAltar(Overworld ow, RegionPlan plan)
        {
            double d = Distance(ow, plan.Center), total = 0;
            var db = ContentDb.Current;
            if (plan.RX == 1 && plan.RZ == 0)
                foreach (var s in db.Stations)
                    foreach (var r in s.Spawns)
                        if (r.Starter)
                            return s;
            foreach (var s in db.Stations)
                foreach (var r in s.Spawns)
                    if (d >= r.MinDistance && d < r.MaxDistance)
                        total += r.Weight;
            if (total <= 0)
                return null;
            double pick = ow.Hash(plan.RX, plan.RZ, 50) * total;
            foreach (var s in db.Stations)
                foreach (var r in s.Spawns)
                    if (d >= r.MinDistance && d < r.MaxDistance)
                    {
                        if (pick < r.Weight)
                            return s;
                        pick -= r.Weight;
                    }
            return null;
        }

        /// The agility course a course region holds (CourseDef.Spawns by distance), or null.
        public static CourseDef CourseIn(Overworld ow, RegionPlan plan)
        {
            double d = Distance(ow, plan.Center), total = 0;
            var db = ContentDb.Current;
            if (plan.RX == -1 && plan.RZ == 0)
                foreach (var c in db.Courses)
                    foreach (var r in c.Spawns)
                        if (r.Starter)
                            return c;
            foreach (var c in db.Courses)
                foreach (var r in c.Spawns)
                    if (d >= r.MinDistance && d < r.MaxDistance)
                        total += r.Weight;
            if (total <= 0)
                return null;
            double pick = ow.Hash(plan.RX, plan.RZ, 51) * total;
            foreach (var c in db.Courses)
                foreach (var r in c.Spawns)
                    if (d >= r.MinDistance && d < r.MaxDistance)
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

        /// The monster groups living in a region: a camp has one (its occupants); wild country up to two;
        /// a town a few chickens and the like.
        public static List<Group> GroupsIn(Overworld ow, RegionPlan plan)
        {
            var groups = new List<Group>();
            int count = plan.Kind == RegionKind.Shrine || plan.Kind == RegionKind.Course ? 0 : plan.Kind == RegionKind.Camp ? 1 : plan.Kind == RegionKind.Town ? 1 : 1 + (ow.Hash(plan.RX, plan.RZ, 30) < 0.5 ? 1 : 0);
            double d = Distance(ow, plan.Center);
            for (int g = 0; g < count; g++)
                Pick(ow, plan, d, g, false, groups);
            // Towns also have their people (residents: the ones you can pickpocket), in two groups.
            if (plan.Kind == RegionKind.Town)
                for (int g = 0; g < 2; g++)
                    Pick(ow, plan, d, 10 + g, true, groups);
            return groups;
        }

        static void Pick(Overworld ow, RegionPlan plan, double d, int g, bool residents, List<Group> groups)
        {
            double total = 0;
            foreach (var n in ContentDb.Current.Npcs)
                foreach (var r in n.Spawns)
                    if (r.Region == plan.Kind && r.Resident == residents && d >= r.MinDistance && d < r.MaxDistance)
                        total += r.Weight;
            if (total <= 0)
                return;
            double pick = ow.Hash(plan.RX, plan.RZ, 31 + g) * total;
            foreach (var n in ContentDb.Current.Npcs)
                foreach (var r in n.Spawns)
                    if (r.Region == plan.Kind && r.Resident == residents && d >= r.MinDistance && d < r.MaxDistance)
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
