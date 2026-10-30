using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The backpack: 28 slots and the money pouch under them. Left-click does an item's main verb
    /// (ItemActions), or picks it to Use on something; drag an item onto another slot to swap them;
    /// shift-click drops it; right-click lists everything. An open bank or shop changes clicks to deposit or sell.
    public sealed class InventoryTab : PanelTab
    {
        public const float PouchHeight = 30f;
        public override string Name => "Inv";

        // Left button pressed on a slot: a click if released there, a drag (to swap) if moved first.
        int pressSlot = -1;
        Vector2 pressAt;
        bool dragging;

        public void ClearSelection()
        {
            pressSlot = -1;
            dragging = false;
            G?.Player?.CancelUse();
        }

        static Rect SlotRect(Rect body, int i) => new Rect(body.x + (i % 4) * (Gui.Slot + Gui.Gap), body.y + (i / 4) * (Gui.Slot + Gui.Gap), Gui.Slot, Gui.Slot);

        public override void Draw(Hud hud, Rect body)
        {
            var inv = G.Inventory;
            var e = Event.current;
            var player = G.Player;
            string tooltip = null;
            int over = -1;
            for (int i = 0; i < Inventory.Size; i++)
            {
                var slot = SlotRect(body, i);
                if (slot.Contains(e.mousePosition))
                    over = i;
                bool picked = player.IsUsing && player.UsingSlot == i;
                Gui.Fill(slot, new Color(0f, 0f, 0f, 0.25f));
                var stack = inv[i];
                if (!stack.IsEmpty && !(dragging && i == pressSlot))
                {
                    Gui.DrawItemIcon(slot, stack.Def);
                    if (stack.Def.Stackable && stack.Count > 1)
                        Gui.Shadowed(new Rect(slot.x + 2f, slot.y, slot.width, 16f), "<color=#ffff00>" + stack.Count + "</color>", Gui.Small);
                    if (slot.Contains(e.mousePosition))
                        tooltip = player.SelectedSpell != null ? "Cast " + player.SelectedSpell.Name + " -> " + stack.Def.Name
                            : player.IsUsing && !picked ? "Use " + player.UsingDef.Name + " -> " + stack.Def.Name : stack.Def.Name;
                }
                if (picked)
                {
                    // A white outline round the item being used.
                    Gui.Fill(new Rect(slot.x, slot.y, slot.width, 1f), Color.white);
                    Gui.Fill(new Rect(slot.x, slot.yMax - 1f, slot.width, 1f), Color.white);
                    Gui.Fill(new Rect(slot.x, slot.y, 1f, slot.height), Color.white);
                    Gui.Fill(new Rect(slot.xMax - 1f, slot.y, 1f, slot.height), Color.white);
                }
                if (e.type != EventType.MouseDown || !slot.Contains(e.mousePosition))
                    continue;
                if (e.button == 1)
                {
                    player.CancelUse();
                    if (!stack.IsEmpty)
                        hud.OpenMenu(hud.Window?.InventoryOptions(hud, i) ?? ItemActions.Options(i, Drop), false);
                }
                else if (e.button == 0)
                {
                    pressSlot = i;
                    pressAt = e.mousePosition;
                    dragging = false;
                }
                e.Use();
            }

            if (pressSlot >= 0 && e.type == EventType.MouseDrag)
            {
                if (!dragging && (e.mousePosition - pressAt).magnitude > 6f && !inv[pressSlot].IsEmpty)
                    dragging = true;
                e.Use();
            }
            else if (pressSlot >= 0 && e.type == EventType.MouseUp)
            {
                int from = pressSlot;
                pressSlot = -1;
                if (dragging)
                {
                    dragging = false;
                    if (over >= 0 && over != from)
                        inv.Swap(from, over);  // dragged onto another slot: swap them
                }
                else if (over == from)
                {
                    Click(hud, from, e.shift);
                }
                e.Use();
            }
            if (dragging && pressSlot >= 0 && !inv[pressSlot].IsEmpty)
                Gui.DrawItemIcon(new Rect(e.mousePosition.x - Gui.Slot / 2f, e.mousePosition.y - Gui.Slot / 2f, Gui.Slot, Gui.Slot), inv[pressSlot].Def);

            DrawPouch(hud, new Rect(body.x, body.y + 7 * (Gui.Slot + Gui.Gap), body.width, PouchHeight));
            if (tooltip != null && !dragging)
                Gui.Tooltip(new Rect(e.mousePosition.x + 14f, e.mousePosition.y - 6f, 260f, 20f), tooltip, Gui.Small);
        }

        /// A left-click on a slot: use the picked item on it, let an open bank or shop take it, drop it
        /// (shift), do its main verb, or pick it to use on something.
        void Click(Hud hud, int i, bool shift)
        {
            var player = G.Player;
            if (player.SelectedSpell != null)
            {
                player.CastOnSlot(i);
                return;
            }
            if (player.IsUsing)
            {
                player.UseOnSlot(i);
                return;
            }
            if (G.Inventory[i].IsEmpty)
                return;
            if (hud.Window != null && hud.Window.InventoryClick(hud, i, shift))
                return;
            if (shift)
                Drop(i);
            else if (!ItemActions.Click(i))
                player.BeginUse(i);
        }

        void Drop(int slot)
        {
            G.Player.Drop(slot);
            G.Player.CancelUse();
        }

        // ---- Money pouch ----

        /// The coin count, RuneScape colours: yellow below 100K, white below 10M, green above.
        static string PouchText(int coins)
        {
            string colour = coins < 100000 ? "#ffff00" : coins < 10000000 ? "#ffffff" : "#00ff80";
            return "<color=" + colour + ">" + Experience.Format(coins) + "</color>";
        }

        void DrawPouch(Hud hud, Rect r)
        {
            var e = Event.current;
            Gui.Bevel(r);
            // A little coin stack.
            var gold = new Color(0.95f, 0.8f, 0.25f);
            var dark = new Color(0.7f, 0.55f, 0.1f);
            for (int k = 0; k < 3; k++)
            {
                Gui.Fill(new Rect(r.x + 7f, r.y + 18f - k * 5f, 16f, 5f), dark);
                Gui.Fill(new Rect(r.x + 7f, r.y + 18f - k * 5f, 16f, 3f), gold);
            }
            GUI.Label(new Rect(r.x + 28f, r.y + 6f, 80f, 18f), "Coins", Gui.Small);
            Gui.Shadowed(new Rect(r.x, r.y + 6f, r.width - 8f, 18f), "<b>" + PouchText(G.Inventory.PouchCoins) + "</b>", Gui.RightSmall);

            if (r.Contains(e.mousePosition) && e.type == EventType.MouseDown)
            {
                if (e.button == 1)
                    hud.OpenMenu(PouchOptions(hud), false);
                else if (hud.Window == null || !hud.Window.PouchClick())
                    ExaminePouch();
                e.Use();
            }
        }

        static void ExaminePouch() => GameLog.Add("Your money pouch contains " + Experience.Format(G.Inventory.PouchCoins) + " coins.");

        static List<MenuOption> PouchOptions(Hud hud)
        {
            string name = MenuOption.Item("Money pouch");
            var o = new List<MenuOption>();
            hud.Window?.PouchOptions(o);
            foreach (int a in new[] { 1, 10, 100, 1000, int.MaxValue })
            {
                int n = a;
                o.Add(new MenuOption("Withdraw-" + (n == int.MaxValue ? "All" : Experience.Format(n)), name, () => WithdrawFromPouch(n)));
            }
            o.Add(new MenuOption("Examine", name, ExaminePouch));
            return o;
        }

        /// Coins out of the pouch into the backpack, e.g. to drop them.
        static void WithdrawFromPouch(int amount)
        {
            if (G.Inventory.PouchCoins == 0)
                GameLog.Add("Your money pouch is empty.");
            else if (G.Inventory.WithdrawFromPouch(amount) == 0)
                GameLog.Add("You don't have enough inventory space.");
        }
    }
}
