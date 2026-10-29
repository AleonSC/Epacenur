using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The bank: one slot per kind of item, with the whole stack in it. Clicking your inventory deposits.
    public sealed class BankWindow : Window
    {
        const int Columns = 8;
        static readonly int[] Amounts = { 1, 5, 10, int.MaxValue };  // int.MaxValue = All
        static int amount = 1;  // remembered between visits

        public override void Draw(Hud hud, Rect r)
        {
            var bank = G.Bank;
            var e = Event.current;
            Frame(hud, r, "Bank of Epacenur");
            Gui.Shadowed(new Rect(r.x + r.width - 140f, r.y + 7f, 100f, 20f), bank.UsedSlots + " / " + Bank.Size, Gui.RightSmall);

            var view = new Rect(r.x + Gui.Pad, r.y + 38f, r.width - Gui.Pad * 2, r.height - 38f - 84f);
            int rows = Mathf.Max(1, (bank.UsedSlots + Columns - 1) / Columns);
            var content = new Rect(0f, 0f, view.width - 18f, rows * (Gui.Slot + Gui.Gap));
            scroll = GUI.BeginScrollView(view, scroll, content);
            string tooltip = null;
            int withdrawSlot = -1;
            long withdrawAmount = 0;
            for (int i = 0; i < bank.UsedSlots; i++)
            {
                var slot = new Rect((i % Columns) * (Gui.Slot + Gui.Gap), (i / Columns) * (Gui.Slot + Gui.Gap), Gui.Slot, Gui.Slot);
                var s = bank[i];
                Gui.Fill(slot, new Color(0f, 0f, 0f, 0.25f));
                Gui.DrawItemIcon(slot, s.Def);
                Gui.Shadowed(new Rect(slot.x + 2f, slot.y - 1f, slot.width, 16f), Gui.Count(s.Count), Gui.Small);
                if (slot.Contains(e.mousePosition))
                    tooltip = s.Def.Name + " x " + Experience.Format(s.Count);
                if (e.type == EventType.MouseDown && slot.Contains(e.mousePosition))
                {
                    if (e.button == 1)
                        hud.OpenMenu(Options(s), false);
                    else
                    {
                        withdrawSlot = i;  // withdraw after the loop, so the slot list doesn't change mid-draw
                        withdrawAmount = e.shift ? long.MaxValue : amount;
                    }
                    e.Use();
                }
            }
            if (bank.UsedSlots == 0)
                GUI.Label(new Rect(0f, 8f, content.width, 40f), "The bank is empty. Click items in your inventory to deposit them.", Gui.Rich);
            GUI.EndScrollView();
            if (withdrawSlot >= 0 && bank.Withdraw(G.Inventory, withdrawSlot, withdrawAmount) == 0 && G.Inventory.IsFull)
                GameLog.Add("You don't have enough inventory space.");

            // Amount buttons and deposit-all.
            float y = r.yMax - 76f;
            GUI.Label(new Rect(r.x + Gui.Pad, y, 140f, 22f), "Withdraw / deposit:", Gui.Small);
            for (int i = 0; i < Amounts.Length; i++)
            {
                int a = Amounts[i];
                if (GUI.Toggle(new Rect(r.x + Gui.Pad + 120f + i * 46f, y, 42f, 22f), amount == a, a == int.MaxValue ? "All" : a.ToString(), GUI.skin.button))
                    amount = a;
            }
            if (GUI.Button(new Rect(r.x + Gui.Pad, y + 26f, 150f, 22f), "Deposit inventory"))
                bank.DepositAll(G.Inventory);
            if (GUI.Button(new Rect(r.x + Gui.Pad, y + 51f, 150f, 22f), "Deposit money pouch"))
                bank.DepositPouch(G.Inventory);
            GUI.Label(new Rect(r.x + Gui.Pad + 160f, y + 26f, r.width - Gui.Pad * 2 - 160f, 50f),
                "Click: withdraw / deposit that many\nShift-click: all of it. Right-click: options", Gui.Small);

            if (tooltip != null && view.Contains(e.mousePosition))
                Gui.Tooltip(new Rect(e.mousePosition.x + 14f, e.mousePosition.y - 6f, 260f, 20f), tooltip, Gui.Small);
        }

        static List<MenuOption> Options(BankSlot s)
        {
            var def = s.Def;
            string name = MenuOption.Item(def.Name);
            var o = new List<MenuOption>();
            foreach (int a in Amounts)
            {
                long n = a == int.MaxValue ? long.MaxValue : a;
                o.Add(new MenuOption("Withdraw-" + (a == int.MaxValue ? "All" : a.ToString()), name, () => Withdraw(def.Id, n)));
            }
            o.Add(new MenuOption("Examine", name, () => GameLog.Add(def.Examine)));
            return o;
        }

        static void Withdraw(string itemId, long n)
        {
            int index = G.Bank.IndexOf(itemId);
            if (index >= 0 && G.Bank.Withdraw(G.Inventory, index, n) == 0 && G.Inventory.IsFull)
                GameLog.Add("You don't have enough inventory space.");
        }

        public override bool InventoryClick(Hud hud, int slot, bool shift)
        {
            var stack = G.Inventory[slot];
            if (!stack.IsEmpty)
                G.Bank.Deposit(G.Inventory, stack.ItemId, shift ? int.MaxValue : amount);
            return true;
        }

        public override List<MenuOption> InventoryOptions(Hud hud, int slot)
        {
            var def = G.Inventory[slot].Def;
            string name = MenuOption.Item(def.Name);
            var o = new List<MenuOption>();
            foreach (int a in Amounts)
            {
                int n = a;
                o.Add(new MenuOption("Deposit-" + (n == int.MaxValue ? "All" : n.ToString()), name, () => G.Bank.Deposit(G.Inventory, def.Id, n)));
            }
            o.Add(new MenuOption("Examine", name, () => GameLog.Add(def.Examine)));
            return o;
        }

        public override bool PouchClick()
        {
            G.Bank.DepositPouch(G.Inventory);
            return true;
        }

        public override void PouchOptions(List<MenuOption> options) =>
            options.Add(new MenuOption("Deposit", MenuOption.Item("Money pouch"), () => G.Bank.DepositPouch(G.Inventory)));
    }
}
