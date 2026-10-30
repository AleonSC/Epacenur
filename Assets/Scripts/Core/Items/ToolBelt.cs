using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// RuneScape 3's tool belt: tools added to it leave the backpack for good and are used from
    /// the belt automatically, as if you carried them. They can't be taken off or dropped.
    /// A tool is any item with a ToolType (see ItemDef); the belt can hold every one.
    public sealed class ToolBelt
    {
        readonly HashSet<string> owned = new HashSet<string>();

        public event Action Changed;

        public static bool IsTool(string itemId) => ContentDb.Current.Item(itemId)?.IsTool ?? false;
        public bool Has(string itemId) => owned.Contains(itemId);
        public IEnumerable<string> Owned => owned;

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

        /// Puts a tool straight on the belt (loading a save, a new character's tools).
        public void Add(string itemId)
        {
            if (IsTool(itemId) && owned.Add(itemId))
                Changed?.Invoke();
        }

        public void Clear()
        {
            owned.Clear();
            Changed?.Invoke();
        }

        /// The best tool of a type you have to hand (on the belt or in the backpack) and the levels to use
        /// (when `skills` is given), or null.
        public static ItemDef Find(ToolBelt belt, Inventory inv, string toolType, SkillSet skills = null)
        {
            ItemDef best = null;
            foreach (var tool in ContentDb.Current.Tools)
                if (tool.ToolType == toolType && (best == null || tool.ToolTier > best.ToolTier) && (belt.Has(tool.Id) || inv.Count(tool.Id) > 0)
                    && Requirements.Meets(tool, skills))
                    best = tool;
            return best;
        }

        /// Whether a specific tool item is to hand: on the belt or in the backpack.
        public static bool HasItem(ToolBelt belt, Inventory inv, string itemId) => belt.Has(itemId) || inv.Count(itemId) > 0;
    }
}
