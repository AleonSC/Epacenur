using System;

namespace Epacenur.Core
{
    /// A kind of tree. To add one (oak, willow...): add a TreeDef here and place it in a skill area.
    public sealed class TreeDef
    {
        public string Name;
        public string Examine, StumpExamine;
        public int LevelRequired;
        public double Xp;              // per log
        public int LogItemId;
        public int MinLives, MaxLives; // successful chops before it becomes a stump (rolled each time it grows)
        public float RespawnSeconds;   // how long it stays a stump
        public int LowChance, HighChance;  // chance of a log per chop, out of 256, at level 1 and level 99
        public float TrunkR, TrunkG, TrunkB, LeafR, LeafG, LeafB;
    }

    public static class TreeDefs
    {
        public static readonly TreeDef Basic = new TreeDef
        {
            Name = "Tree", Examine = "One of the most common trees in the land.", StumpExamine = "This tree has been cut down.",
            LevelRequired = 1, Xp = 25, LogItemId = ItemDefs.Logs.Id,
            MinLives = 3, MaxLives = 6, RespawnSeconds = 12f,
            LowChance = 64, HighChance = 200,
            TrunkR = 0.42f, TrunkG = 0.28f, TrunkB = 0.15f,
            LeafR = 0.22f, LeafG = 0.55f, LeafB = 0.2f,
        };
    }

    public static class WoodcuttingRules
    {
        /// One game tick, as in RuneScape.
        public const float TickSeconds = 0.6f;
        /// Tile mode: an automatic chop every 4 ticks (2.4 s) while standing at the tree.
        public const int AutoChopTicks = 4;
        /// First person: the shortest time between two clicks that count as chops.
        public const int FirstPersonSwingTicks = 2;

        /// Chance (0..1) that one chop gets a log. RuneScape-style: rises in a straight line from
        /// LowChance at level 1 to HighChance at level 99, and keeps rising past 99 until it is certain.
        public static double SuccessChance(TreeDef tree, int level) => GatherRules.Chance(tree.LowChance, tree.HighChance, level);
    }

    /// The success roll shared by gathering skills (woodcutting, firemaking, fishing).
    public static class GatherRules
    {
        /// Chance (0..1) out of 256: a straight line from `low` at level 1 to `high` at level 99,
        /// carrying on past 99 until it's certain.
        public static double Chance(int low, int high, int level)
        {
            double perLevel = (high - low) / 98.0;
            double outOf256 = low + perLevel * (level - 1);
            return Math.Max(0.0, Math.Min(1.0, (outOf256 + 1) / 256.0));
        }
    }
}
