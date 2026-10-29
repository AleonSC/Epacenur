namespace Epacenur.Core
{
    /// Something you can cook on a fire. To add more food: add a CookDef here.
    public sealed class CookDef
    {
        public int RawItemId, CookedItemId, BurntItemId;
        public int Level;
        public double Xp;
        public int LowChance, HighChance;  // chance not to burn, out of 256, at level 1 and 99
    }

    public static class CookingRules
    {
        /// Ticks per item cooked.
        public const int AttemptTicks = 4;
        public const int FirstPersonCookTicks = 2;

        // Both stop burning at level 34 on a fire, as in RuneScape.
        public static readonly CookDef Shrimps = new CookDef
        {
            RawItemId = ItemDefs.RawShrimps.Id, CookedItemId = ItemDefs.Shrimps.Id, BurntItemId = ItemDefs.BurntShrimp.Id,
            Level = 1, Xp = 30, LowChance = 80, HighChance = 605,
        };

        public static readonly CookDef Anchovies = new CookDef
        {
            RawItemId = ItemDefs.RawAnchovies.Id, CookedItemId = ItemDefs.Anchovies.Id, BurntItemId = ItemDefs.BurntFish.Id,
            Level = 1, Xp = 30, LowChance = 80, HighChance = 605,
        };

        public static readonly CookDef[] All = { Shrimps, Anchovies };

        /// The cooking rules for a raw item, or null if it can't be cooked.
        public static CookDef For(int rawItemId)
        {
            foreach (var c in All)
                if (c.RawItemId == rawItemId)
                    return c;
            return null;
        }

        /// Chance (0..1) of cooking it rather than burning it.
        public static double SuccessChance(CookDef food, int level) => GatherRules.Chance(food.LowChance, food.HighChance, level);
    }
}
