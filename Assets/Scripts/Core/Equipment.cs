using System;

namespace Epacenur.Core
{
    /// Where an item is worn (the slots of the equipment tab).
    public enum EquipSlot
    {
        None = -1,
        Head, Cape, Neck, Ammo, Weapon, Body, Shield, Legs, Hands, Feet, Ring,
    }

    /// The items you're wearing, one per slot.
    public sealed class Equipment
    {
        public static readonly int SlotCount = Enum.GetValues(typeof(EquipSlot)).Length - 1;
        readonly ItemStack[] worn = new ItemStack[SlotCount];

        public event Action Changed;

        public ItemStack this[EquipSlot slot] => worn[(int)slot];
        public bool IsWearing(EquipSlot slot) => !worn[(int)slot].IsEmpty;

        public int AttackBonus => Sum(d => d.AttackBonus);
        public int StrengthBonus => Sum(d => d.StrengthBonus);
        public int DefenceBonus => Sum(d => d.DefenceBonus);
        /// Ticks between attacks: the weapon's speed, or 4 when unarmed.
        public int AttackTicks => IsWearing(EquipSlot.Weapon) ? this[EquipSlot.Weapon].Def.AttackTicks : 4;

        /// Wears the item in an inventory slot. Whatever was in that equipment slot goes back into the
        /// same inventory slot. Returns false if the item can't be worn.
        public bool Equip(Inventory inv, int invSlot)
        {
            var stack = inv[invSlot];
            if (stack.IsEmpty || stack.Def.Slot == EquipSlot.None)
                return false;
            int e = (int)stack.Def.Slot;
            var old = worn[e];
            worn[e] = stack;
            inv.Set(invSlot, old);
            Changed?.Invoke();
            return true;
        }

        /// Takes an item off into the inventory. Returns false when the inventory has no room.
        public bool Unequip(EquipSlot slot, Inventory inv)
        {
            var stack = worn[(int)slot];
            if (stack.IsEmpty)
                return true;
            if (!inv.HasRoomFor(stack.ItemId))
                return false;
            inv.Add(stack.ItemId, stack.Count);
            worn[(int)slot] = default(ItemStack);
            Changed?.Invoke();
            return true;
        }

        /// Puts an item straight into a slot (loading a save, dev tools).
        public void Set(EquipSlot slot, ItemStack stack)
        {
            worn[(int)slot] = stack.IsEmpty ? default(ItemStack) : stack;
            Changed?.Invoke();
        }

        int Sum(Func<ItemDef, int> f)
        {
            int n = 0;
            foreach (var s in worn)
                if (!s.IsEmpty)
                    n += f(s.Def);
            return n;
        }
    }
}
