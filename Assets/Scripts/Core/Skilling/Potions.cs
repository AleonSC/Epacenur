namespace Epacenur.Core
{
    public static class Potions
    {
        /// Ticks a drink takes (as eating does).
        public const int DrinkTicks = 3;

        /// Drinks a dose from a slot: its boosts, prayer and energy, and what's left goes back in the slot
        /// (the potion with a dose less, or an empty vial). Returns the potion drunk, or null.
        public static PotionDef Drink(Inventory inv, int slot, SkillSet skills, Boosts boosts, PrayerBook prayer, RunEnergy energy)
        {
            var stack = inv[slot];
            var p = stack.IsEmpty ? null : stack.Def.Potion;
            if (p == null)
                return null;
            foreach (var b in p.Boosts)
                boosts.Apply(skills, b);
            if (p.PrayerFlat > 0 || p.PrayerPercent > 0)
                prayer.SetPoints(System.Math.Min(prayer.Max,
                    prayer.Points + p.PrayerFlat + (int)System.Math.Floor(skills.GetLevel(SkillIds.Prayer) * p.PrayerPercent)));
            if (p.Energy > 0)
                energy.Restore(p.Energy);
            if (!Cheats.InfiniteResources)
                inv.Set(slot, new ItemStack(p.Leaves, 1));
            return p;
        }
    }
}
