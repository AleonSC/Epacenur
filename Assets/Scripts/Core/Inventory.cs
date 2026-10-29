using System;

namespace Epacenur.Core
{
    public sealed class ItemDef
    {
        public int Id;
        public string Name;
        public bool Stackable;  // stackable items share one slot; everything else takes a slot each
        public string Examine;
        public int Value;       // in coins: shops sell at this price and buy back for less
        public float R, G, B;   // icon colour until there are real icons
    }

    /// Every item in the game. To add one: give it the next id and add it to All.
    public static class ItemDefs
    {
        public static readonly ItemDef Logs = new ItemDef
        {
            Id = 1, Name = "Logs", Examine = "A number of wooden logs.", Value = 4, R = 0.55f, G = 0.36f, B = 0.18f,
        };

        public static readonly ItemDef Coins = new ItemDef
        {
            Id = 2, Name = "Coins", Stackable = true, Examine = "Lovely money!", Value = 1, R = 0.95f, G = 0.8f, B = 0.25f,
        };

        public static readonly ItemDef Tinderbox = new ItemDef
        {
            Id = 3, Name = "Tinderbox", Examine = "Useful for lighting a fire.", Value = 1, R = 0.5f, G = 0.5f, B = 0.52f,
        };

        public static readonly ItemDef BronzeSword = new ItemDef
        {
            Id = 4, Name = "Bronze sword", Examine = "A razor-sharp sword.", Value = 26, R = 0.72f, G = 0.5f, B = 0.28f,
        };

        public static readonly ItemDef WoodenShield = new ItemDef
        {
            Id = 5, Name = "Wooden shield", Examine = "A solid wooden shield.", Value = 20, R = 0.5f, G = 0.33f, B = 0.18f,
        };

        public static readonly ItemDef[] All = { Logs, Coins, Tinderbox, BronzeSword, WoodenShield };

        public static ItemDef Get(int id)
        {
            foreach (var def in All)
                if (def.Id == id)
                    return def;
            return null;
        }
    }

    public struct ItemStack
    {
        public int ItemId;  // 0 = empty slot
        public int Count;
        public bool IsEmpty => ItemId == 0 || Count <= 0;
        public ItemDef Def => ItemDefs.Get(ItemId);
    }

    /// The 28-slot backpack.
    public sealed class Inventory
    {
        public const int Size = 28;
        readonly ItemStack[] slots = new ItemStack[Size];

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
        public bool HasRoomFor(int itemId)
        {
            var def = ItemDefs.Get(itemId);
            if (def != null && def.Stackable && IndexOf(itemId) >= 0)
                return true;
            return !IsFull;
        }

        /// Adds items; returns how many fit (non-stackable items take a slot each).
        public int Add(int itemId, int count = 1)
        {
            var def = ItemDefs.Get(itemId);
            if (def == null || count <= 0)
                return 0;
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

        public void Set(int slot, ItemStack stack)
        {
            slots[slot] = stack.IsEmpty ? default(ItemStack) : stack;
            Changed?.Invoke();
        }

        public void RemoveAt(int slot) => Set(slot, default(ItemStack));

        /// Removes up to `count` of an item, from the last slots first. Returns how many were removed.
        public int Remove(int itemId, int count)
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

        /// Total count of an item across all slots.
        public int Count(int itemId)
        {
            int n = 0;
            foreach (var s in slots)
                if (!s.IsEmpty && s.ItemId == itemId)
                    n += s.Count;
            return n;
        }

        int IndexOf(int itemId)
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
