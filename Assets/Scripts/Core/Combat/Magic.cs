using System;

namespace Epacenur.Core
{
    /// The rules for casting: levels, runes (a staff stands in for its element's runes), alchemy.
    public static class Magic
    {
        static int Have(string rune, Inventory inv, Equipment eq)
        {
            var weapon = eq.IsWearing(EquipSlot.Weapon) ? eq[EquipSlot.Weapon].Def : null;
            if (weapon != null && weapon.InfiniteRune == rune)
                return int.MaxValue;
            return inv.Count(rune);
        }

        /// Why the spell can't be cast now (level or runes), or null.
        public static string Problem(SpellDef spell, SkillSet skills, Inventory inv, Equipment eq)
        {
            if (skills.GetLevel(SkillIds.Magic) < spell.Level)
                return "You need a Magic level of " + spell.Level + " to cast " + spell.Name + ".";
            foreach (var r in spell.Runes)
                if (Have(r.ItemId, inv, eq) < r.Count)
                    return "You do not have enough " + ContentDb.Current.Item(r.ItemId).Name + "s to cast this spell.";
            return null;
        }

        /// Uses up the runes (none of a staff's element).
        public static void UseRunes(SpellDef spell, Inventory inv, Equipment eq)
        {
            foreach (var r in spell.Runes)
                if (Have(r.ItemId, inv, eq) != int.MaxValue)
                    inv.Use(r.ItemId, r.Count);
        }

        /// Casts an alchemy spell on one of the item in a slot: it turns into coins (into the pouch), the runes
        /// are used up and Magic xp given. Returns the coins, or -1 with the reason it can't be cast.
        public static int Alch(SpellDef spell, SkillSet skills, Inventory inv, Equipment eq, int slot, out string problem)
        {
            var stack = inv[slot];
            problem = stack.IsEmpty ? "There's nothing there." : stack.ItemId == ItemIds.Coins ? "Coins are already made of gold!" : null;
            if (problem != null)
                return -1;
            // Take the item first: if it's one of the spell's runes, the last one can't pay for itself.
            inv.Set(slot, stack.Count > 1 ? new ItemStack(stack.ItemId, stack.Count - 1) : default(ItemStack));
            problem = Problem(spell, skills, inv, eq);
            if (problem != null)
            {
                inv.Set(slot, stack);
                return -1;
            }
            UseRunes(spell, inv, eq);
            int coins = AlchValue(spell, stack.Def);
            if (coins > 0)
                inv.Add(ItemIds.Coins, coins);
            skills.AddXp(SkillIds.Magic, spell.Xp);
            return coins;
        }

        /// Coins an alchemy spell pays for an item (none for coins themselves).
        public static int AlchValue(SpellDef spell, ItemDef item) =>
            item == null || item.Id == ItemIds.Coins ? 0 : (int)Math.Floor(item.Value * spell.Value);
    }
}
