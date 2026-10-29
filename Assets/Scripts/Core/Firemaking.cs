namespace Epacenur.Core
{
    /// Logs you can burn. To add oak logs etc.: add a LogBurnDef here.
    public sealed class LogBurnDef
    {
        public int LogItemId;
        public int Level;
        public double Xp;
        public int LowChance, HighChance;   // chance to light per attempt, out of 256, at level 1 and 99
        public float MinBurnSeconds, MaxBurnSeconds;
    }

    public static class FiremakingRules
    {
        /// Ticks between attempts to light the logs.
        public const int AttemptTicks = 3;

        public static readonly LogBurnDef[] Logs =
        {
            // Normal logs: about 1 in 4 attempts at level 1, certain by level 43.
            new LogBurnDef { LogItemId = ItemDefs.Logs.Id, Level = 1, Xp = 40, LowChance = 65, HighChance = 513,
                MinBurnSeconds = 60f, MaxBurnSeconds = 120f },
        };

        /// The burn rules for an item, or null if it can't be lit.
        public static LogBurnDef For(int itemId)
        {
            foreach (var l in Logs)
                if (l.LogItemId == itemId)
                    return l;
            return null;
        }

        public static double LightChance(LogBurnDef logs, int level) => GatherRules.Chance(logs.LowChance, logs.HighChance, level);
    }
}
