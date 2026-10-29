using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    public sealed class ShopItem
    {
        public string ItemId;
        public int NormalStock;  // what the shop restocks to (0 for things players sold it)
        public int Stock;
        public ItemDef Def => ContentDb.Current.Item(ItemId);
    }

    /// A running shop, made from a ShopDef: its stock, buying and selling. Prices are the item's value;
    /// selling pays the shop's sell rate (40% for a general store), rounded down. Stock drifts back to normal
    /// over time (Restock), and items players sold that the shop doesn't normally stock go away.
    public sealed class Shop
    {
        public readonly ShopDef Def;
        public string Name => Def.Name;
        public readonly List<ShopItem> Items = new List<ShopItem>();

        public event Action Changed;

        public Shop(ShopDef def)
        {
            Def = def;
            foreach (var s in def.Stock)
                Items.Add(new ShopItem { ItemId = s.ItemId, NormalStock = s.Count, Stock = s.Count });
        }

        public static int BuyPrice(ItemDef def) => Math.Max(1, def.Value);
        public int SellPrice(ItemDef def) => (int)Math.Floor(def.Value * Def.SellRate);

        /// Coins can't be sold anywhere; a specialist shop only buys what it stocks.
        public bool CanSell(ItemDef def) => def != null && def.Id != ItemIds.Coins && (Def.BuysAnything || Find(def.Id) != null);

        /// Buys up to `amount` of the shop's item at `index`. Returns how many were bought and,
        /// when fewer than asked, why (for the chat box).
        public int Buy(Inventory inv, int index, int amount, out string problem)
        {
            problem = null;
            if (index < 0 || index >= Items.Count)
                return 0;
            var item = Items[index];
            int price = BuyPrice(item.Def);
            int bought = 0;
            for (; bought < amount; bought++)
            {
                if (item.Stock <= 0) { problem = "The shop has run out of stock."; break; }
                if (inv.Count(ItemIds.Coins) < price) { problem = "You don't have enough coins."; break; }
                // Paying might empty a coin stack in the backpack, which makes room for the item.
                bool coinsFreeASlot = inv.CountInSlots(ItemIds.Coins) > 0 && inv.Count(ItemIds.Coins) == price;
                if (!inv.HasRoomFor(item.ItemId) && !coinsFreeASlot) { problem = "You don't have enough inventory space."; break; }
                inv.Remove(ItemIds.Coins, price);
                inv.Add(item.ItemId, 1);
                item.Stock--;
            }
            if (bought > 0)
                Changed?.Invoke();
            return bought;
        }

        /// Sells up to `amount` of an item from the inventory. Returns how many were sold.
        public int Sell(Inventory inv, string itemId, int amount, out string problem)
        {
            problem = null;
            var def = ContentDb.Current.Item(itemId);
            if (!CanSell(def))
            {
                problem = "You can't sell this item to this shop.";
                return 0;
            }
            int price = SellPrice(def);
            int sold = 0;
            for (; sold < amount && inv.CountInSlots(itemId) > 0; sold++)
            {
                inv.RemoveFromSlots(itemId, 1);
                if (price > 0 && inv.Add(ItemIds.Coins, price) == 0)
                {
                    inv.Add(itemId, 1);  // no room for the coins: undo
                    problem = "You don't have enough inventory space.";
                    break;
                }
                var entry = Find(itemId);
                if (entry == null)
                    Items.Add(entry = new ShopItem { ItemId = itemId, NormalStock = 0, Stock = 0 });
                entry.Stock++;
            }
            if (sold > 0)
                Changed?.Invoke();
            return sold;
        }

        /// One restock step: every item moves one closer to its normal stock;
        /// sold-in items that have run out disappear.
        public void Restock()
        {
            bool changed = false;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var item = Items[i];
                if (item.Stock < item.NormalStock) { item.Stock++; changed = true; }
                else if (item.Stock > item.NormalStock) { item.Stock--; changed = true; }
                if (item.NormalStock == 0 && item.Stock == 0) { Items.RemoveAt(i); changed = true; }
            }
            if (changed)
                Changed?.Invoke();
        }

        public int IndexOf(string itemId) => Items.FindIndex(i => i.ItemId == itemId);

        ShopItem Find(string itemId)
        {
            foreach (var item in Items)
                if (item.ItemId == itemId)
                    return item;
            return null;
        }
    }
}
