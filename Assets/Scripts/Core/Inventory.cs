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
        public EquipSlot Slot = EquipSlot.None;  // where it's worn, if it can be
        public int AttackBonus, StrengthBonus, DefenceBonus;
        public int AttackTicks = 4;              // weapons: game ticks between attacks
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
            Id = 4, Name = "Bronze sword", Examine = "A razor-sharp sword.", Value = 26,
            Slot = EquipSlot.Weapon, AttackBonus = 4, StrengthBonus = 5, AttackTicks = 4, R = 0.72f, G = 0.5f, B = 0.28f,
        };

        public static readonly ItemDef WoodenShield = new ItemDef
        {
            Id = 5, Name = "Wooden shield", Examine = "A solid wooden shield.", Value = 20,
            Slot = EquipSlot.Shield, DefenceBonus = 5, R = 0.5f, G = 0.33f, B = 0.18f,
        };

        public static readonly ItemDef Ashes = new ItemDef
        {
            Id = 6, Name = "Ashes", Examine = "A heap of ashes.", Value = 2, R = 0.45f, G = 0.45f, B = 0.45f,
        };

        public static readonly ItemDef SmallFishingNet = new ItemDef
        {
            Id = 7, Name = "Small fishing net", Examine = "Useful for catching small fish.", Value = 5, R = 0.75f, G = 0.72f, B = 0.6f,
        };

        public static readonly ItemDef RawShrimps = new ItemDef
        {
            Id = 8, Name = "Raw shrimps", Examine = "I should try cooking this.", Value = 5, R = 0.95f, G = 0.55f, B = 0.55f,
        };

        public static readonly ItemDef RawAnchovies = new ItemDef
        {
            Id = 9, Name = "Raw anchovies", Examine = "I should try cooking this.", Value = 15, R = 0.5f, G = 0.6f, B = 0.75f,
        };

        public static readonly ItemDef BronzeHatchet = new ItemDef
        {
            Id = 10, Name = "Bronze hatchet", Examine = "A woodcutter's hatchet.", Value = 16, R = 0.72f, G = 0.5f, B = 0.28f,
        };

        public static readonly ItemDef Shrimps = new ItemDef
        {
            Id = 11, Name = "Shrimps", Examine = "Some nicely cooked shrimps.", Value = 5, R = 1f, G = 0.45f, B = 0.3f,
        };

        public static readonly ItemDef Anchovies = new ItemDef
        {
            Id = 12, Name = "Anchovies", Examine = "Some nicely cooked anchovies.", Value = 15, R = 0.62f, G = 0.48f, B = 0.36f,
        };

        public static readonly ItemDef BurntShrimp = new ItemDef
        {
            Id = 13, Name = "Burnt shrimp", Examine = "Oops!", Value = 1, R = 0.2f, G = 0.15f, B = 0.12f,
        };

        public static readonly ItemDef BurntFish = new ItemDef
        {
            Id = 14, Name = "Burnt fish", Examine = "Oops!", Value = 1, R = 0.17f, G = 0.15f, B = 0.14f,
        };

        public static readonly ItemDef Bones = new ItemDef
        {
            Id = 15, Name = "Bones", Examine = "Bones are for burying!", Value = 1, R = 0.93f, G = 0.9f, B = 0.8f,
        };

        public static readonly ItemDef[] All =
        {
            Logs, Coins, Tinderbox, BronzeSword, WoodenShield, Ashes, SmallFishingNet, RawShrimps, RawAnchovies, BronzeHatchet,
            Shrimps, Anchovies, BurntShrimp, BurntFish, Bones,
        };


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
        static bool IsCoins(int itemId) => itemId == ItemDefs.Coins.Id;

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
            if (IsCoins(itemId) && pouch < MaxPouchCoins)
                return true;
            var def = ItemDefs.Get(itemId);
            if (def != null && def.Stackable && IndexOf(itemId) >= 0)
                return true;
            return !IsFull;
        }

        /// Adds items; returns how many fit (non-stackable items take a slot each). Coins go into the
        /// money pouch, and only what doesn't fit there into the backpack.
        public int Add(int itemId, int count = 1)
        {
            var def = ItemDefs.Get(itemId);
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
            int itemId = def.Id;
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
            int i = IndexOf(ItemDefs.Coins.Id);
            if (i < 0)
                i = FirstEmpty();
            if (i < 0)
                return 0;
            int have = slots[i].IsEmpty ? 0 : slots[i].Count;
            count = Math.Min(count, int.MaxValue - have);
            slots[i] = new ItemStack { ItemId = ItemDefs.Coins.Id, Count = have + count };
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
        public int Remove(int itemId, int count)
        {
            int fromPouch = IsCoins(itemId) ? TakeFromPouch(count) : 0;
            return fromPouch + RemoveFromSlots(itemId, count - fromPouch);
        }

        /// Removes up to `count` of an item from backpack slots only. Returns how many.
        public int RemoveFromSlots(int itemId, int count)
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
        public int Count(int itemId)
        {
            long n = CountInSlots(itemId) + (IsCoins(itemId) ? (long)pouch : 0);
            return (int)Math.Min(n, int.MaxValue);
        }

        /// Count of an item in backpack slots only (not the pouch).
        public int CountInSlots(int itemId)
        {
            long n = 0;
            foreach (var s in slots)
                if (!s.IsEmpty && s.ItemId == itemId)
                    n += s.Count;
            return (int)Math.Min(n, int.MaxValue);
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
