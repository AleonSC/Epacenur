using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// RuneScape 3's tool belt: tools added to it leave the backpack for good and are used from
    /// the belt automatically, as if you carried them. They can't be taken off or dropped.
    public sealed class ToolBelt
    {
        /// Every tool the belt can hold, in display order. To add one: put its ItemDef here.
        public static readonly ItemDef[] Tools =
        {
            ItemDefs.BronzeHatchet, ItemDefs.BronzePickaxe, ItemDefs.Hammer, ItemDefs.Tinderbox, ItemDefs.SmallFishingNet,
        };

        readonly HashSet<int> owned = new HashSet<int>();

        public event Action Changed;

        public static bool IsTool(int itemId) => Array.Exists(Tools, t => t.Id == itemId);
        public bool Has(int itemId) => owned.Contains(itemId);
        public IEnumerable<int> Owned => owned;

        /// Moves a tool from an inventory slot onto the belt. Returns false (and why) if it can't.
        public bool AddFromInventory(Inventory inv, int slot, out string problem)
        {
            problem = null;
            var stack = inv[slot];
            if (stack.IsEmpty || !IsTool(stack.ItemId))
            {
                problem = "That can't go on your tool belt.";
                return false;
            }
            if (Has(stack.ItemId))
            {
                problem = "That is already on your tool belt.";
                return false;
            }
            inv.RemoveAt(slot);
            owned.Add(stack.ItemId);
            Changed?.Invoke();
            return true;
        }

        /// Puts a tool straight on the belt (loading a save).
        public void Add(int itemId)
        {
            if (IsTool(itemId) && owned.Add(itemId))
                Changed?.Invoke();
        }

        public void Clear()
        {
            owned.Clear();
            Changed?.Invoke();
        }

        /// Whether you have a tool to hand: on the belt or in the backpack.
        public static bool HasTool(ToolBelt belt, Inventory inv, int itemId) => belt.Has(itemId) || inv.Count(itemId) > 0;
    }
}
