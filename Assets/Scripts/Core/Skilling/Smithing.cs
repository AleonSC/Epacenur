using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// Where a recipe is made.
    public enum Station { Furnace, Anvil }

    public struct Ingredient
    {
        public ItemDef Item;
        public int Count;
        public Ingredient(ItemDef item, int count) { Item = item; Count = count; }
    }

    /// Something made from other items at a furnace or anvil.
    public sealed class Recipe
    {
        public string Name;
        public Station Station;
        public int ProductItemId;
        public Ingredient[] Inputs;
        public int Level;
        public double Xp;
        public ItemDef Product => ItemDefs.Get(ProductItemId);
    }

    public static class SmithingRules
    {
        /// Ticks per item made.
        public const int Ticks = 4;

        public static readonly Recipe BronzeBar = new Recipe
        {
            Name = "Bronze bar", Station = Station.Furnace, ProductItemId = ItemDefs.BronzeBar.Id,
            Inputs = new[] { new Ingredient(ItemDefs.CopperOre, 1), new Ingredient(ItemDefs.TinOre, 1) }, Level = 1, Xp = 6.2,
        };

        // Bronze armour at an anvil: 12.5 xp per bar, as in OSRS.
        public static readonly Recipe BronzeFullHelm = new Recipe
        {
            Name = "Bronze full helm", Station = Station.Anvil, ProductItemId = ItemDefs.BronzeFullHelm.Id,
            Inputs = new[] { new Ingredient(ItemDefs.BronzeBar, 2) }, Level = 7, Xp = 25,
        };

        public static readonly Recipe BronzePlatelegs = new Recipe
        {
            Name = "Bronze platelegs", Station = Station.Anvil, ProductItemId = ItemDefs.BronzePlatelegs.Id,
            Inputs = new[] { new Ingredient(ItemDefs.BronzeBar, 3) }, Level = 16, Xp = 37.5,
        };

        public static readonly Recipe BronzePlatebody = new Recipe
        {
            Name = "Bronze platebody", Station = Station.Anvil, ProductItemId = ItemDefs.BronzePlatebody.Id,
            Inputs = new[] { new Ingredient(ItemDefs.BronzeBar, 5) }, Level = 18, Xp = 62.5,
        };

        public static readonly Recipe[] All = { BronzeBar, BronzeFullHelm, BronzePlatelegs, BronzePlatebody };

        public static List<Recipe> At(Station station) => new List<Recipe>(Array.FindAll(All, r => r.Station == station));

        /// Why a recipe can't be made right now, or null if it can.
        public static string Problem(Recipe r, Inventory inv, int smithingLevel)
        {
            if (smithingLevel < r.Level)
                return "You need a Smithing level of " + r.Level + " to make " + Article(r.Name) + ".";
            foreach (var i in r.Inputs)
                if (inv.Count(i.Item.Id) < i.Count)
                    return "You need " + Needs(r) + " to make " + Article(r.Name) + ".";
            return null;
        }

        /// How many times the inventory can make it.
        public static int CanMake(Recipe r, Inventory inv)
        {
            int n = int.MaxValue;
            foreach (var i in r.Inputs)
                n = Math.Min(n, inv.Count(i.Item.Id) / i.Count);
            return n;
        }

        /// Uses up the inputs and adds the product. Returns false (changing nothing) if the inputs aren't there.
        public static bool Make(Recipe r, Inventory inv)
        {
            if (CanMake(r, inv) < 1)
                return false;
            foreach (var i in r.Inputs)
                inv.Remove(i.Item.Id, i.Count);
            inv.Add(r.ProductItemId);
            return true;
        }

        /// "1 copper ore and 1 tin ore", "5 bronze bars".
        public static string Needs(Recipe r)
        {
            var parts = new List<string>();
            foreach (var i in r.Inputs)
                parts.Add(i.Count + " " + i.Item.Name.ToLowerInvariant() + (i.Count > 1 && !i.Item.Name.EndsWith("s") ? "s" : ""));
            return string.Join(" and ", parts);
        }

        static string Article(string name) => ("aeiou".IndexOf(char.ToLowerInvariant(name[0])) >= 0 ? "an " : "a ") + name.ToLowerInvariant();
    }
}
