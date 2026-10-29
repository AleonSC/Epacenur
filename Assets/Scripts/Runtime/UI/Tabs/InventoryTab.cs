using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The backpack: 28 slots and the money pouch under them. Left-click does an item's main verb
    /// (ItemActions), or picks it up to swap with another slot; shift-click drops it; right-click lists
    /// everything. An open bank or shop changes clicks to deposit or sell.
    public sealed class InventoryTab : PanelTab
    {
        public const float PouchHeight = 30f;
        public override string Name => "Inv";

        int selectedSlot = -1;

        public void ClearSelection() => selectedSlot = -1;

        public override void Draw(Hud hud, Rect body)
        {
            var inv = G.Inventory;
            var e = Event.current;
            string tooltip = null;
            for (int i = 0; i < Inventory.Size; i++)
            {
                var slot = new Rect(body.x + (i % 4) * (Gui.Slot + Gui.Gap), body.y + (i / 4) * (Gui.Slot + Gui.Gap), Gui.Slot, Gui.Slot);
                Gui.Fill(slot, i == selectedSlot ? new Color(0.6f, 0.5f, 0.2f, 0.9f) : new Color(0f, 0f, 0f, 0.25f));
                var stack = inv[i];
                if (!stack.IsEmpty)
                {
                    Gui.DrawItemIcon(slot, stack.Def);
                    if (stack.Def.Stackable && stack.Count > 1)
                        Gui.Shadowed(new Rect(slot.x + 2f, slot.y, slot.width, 16f), "<color=#ffff00>" + stack.Count + "</color>", Gui.Small);
                    if (slot.Contains(e.mousePosition))
                        tooltip = stack.Def.Name;
                }
                if (e.type != EventType.MouseDown || !slot.Contains(e.mousePosition))
                    continue;
                if (e.button == 1)
                {
                    if (!stack.IsEmpty)
                        hud.OpenMenu(hud.Window?.InventoryOptions(hud, i) ?? ItemActions.Options(i, Drop), false);
                }
                else if (hud.Window != null && hud.Window.InventoryClick(hud, i, e.shift))
                {
                    // the bank or shop took it
                }
                else if (e.shift)
                {
                    if (!stack.IsEmpty)
                        Drop(i);
                }
                else if (selectedSlot >= 0)
                {
                    // Click one slot, then another, to swap them.
                    if (selectedSlot != i)
                        inv.Swap(selectedSlot, i);
                    selectedSlot = -1;
                }
                else if (!stack.IsEmpty && !ItemActions.Click(i))
                {
                    selectedSlot = i;
                }
                e.Use();
            }
            DrawPouch(hud, new Rect(body.x, body.y + 7 * (Gui.Slot + Gui.Gap), body.width, PouchHeight));
            if (tooltip != null)
                Gui.Tooltip(new Rect(e.mousePosition.x + 14f, e.mousePosition.y - 6f, 200f, 20f), tooltip, Gui.Small);
        }

        void Drop(int slot)
        {
            G.Player.Drop(slot);
            selectedSlot = -1;
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
