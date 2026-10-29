using System;

namespace Epacenur.Core
{
    public sealed class FishDef
    {
        public int ItemId;
        public int Level;
        public double Xp;
        public int LowChance, HighChance;  // out of 256, at level 1 and 99
    }

    /// A kind of fishing spot: what you fish it with and what it gives.
    public sealed class FishingSpotDef
    {
        public string Name, Action, Examine;
        public int ToolItemId;
        public FishDef[] Fish;  // highest level first
    }

    public static class FishingRules
    {
        /// Ticks between catches attempts (tile mode); first person is one attempt per click.
        public const int AttemptTicks = 5;
        public const int FirstPersonCastTicks = 2;

        public static readonly FishDef Shrimps = new FishDef { ItemId = ItemDefs.RawShrimps.Id, Level = 1, Xp = 10, LowChance = 48, HighChance = 256 };
        public static readonly FishDef Anchovies = new FishDef { ItemId = ItemDefs.RawAnchovies.Id, Level = 15, Xp = 40, LowChance = 24, HighChance = 128 };

        public static readonly FishingSpotDef NetSpot = new FishingSpotDef
        {
            Name = "Fishing spot", Action = "Net", Examine = "I can see small fish swimming in the water.",
            ToolItemId = ItemDefs.SmallFishingNet.Id,
            Fish = new[] { Anchovies, Shrimps },
        };

        /// The lowest level needed to fish here at all.
        public static int MinLevel(FishingSpotDef spot)
        {
            int min = int.MaxValue;
            foreach (var f in spot.Fish)
                min = Math.Min(min, f.Level);
            return min;
        }

        /// One attempt, RuneScape style: try each fish you have the level for, best first;
        /// the first one that rolls a success is caught. Null when nothing bites.
        public static FishDef Roll(FishingSpotDef spot, int level, Random rng)
        {
            foreach (var f in spot.Fish)
                if (level >= f.Level && rng.NextDouble() < GatherRules.Chance(f.LowChance, f.HighChance, level))
                    return f;
            return null;
        }
    }
}
