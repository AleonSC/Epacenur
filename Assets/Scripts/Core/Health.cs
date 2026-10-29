using System;

namespace Epacenur.Core
{
    /// Your hitpoints, OSRS style: the maximum is your Hitpoints level.
    public sealed class Health
    {
        /// Seconds per point regenerated (OSRS: 1 every 100 ticks).
        public const float RegenSeconds = 60f;

        public int Current { get; private set; }
        public int Max { get; private set; }
        public bool IsFull => Current >= Max;
        public bool IsDead => Current <= 0;
        public float Fraction => Max > 0 ? (float)Current / Max : 0f;

        public event Action Changed;

        public Health(int max)
        {
            Max = Current = Math.Max(1, max);
        }

        /// Follows the Hitpoints level. A level gained adds to current health too, as in RuneScape.
        public void SetMax(int max)
        {
            max = Math.Max(1, max);
            if (max == Max)
                return;
            if (max > Max)
                Current += max - Max;
            Max = max;
            Current = Math.Min(Current, Max);
            Changed?.Invoke();
        }

        /// Heals up to `amount` (never past the maximum). Returns how much it healed.
        public int Heal(int amount)
        {
            int healed = Math.Max(0, Math.Min(amount, Max - Current));
            Current += healed;
            if (healed > 0)
                Changed?.Invoke();
            return healed;
        }

        /// Takes damage (never below 0). Returns how much was taken.
        public int Damage(int amount)
        {
            int taken = Math.Max(0, Math.Min(amount, Current));
            Current -= taken;
            if (taken > 0)
                Changed?.Invoke();
            return taken;
        }

        /// Sets health directly (loading a save, respawning), clamped to 0..Max.
        public void Set(int current)
        {
            Current = Math.Max(0, Math.Min(current, Max));
            Changed?.Invoke();
        }
    }

    public sealed class FoodDef
    {
        public int ItemId;
        public int Heals;
    }

    /// What can be eaten and how much it heals. To add food: add it to All.
    public static class Foods
    {
        /// Eating takes 3 ticks, and delays your next attack by as much.
        public const int EatTicks = 3;

        public static readonly FoodDef[] All =
        {
            new FoodDef { ItemId = ItemDefs.Shrimps.Id, Heals = 3 },
            new FoodDef { ItemId = ItemDefs.Anchovies.Id, Heals = 1 },
        };

        public static FoodDef For(int itemId) => Array.Find(All, f => f.ItemId == itemId);

        /// Eats the food in an inventory slot. Returns the food eaten (null if it isn't food) and how much it healed.
        public static FoodDef Eat(Inventory inv, int slot, Health health, out int healed)
        {
            healed = 0;
            var stack = inv[slot];
            var food = stack.IsEmpty ? null : For(stack.ItemId);
            if (food == null)
                return null;
            inv.RemoveAt(slot);
            healed = health.Heal(food.Heals);
            return food;
        }
    }
}
