using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// "Use": pick an item in the inventory, then click another item or something in the world to use it
    /// on, as in RuneScape: a tinderbox on logs, raw fish on a fire, ore on a furnace, bones on an altar.
    public sealed partial class PlayerController
    {
        /// The inventory slot being used ("Use Logs ->"), or -1.
        public int UsingSlot { get; private set; } = -1;
        string usingItem;

        /// Whether an item is picked to use (and still in its slot).
        public bool IsUsing => UsingSlot >= 0 && G.Inventory[UsingSlot].ItemId == usingItem && usingItem != null;
        public ItemDef UsingDef => IsUsing ? G.Inventory[UsingSlot].Def : null;

        public void BeginUse(int slot)
        {
            if (G.Inventory[slot].IsEmpty)
                return;
            SelectedSpell = null;
            UsingSlot = slot;
            usingItem = G.Inventory[slot].ItemId;
        }

        /// Puts down the picked item, or picked spell (right-click, Escape, walking off).
        public void CancelUse()
        {
            UsingSlot = -1;
            usingItem = null;
            SelectedSpell = null;
        }

        /// The picked item on another inventory item.
        public void UseOnSlot(int target)
        {
            int from = UsingSlot;
            bool using_ = IsUsing;
            CancelUse();
            if (!using_ || from == target || G.Inventory[target].IsEmpty)
                return;
            ItemActions.UseOnItem(from, target);
        }

        /// World options while an item is picked: "Use Raw shrimps -> Fire" for each thing, then examines.
        void AddUseOptions(Component thing, bool firstPerson, List<MenuOption> options, List<MenuOption> examines)
        {
            string item = usingItem;
            string use = MenuOption.Item(UsingDef.Name) + " -> ";
            if (thing is Interactable obj)
            {
                options.Add(new MenuOption("Use", use + MenuOption.Thing(obj.DisplayName), () =>
                {
                    if (firstPerson)
                    {
                        CancelUse();
                        UseOnObject(obj, item);
                    }
                    else
                    {
                        WalkToObject(obj, t => UseOnObject(t, item));
                    }
                }));
                examines.Add(new MenuOption("Examine", MenuOption.Thing(obj.DisplayName), () => GameLog.Add(obj.Examine)));
            }
            else if (thing is Npc npc && !npc.IsDead)
            {
                options.Add(new MenuOption("Use", use + MenuOption.Npc(npc.HoverName), () =>
                {
                    CancelUse();
                    GameLog.Add("Nothing interesting happens.");
                }));
            }
        }

        /// Uses an item from the inventory on something in the world (you're next to it).
        public void UseOnObject(Interactable thing, string itemId)
        {
            int slot = FindSlot(itemId);
            if (slot < 0 || thing == null)
                return;
            var def = G.Inventory[slot].Def;
            if (thing is StationObject station)
            {
                switch (station.Def.Kind)
                {
                    case StationKind.Bank:
                        // Into the bank, all of them.
                        int n = G.Bank.Deposit(G.Inventory, itemId, int.MaxValue);
                        if (n > 0)
                            GameLog.Add("You deposit " + (n > 1 ? n + " x " : "") + def.Name.ToLowerInvariant() + ".");
                        return;
                    case StationKind.Altar:
                        if (def.BuryXp > 0)
                        {
                            OfferBones(slot);
                            return;
                        }
                        break;
                    case StationKind.Craft:
                        if (ContentDb.Current.RecipeUsing(station.Def.Id, itemId) != null)
                        {
                            StartMaking(station, null, int.MaxValue);
                            return;
                        }
                        break;
                    case StationKind.Make:
                        if (ContentDb.Current.RecipeUsing(station.Def.Id, itemId) != null)
                        {
                            G.Hud.OpenMaking(station, itemId);
                            return;
                        }
                        break;
                }
            }
            else if (thing is NodeObject node && def.ToolType != null && def.ToolType == node.Def.ToolType)
            {
                Gather(node);  // a hatchet on a tree is the same as chopping it
                return;
            }
            GameLog.Add("Nothing interesting happens.");
        }

        /// Bones offered at an altar: 3.5 times the xp of burying them.
        public void OfferBones(int slot)
        {
            var stack = G.Inventory[slot];
            if (stack.IsEmpty || stack.Def.BuryXp <= 0 || buryCooldown > 0f)
                return;
            buryCooldown = Prayers.BuryTicks * GameTime.TickSeconds;
            G.Inventory.RemoveAt(slot);
            G.Skills.AddXp(SkillIds.Prayer, stack.Def.BuryXp * 3.5);
            Kneel(0.8f);
            GameLog.Add("The gods are pleased with your offering.");
        }

        int FindSlot(string itemId)
        {
            for (int i = 0; i < Inventory.Size; i++)
                if (G.Inventory[i].ItemId == itemId)
                    return i;
            return -1;
        }
    }
}
