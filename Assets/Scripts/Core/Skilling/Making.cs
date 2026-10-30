using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// The rules for making things from recipes at stations: smelting, smithing, cooking.
    public static class Making
    {
        /// The "station" of recipes made in your hands from the inventory (knife on logs...).
        public const string InHand = "inventory";

        /// Why a recipe can't be made right now, or null if it can. `hasTool` says whether the
        /// recipe's ToolType (if any) is to hand.
        public static string Problem(RecipeDef r, Inventory inv, int level, bool hasTool)
        {
            var skill = ContentDb.Current.Skill(r.Skill);
            if (level < r.Level)
                return "You need " + Article(skill.Name) + " level of " + r.Level + " to make " + Article(r.Name) + ".";
            if (r.ToolType != null && !hasTool)
                return "You need " + Article(r.ToolType) + " to do that.";
            foreach (var i in r.Inputs)
                if (inv.Count(i.ItemId) < i.Count)
                    return "You need " + Needs(r) + " to make " + Article(r.Name) + ".";
            return null;
        }

        /// How many times the inventory can make it.
        public static int CanMake(RecipeDef r, Inventory inv)
        {
            int n = int.MaxValue;
            foreach (var i in r.Inputs)
                n = Math.Min(n, inv.Count(i.ItemId) / i.Count);
            return n;
        }

        /// Uses up the inputs and adds the product (or, for a recipe that can fail and did, the fail product).
        /// Returns the item made, or null (changing nothing) if the inputs aren't there; `failed` says which.
        /// When the recipe takes a single item and makes one unstackable thing, the product goes into that
        /// item's slot, so cooked fish stay put.
        public static string Make(RecipeDef r, Inventory inv, int level, Random rng, out bool failed) => Make(r, inv, level, rng, out failed, out _);

        /// As Make; `made` says how many times (a batch recipe makes up to its Batch at once).
        public static string Make(RecipeDef r, Inventory inv, int level, Random rng, out bool failed, out int times)
        {
            failed = false;
            times = Math.Min(Math.Max(1, r.Batch), CanMake(r, inv));
            if (times < 1)
                return null;
            if (r.FailProduct != null)
                failed = rng.NextDouble() >= SuccessChance(r, level);
            string made = failed ? r.FailProduct : r.Product;
            var def = ContentDb.Current.Item(made);
            if (times == 1 && r.ProductCount == 1 && r.Inputs.Length == 1 && r.Inputs[0].Count == 1 && !def.Stackable)
            {
                int slot = FindSlot(inv, r.Inputs[0].ItemId);
                if (slot >= 0)
                {
                    inv.Set(slot, new ItemStack(made, 1));
                    return made;
                }
            }
            foreach (var i in r.Inputs)
                inv.Remove(i.ItemId, i.Count * times);
            inv.Add(made, r.ProductCount * times);
            return made;
        }

        /// The success chance of a recipe that can fail (1 if it can't).
        public static double SuccessChance(RecipeDef r, int level) => r.FailProduct == null ? 1.0 : Gathering.Chance(r.LowChance, r.HighChance, level);

        /// "1 copper ore and 1 tin ore", "5 bronze bars".
        public static string Needs(RecipeDef r)
        {
            var parts = new List<string>();
            foreach (var i in r.Inputs)
            {
                string name = ContentDb.Current.Item(i.ItemId).Name.ToLowerInvariant();
                parts.Add(i.Count + " " + name + (i.Count > 1 && !name.EndsWith("s") ? "s" : ""));
            }
            return string.Join(" and ", parts);
        }

        static int FindSlot(Inventory inv, string itemId)
        {
            for (int i = 0; i < Inventory.Size; i++)
                if (!inv[i].IsEmpty && inv[i].ItemId == itemId)
                    return i;
            return -1;
        }

        /// "a bronze bar", "an anvil".
        public static string Article(string name) => ("aeiou".IndexOf(char.ToLowerInvariant(name[0])) >= 0 ? "an " : "a ") + name.ToLowerInvariant();
    }
}
