using System;

namespace Epacenur.Core
{
    /// A farming patch: whether you've rented it from its farm's farmer; empty, or growing a seed since a time
    /// (real time: it grows while you're away), and once grown, how many harvests are left.
    [Serializable]
    public sealed class PatchState
    {
        public int x, z, map;   // map: MapId (the overworld or the dev plot)
        public string seed;      // the seed item planted, or null when empty
        public long planted;     // UTC ticks when it was planted
        public int lives;        // harvests left once it's grown
        public bool rented;      // yours to plant (rented from the farmer; a town's patches are all the farmer's at first)
    }

    public static class Farming
    {
        public const string Skill = "farming";
        public const int Stages = 4;

        public static bool IsEmpty(PatchState p) => p.seed == null;

        /// What the farmer charges to rent you a patch, once (it's yours for good).
        public static int RentCost(PatchKind kind) => kind == PatchKind.Herb ? 250 : 100;

        /// Rents a patch, paying from your coins. Why not, or null when it's done.
        public static string Rent(PatchState p, PatchKind kind, Inventory inv)
        {
            if (p.rented)
                return "You already rent that patch.";
            int cost = RentCost(kind);
            if (inv.Count(ItemIds.Coins) < cost)
                return "You need " + cost + " coins to rent " + (kind == PatchKind.Herb ? "a herb patch." : "an allotment.");
            inv.Remove(ItemIds.Coins, cost);
            p.rented = true;
            return null;
        }

        /// How far it's grown, 0 (just planted) to 1 (ready).
        public static double Growth(PatchState p, SeedDef seed, DateTime now)
        {
            if (p.seed == null || seed == null)
                return 0;
            double minutes = (now - new DateTime(p.planted, DateTimeKind.Utc)).TotalMinutes;
            return Math.Max(0, Math.Min(1, minutes / seed.GrowMinutes));
        }

        /// Its stage of growth: 0 just planted .. Stages when ready to harvest.
        public static int Stage(PatchState p, SeedDef seed, DateTime now) => (int)Math.Floor(Growth(p, seed, now) * Stages);

        public static bool IsGrown(PatchState p, SeedDef seed, DateTime now) => p.seed != null && Growth(p, seed, now) >= 1;

        /// Why the seed can't go in this patch, or null.
        public static string Problem(PatchState p, PatchKind kind, SeedDef seed, int level)
        {
            if (seed == null)
                return "You can't plant that.";
            if (seed.Patch != kind)
                return "You can't plant " + Making.Article(seed.Name.ToLowerInvariant()) + " in " + (kind == PatchKind.Herb ? "a herb patch." : "an allotment.");
            if (p.seed != null)
                return "There's already something growing here.";
            if (level < seed.Level)
                return "You need a Farming level of " + seed.Level + " to plant that.";
            return null;
        }

        /// Plants a seed (one, taken from the inventory), giving planting xp.
        public static void Plant(PatchState p, SeedDef seed, DateTime now, Random rng)
        {
            p.seed = seed.SeedId;
            p.planted = now.Ticks;
            p.lives = seed.MinYield + rng.Next(seed.MaxYield - seed.MinYield + 1);
        }

        /// One harvest from a grown patch: its product, and the patch empties with the last. Returns false if not grown.
        public static bool Harvest(PatchState p, SeedDef seed, DateTime now)
        {
            if (!IsGrown(p, seed, now) || p.lives <= 0)
                return false;
            p.lives--;
            if (p.lives == 0)
                p.seed = null;
            return true;
        }
    }
}
