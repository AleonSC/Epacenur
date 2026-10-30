using System;

namespace Epacenur.Core
{
    /// The rules shared by gathering skills (woodcutting, mining, fishing) and firemaking.
    public static class Gathering
    {
        /// Chance (0..1) out of 256: a straight line from `low` at level 1 to `high` at level 99,
        /// carrying on past 99 until it's certain (RuneScape's skilling chance, extended to level 999).
        public static double Chance(int low, int high, int level)
        {
            double perLevel = (high - low) / 98.0;
            double outOf256 = low + perLevel * (level - 1);
            return Math.Max(0.0, Math.Min(1.0, (outOf256 + 1) / 256.0));
        }

        public static double Chance(NodeYield y, int level) => Chance(y.LowChance, y.HighChance, level);

        /// How much a tool of a tier multiplies the chance: bronze (tier 1) x1, each tier up 20% more.
        public static double ToolBoost(int tier) => 1.0 + 0.2 * Math.Max(0, tier - 1);

        /// The chance with a tool of a tier (capped at certain).
        public static double Chance(NodeYield y, int level, int toolTier) => Math.Min(1.0, Chance(y, level) * ToolBoost(toolTier));

        /// One attempt at a node: tries each yield you have the level for, best first; the first one that
        /// rolls a success is what you get. Null when nothing comes of it. `toolTier`: the tool used (better is faster).
        public static NodeYield Roll(NodeDef node, int level, Random rng, int toolTier = 1)
        {
            foreach (var y in node.Yields)
                if (level >= y.Level && rng.NextDouble() < Chance(y, level, toolTier))
                    return y;
            return null;
        }

        /// Lives for a node that's just grown back (successes before it's used up; 0 = never).
        public static int RollLives(NodeDef node, Random rng) => node.MaxLives <= 0 ? 0 : rng.Next(node.MinLives, node.MaxLives + 1);
    }

    public static class Firemaking
    {
        /// Ticks between attempts to light logs.
        public const int AttemptTicks = 3;

        public static double LightChance(BurnableDef logs, int level) => Gathering.Chance(logs.LowChance, logs.HighChance, level);
    }
}
