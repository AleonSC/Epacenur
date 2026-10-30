using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    public static class Hunter
    {
        public const string Skill = "hunter";
        /// Seconds a laid trap waits before something comes along (or doesn't).
        public const float MinWait = 5f, MaxWait = 12f;
        /// Seconds a sprung or caught trap is left before it falls apart.
        public const float Collapse = 90f;

        /// Traps you can have laid at once: one more every 20 levels.
        public static int MaxTraps(int level) => 1 + level / 20;

        /// What a trap of this kind could catch in this biome at your level, best first.
        public static List<CreatureDef> Prey(ContentDb db, string trap, Biome biome, int level)
        {
            var list = new List<CreatureDef>();
            foreach (var c in db.Creatures)
                if (c.Trap == trap && c.Level <= level && c.Biomes.Contains(biome))
                    list.Add(c);
            list.Sort((a, b) => b.Level.CompareTo(a.Level));
            return list;
        }

        /// What a laid trap catches when it's sprung, or null if nothing does.
        public static CreatureDef Catch(ContentDb db, string trap, Biome biome, int level, Random rng)
        {
            foreach (var c in Prey(db, trap, biome, level))
                if (rng.NextDouble() < Gathering.Chance(c.LowChance, c.HighChance, level))
                    return c;
            return null;
        }
    }
}
