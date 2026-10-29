using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    public sealed class ShopItem
    {
        public int ItemId;
        public int NormalStock;  // what the shop restocks to (0 for things players sold it)
        public int Stock;
        public ItemDef Def => ItemDefs.Get(ItemId);
    }

    /// A shop's stock, buying and selling. Prices are the item's value; selling an item to a shop
    /// pays 40% of its value, rounded down, like a RuneScape general store. Stock drifts back to normal
    /// over time (Restock), and items players sold that the shop doesn't normally stock go away.
    public sealed class Shop
    {
        public const double SellRate = 0.4;
        public readonly string Name;
        public readonly List<ShopItem> Items = new List<ShopItem>();

        public event Action Changed;

        public Shop(string name, params (ItemDef item, int stock)[] stock)
        {
            Name = name;
            foreach (var s in stock)
                Items.Add(new ShopItem { ItemId = s.item.Id, NormalStock = s.stock, Stock = s.stock });
        }

        public static int BuyPrice(ItemDef def) => Math.Max(1, def.Value);
        public static int SellPrice(ItemDef def) => (int)Math.Floor(def.Value * SellRate);
        /// Coins can't be sold, everything else can (this is a general store).
        public static bool CanSell(ItemDef def) => def != null && def != ItemDefs.Coins;

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
                if (inv.Count(ItemDefs.Coins.Id) < price) { problem = "You don't have enough coins."; break; }
                // Paying might empty the coin slot, which makes room for the item.
                bool coinsFreeASlot = inv.Count(ItemDefs.Coins.Id) == price;
                if (!inv.HasRoomFor(item.ItemId) && !coinsFreeASlot) { problem = "You don't have enough inventory space."; break; }
                inv.Remove(ItemDefs.Coins.Id, price);
                inv.Add(item.ItemId, 1);
                item.Stock--;
            }
            if (bought > 0)
                Changed?.Invoke();
            return bought;
        }

        /// Sells up to `amount` of an item from the inventory. Returns how many were sold.
        public int Sell(Inventory inv, int itemId, int amount, out string problem)
        {
            problem = null;
            var def = ItemDefs.Get(itemId);
            if (!CanSell(def))
            {
                problem = "You can't sell this item to this shop.";
                return 0;
            }
            int price = SellPrice(def);
            int sold = 0;
            for (; sold < amount && inv.Count(itemId) > 0; sold++)
            {
                inv.Remove(itemId, 1);
                if (price > 0 && inv.Add(ItemDefs.Coins.Id, price) == 0)
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

        ShopItem Find(int itemId)
        {
            foreach (var item in Items)
                if (item.ItemId == itemId)
                    return item;
            return null;
        }
    }

    public static class Shops
    {
        /// The dev area's shop. To stock something else, add it here.
        public static Shop DevAreaStore() => new Shop("Dev Area General Store",
            (ItemDefs.Tinderbox, 5),
            (ItemDefs.BronzeSword, 5),
            (ItemDefs.WoodenShield, 5),
            (ItemDefs.SmallFishingNet, 5));
    }
}
