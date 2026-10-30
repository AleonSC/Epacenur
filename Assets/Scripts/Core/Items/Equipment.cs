using System;

namespace Epacenur.Core
{
    /// The items you're wearing, one per slot.
    public sealed class Equipment
    {
        public static readonly int SlotCount = Enum.GetValues(typeof(EquipSlot)).Length - 1;
        readonly ItemStack[] worn = new ItemStack[SlotCount];

        public event Action Changed;

        public ItemStack this[EquipSlot slot] => worn[(int)slot];
        public bool IsWearing(EquipSlot slot) => !worn[(int)slot].IsEmpty;

        /// Everything worn, added up.
        public Bonuses Bonuses
        {
            get
            {
                var b = new Bonuses();
                foreach (var s in worn)
                    if (!s.IsEmpty)
                        b = b + s.Def.Bonuses;
                return b;
            }
        }

        /// Ticks between attacks: the weapon's speed, or 4 when unarmed.
        public int AttackTicks => IsWearing(EquipSlot.Weapon) ? this[EquipSlot.Weapon].Def.AttackTicks : 4;

        /// The kind of weapon wielded (its attack styles): "unarmed" with none.
        public string WeaponKind => IsWearing(EquipSlot.Weapon) ? this[EquipSlot.Weapon].Def.WeaponKind ?? WeaponKindIds.Unarmed : WeaponKindIds.Unarmed;

        /// Wears the item in an inventory slot. Whatever was in that equipment slot goes back into the
        /// same inventory slot. A two-handed weapon takes the shield off too (and a shield a two-handed
        /// weapon), which needs a free slot. Returns false if the item can't be worn, with the reason.
        public bool Equip(Inventory inv, int invSlot) => Equip(inv, invSlot, null, out _);

        /// As Equip, checking the item's level requirements against `skills` (null: no check).
        public bool Equip(Inventory inv, int invSlot, SkillSet skills, out string problem)
        {
            problem = null;
            var stack = inv[invSlot];
            if (stack.IsEmpty || stack.Def.Slot == EquipSlot.None)
                return false;
            var def = stack.Def;
            problem = Requirements.Problem(def, skills, def.Slot == EquipSlot.Weapon ? "wield" : "wear");
            if (problem != null)
                return false;
            // More of the ammo already worn joins it.
            if (def.Stackable && this[def.Slot].ItemId == def.Id)
            {
                worn[(int)def.Slot] = new ItemStack(def.Id, this[def.Slot].Count + stack.Count);
                inv.RemoveAt(invSlot);
                Changed?.Invoke();
                return true;
            }
            int e = (int)def.Slot;
            var old = worn[e];
            // The other hand: a two-handed weapon and a shield can't both be worn.
            EquipSlot other = EquipSlot.None;
            if (def.TwoHanded && IsWearing(EquipSlot.Shield))
                other = EquipSlot.Shield;
            else if (def.Slot == EquipSlot.Shield && IsWearing(EquipSlot.Weapon) && this[EquipSlot.Weapon].Def.TwoHanded)
                other = EquipSlot.Weapon;
            var removed = other == EquipSlot.None ? default(ItemStack) : worn[(int)other];
            if (!removed.IsEmpty && inv.FreeSlots + (old.IsEmpty ? 1 : 0) < 1)
            {
                problem = "You don't have enough inventory space to do that.";
                return false;
            }
            worn[e] = stack;
            inv.Set(invSlot, old);
            if (!removed.IsEmpty)
            {
                worn[(int)other] = default(ItemStack);
                inv.Add(removed.ItemId, removed.Count);
            }
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

        /// Uses one of the worn ammo (an arrow fired). Returns false if there was none.
        public bool UseAmmo()
        {
            var ammo = worn[(int)EquipSlot.Ammo];
            if (ammo.IsEmpty)
                return false;
            worn[(int)EquipSlot.Ammo] = ammo.Count > 1 ? new ItemStack(ammo.ItemId, ammo.Count - 1) : default(ItemStack);
            Changed?.Invoke();
            return true;
        }

        /// Why the wielded ranged weapon can't fire right now (no ammo, or ammo it can't use), or null.
        public string AmmoProblem()
        {
            var weapon = IsWearing(EquipSlot.Weapon) ? this[EquipSlot.Weapon].Def : null;
            if (weapon == null || weapon.AmmoType == null)
                return null;
            var ammo = this[EquipSlot.Ammo];
            if (ammo.IsEmpty || ammo.Def.AmmoType != weapon.AmmoType)
                return "There is no ammo left in your quiver.";
            if (ammo.Def.AmmoTier > weapon.AmmoTier)
            {
                string name = ammo.Def.Name.ToLowerInvariant();
                return "You can't use " + name + (name.EndsWith("s") ? "" : "s") + " with " + Making.Article(weapon.Name) + ".";
            }
            return null;
        }

        /// Takes everything off (dying).
        public void Clear()
        {
            for (int i = 0; i < worn.Length; i++)
                worn[i] = default(ItemStack);
            Changed?.Invoke();
        }
    }
}
