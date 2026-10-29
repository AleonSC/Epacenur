using System;

namespace Epacenur.Core
{
    /// Well-known item ids the core rules use. Every other item's id lives in its content module.
    public static class ItemIds
    {
        public const string Coins = "coins";
    }

    public struct ItemStack
    {
        public string ItemId;  // null = empty slot
        public int Count;
        public ItemStack(string itemId, int count) { ItemId = itemId; Count = count; }
        public bool IsEmpty => ItemId == null || Count <= 0;
        public ItemDef Def => ContentDb.Current.Item(ItemId);
    }

    /// The 28-slot backpack, and the money pouch beside it.
    ///
    /// The pouch works like RuneScape 3's: coins you get go straight into it and take no slot. It holds
    /// up to 2,147,483,647; anything past that lands in the backpack as a stack. You can withdraw coins
    /// from it into the backpack (to drop them, for instance) and put a stack back. Spending takes coins
    /// from the pouch first, then from the backpack. Count and Remove for coins cover both.
    public sealed class Inventory
    {
        public const int Size = 28;
        public const int MaxPouchCoins = int.MaxValue;
        readonly ItemStack[] slots = new ItemStack[Size];
        int pouch;

        /// Coins in the money pouch.
        public int PouchCoins => pouch;
        static bool IsCoins(string itemId) => itemId == ItemIds.Coins;

        public event Action Changed;

        public ItemStack this[int slot] => slots[slot];

        public int FreeSlots
        {
            get
            {
                int free = 0;
                foreach (var s in slots)
                    if (s.IsEmpty)
                        free++;
                return free;
            }
        }

        public bool IsFull => FreeSlots == 0;

        /// Whether one more of this item fits.
        public bool HasRoomFor(string itemId)
        {
            if (IsCoins(itemId) && pouch < MaxPouchCoins)
                return true;
            var def = ContentDb.Current.Item(itemId);
            if (def != null && def.Stackable && IndexOf(itemId) >= 0)
                return true;
            return !IsFull;
        }

        /// Adds items; returns how many fit (non-stackable items take a slot each). Coins go into the
        /// money pouch, and only what doesn't fit there into the backpack.
        public int Add(string itemId, int count = 1)
        {
            var def = ContentDb.Current.Item(itemId);
            if (def == null || count <= 0)
                return 0;
            if (IsCoins(itemId))
            {
                int toPouch = Math.Min(count, MaxPouchCoins - pouch);
                pouch += toPouch;
                int rest = count - toPouch;
                int spilled = rest > 0 ? AddToSlots(def, rest) : 0;
                if (toPouch > 0)
                    Changed?.Invoke();
                return toPouch + spilled;
            }
            int added = AddToSlots(def, count);
            if (added > 0)
                Changed?.Invoke();
            return added;
        }

        int AddToSlots(ItemDef def, int count)
        {
            string itemId = def.Id;
            int added = 0;
            if (def.Stackable)
            {
                int i = IndexOf(itemId);
                if (i < 0)
                    i = FirstEmpty();
                if (i >= 0)
                {
                    slots[i] = new ItemStack { ItemId = itemId, Count = (slots[i].IsEmpty ? 0 : slots[i].Count) + count };
                    added = count;
                }
            }
            else
            {
                for (; added < count; added++)
                {
                    int i = FirstEmpty();
                    if (i < 0)
                        break;
                    slots[i] = new ItemStack { ItemId = itemId, Count = 1 };
                }
            }
            if (added > 0)
                Changed?.Invoke();
            return added;
        }

        /// Moves coins from the pouch into the backpack (as one stack). Returns how many moved.
        public int WithdrawFromPouch(int count)
        {
            count = Math.Min(count, pouch);
            if (count <= 0)
                return 0;
            int i = IndexOf(ItemIds.Coins);
            if (i < 0)
                i = FirstEmpty();
            if (i < 0)
                return 0;
            int have = slots[i].IsEmpty ? 0 : slots[i].Count;
            count = Math.Min(count, int.MaxValue - have);
            slots[i] = new ItemStack { ItemId = ItemIds.Coins, Count = have + count };
            pouch -= count;
            Changed?.Invoke();
            return count;
        }

        /// Puts a stack of coins from a backpack slot back into the pouch (as much as fits).
        public int AddToPouch(int slot)
        {
            var s = slots[slot];
            if (s.IsEmpty || !IsCoins(s.ItemId))
                return 0;
            int n = Math.Min(s.Count, MaxPouchCoins - pouch);
            pouch += n;
            s.Count -= n;
            slots[slot] = s.Count > 0 ? s : default(ItemStack);
            Changed?.Invoke();
            return n;
        }

        /// Takes coins out of the pouch only (depositing the pouch at the bank). Returns how many.
        public int TakeFromPouch(int count)
        {
            count = Math.Min(count, pouch);
            if (count <= 0)
                return 0;
            pouch -= count;
            Changed?.Invoke();
            return count;
        }

        /// Sets the pouch directly (loading a save, dev tools).
        public void SetPouch(int coins)
        {
            pouch = Math.Max(0, coins);
            Changed?.Invoke();
        }

        public void Set(int slot, ItemStack stack)
        {
            slots[slot] = stack.IsEmpty ? default(ItemStack) : stack;
            Changed?.Invoke();
        }

        public void RemoveAt(int slot) => Set(slot, default(ItemStack));

        /// Removes up to `count` of an item, from the last slots first (coins: the pouch first).
        /// Returns how many were removed.
        public int Remove(string itemId, int count)
        {
            int fromPouch = IsCoins(itemId) ? TakeFromPouch(count) : 0;
            return fromPouch + RemoveFromSlots(itemId, count - fromPouch);
        }

        /// Removes up to `count` of an item from backpack slots only. Returns how many.
        public int RemoveFromSlots(string itemId, int count)
        {
            int removed = 0;
            for (int i = Size - 1; i >= 0 && removed < count; i--)
            {
                if (slots[i].IsEmpty || slots[i].ItemId != itemId)
                    continue;
                int take = Math.Min(count - removed, slots[i].Count);
                slots[i].Count -= take;
                if (slots[i].Count <= 0)
                    slots[i] = default(ItemStack);
                removed += take;
            }
            if (removed > 0)
                Changed?.Invoke();
            return removed;
        }

        public void Swap(int a, int b)
        {
            var t = slots[a];
            slots[a] = slots[b];
            slots[b] = t;
            Changed?.Invoke();
        }

        public void Clear()
        {
            for (int i = 0; i < Size; i++)
                slots[i] = default(ItemStack);
            Changed?.Invoke();
        }

        /// Total count of an item: every slot, plus the pouch for coins (capped at int.MaxValue).
        public int Count(string itemId)
        {
            long n = CountInSlots(itemId) + (IsCoins(itemId) ? (long)pouch : 0);
            return (int)Math.Min(n, int.MaxValue);
        }

        /// Count of an item in backpack slots only (not the pouch).
        public int CountInSlots(string itemId)
        {
            long n = 0;
            foreach (var s in slots)
                if (!s.IsEmpty && s.ItemId == itemId)
                    n += s.Count;
            return (int)Math.Min(n, int.MaxValue);
        }

        int IndexOf(string itemId)
        {
            for (int i = 0; i < Size; i++)
                if (!slots[i].IsEmpty && slots[i].ItemId == itemId)
                    return i;
            return -1;
        }

        int FirstEmpty()
        {
            for (int i = 0; i < Size; i++)
                if (slots[i].IsEmpty)
                    return i;
            return -1;
        }
    }
}
