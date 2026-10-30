using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// What happens to your things when you die, as in RuneScape: you keep your most valuable items
    /// (3 by default), and everything else you carried or wore goes into a gravestone where you fell.
    /// The money pouch and tool belt are safe.
    public static class Death
    {
        public const int KeptItems = 3;
        public const float GraveSeconds = 15 * 60f;

        /// Splits everything in the inventory and equipment into what's kept and what's lost, counting each
        /// item of a stack separately (so it's the 3 most valuable items, not stacks), and empties both.
        public static void Split(Inventory inv, Equipment eq, int keep, List<ItemStack> kept, List<ItemStack> lost)
        {
            var all = new List<ItemStack>();
            for (int i = 0; i < Inventory.Size; i++)
                if (!inv[i].IsEmpty)
                    all.Add(inv[i]);
            foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
                if (slot != EquipSlot.None && eq.IsWearing(slot))
                    all.Add(eq[slot]);
            // Most valuable first; ties keep their order (inventory before equipment), as a stable sort.
            var order = new List<int>();
            for (int i = 0; i < all.Count; i++)
                order.Add(i);
            order.Sort((a, b) =>
            {
                int c = all[b].Def.Value.CompareTo(all[a].Def.Value);
                return c != 0 ? c : a.CompareTo(b);
            });
            var keptCounts = new int[all.Count];
            int left = keep;
            foreach (int i in order)
            {
                if (left <= 0)
                    break;
                int n = Math.Min(left, all[i].Count);
                keptCounts[i] = n;
                left -= n;
            }
            for (int i = 0; i < all.Count; i++)
            {
                if (keptCounts[i] > 0)
                    kept.Add(new ItemStack(all[i].ItemId, keptCounts[i]));
                if (all[i].Count > keptCounts[i])
                    lost.Add(new ItemStack(all[i].ItemId, all[i].Count - keptCounts[i]));
            }
            for (int i = 0; i < Inventory.Size; i++)
                inv.RemoveAt(i);
            eq.Clear();
        }
    }
}
