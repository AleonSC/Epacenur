using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    public struct BankSlot
    {
        public int ItemId;
        public long Count;
        public ItemDef Def => ItemDefs.Get(ItemId);
    }

    /// The bank: every kind of item takes one slot and all of it stacks there, even items that
    /// take a slot each in the inventory (like logs). Slots stay in the order items were first deposited.
    public sealed class Bank
    {
        public const int Size = 400;
        readonly List<BankSlot> slots = new List<BankSlot>();

        public event Action Changed;

        public int UsedSlots => slots.Count;
        public BankSlot this[int slot] => slots[slot];

        public long Count(int itemId)
        {
            int i = IndexOf(itemId);
            return i < 0 ? 0 : slots[i].Count;
        }

        /// Whether the item already has a slot or there's a free one.
        public bool HasRoomFor(int itemId) => IndexOf(itemId) >= 0 || slots.Count < Size;

        /// Moves up to `amount` of an item from the inventory's slots into the bank (coins in the money
        /// pouch stay there: see DepositPouch). Returns how many moved.
        public int Deposit(Inventory inv, int itemId, int amount)
        {
            int n = Math.Min(amount, inv.CountInSlots(itemId));
            if (n <= 0 || !HasRoomFor(itemId))
                return 0;
            n = inv.RemoveFromSlots(itemId, n);
            Add(itemId, n);
            return n;
        }

        /// "Deposit money pouch": every coin in the pouch goes into the bank. Returns how many.
        public int DepositPouch(Inventory inv)
        {
            if (inv.PouchCoins <= 0 || !HasRoomFor(ItemDefs.Coins.Id))
                return 0;
            int n = inv.TakeFromPouch(inv.PouchCoins);
            Add(ItemDefs.Coins.Id, n);
            return n;
        }

        /// Deposits everything in the inventory. Returns how many items moved.
        public int DepositAll(Inventory inv)
        {
            int moved = 0;
            for (int i = 0; i < Inventory.Size; i++)
                if (!inv[i].IsEmpty)
                    moved += Deposit(inv, inv[i].ItemId, int.MaxValue);
            return moved;
        }

        /// Moves up to `amount` from a bank slot into the inventory, as much as fits
        /// (one free inventory slot per item unless the item stacks). Returns how many moved.
        public int Withdraw(Inventory inv, int slot, long amount)
        {
            if (slot < 0 || slot >= slots.Count || amount <= 0)
                return 0;
            var s = slots[slot];
            int n = (int)Math.Min(Math.Min(amount, s.Count), int.MaxValue);
            n = inv.Add(s.ItemId, n);
            if (n <= 0)
                return 0;
            s.Count -= n;
            if (s.Count <= 0)
                slots.RemoveAt(slot);
            else
                slots[slot] = s;
            Changed?.Invoke();
            return n;
        }

        public void Add(int itemId, long count)
        {
            if (count <= 0)
                return;
            int i = IndexOf(itemId);
            if (i >= 0)
                slots[i] = new BankSlot { ItemId = itemId, Count = slots[i].Count + count };
            else if (slots.Count < Size)
                slots.Add(new BankSlot { ItemId = itemId, Count = count });
            Changed?.Invoke();
        }

        public void Clear()
        {
            slots.Clear();
            Changed?.Invoke();
        }

        public int IndexOf(int itemId)
        {
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].ItemId == itemId)
                    return i;
            return -1;
        }
    }
}
