using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A shop's stock, with prices. Clicking your inventory sells.
    public sealed class ShopWindow : Window
    {
        const int Columns = 8;
        static readonly int[] Amounts = { 1, 5, 10, 50 };
        static int amount = 1;

        readonly Shop shop;

        public ShopWindow(Shop shop) { this.shop = shop; }

        public override void Draw(Hud hud, Rect r)
        {
            var e = Event.current;
            Frame(hud, r, shop.Name);

            // Stock: each item with how many the shop has, and its price under it.
            const float cellH = Gui.Slot + 16f;
            var view = new Rect(r.x + Gui.Pad, r.y + 38f, r.width - Gui.Pad * 2, r.height - 38f - 84f);
            int rows = Mathf.Max(1, (shop.Items.Count + Columns - 1) / Columns);
            var content = new Rect(0f, 0f, view.width - 18f, rows * (cellH + Gui.Gap));
            scroll = GUI.BeginScrollView(view, scroll, content);
            string tooltip = null;
            int buyIndex = -1, buyAmount = 0;
            for (int i = 0; i < shop.Items.Count; i++)
            {
                var item = shop.Items[i];
                var slot = new Rect((i % Columns) * (Gui.Slot + Gui.Gap), (i / Columns) * (cellH + Gui.Gap), Gui.Slot, Gui.Slot);
                int price = Shop.BuyPrice(item.Def);
                Gui.EmptySlot(slot);
                if (slot.Contains(e.mousePosition))
                    Gui.Highlight(slot, false);
                Gui.DrawItemIcon(slot, item.Def);
                Gui.Shadowed(new Rect(slot.x + 2f, slot.y - 1f, slot.width, 16f), (item.Stock > 0 ? "<color=#ffff00>" : "<color=" + Gui.BadHex + ">") + item.Stock + "</color>", Gui.Small);
                Gui.Shadowed(new Rect(slot.x - 4f, slot.yMax, slot.width + 8f, 16f), "<size=11><color=" + Gui.GoldHex + ">" + price + " gp</color></size>", Gui.Centered);
                if (slot.Contains(e.mousePosition))
                    tooltip = item.Def.Name + ": " + price + " coins (" + item.Stock + " in stock)";
                if (e.type == EventType.MouseDown && slot.Contains(e.mousePosition))
                {
                    if (e.button == 1)
                        hud.OpenMenu(Options(item), false);
                    else
                    {
                        buyIndex = i;  // buy after the loop, so the stock list doesn't change mid-draw
                        buyAmount = e.shift ? 50 : amount;
                    }
                    e.Use();
                }
            }
            GUI.EndScrollView();
            if (buyIndex >= 0)
            {
                int bought = shop.Buy(G.Inventory, buyIndex, buyAmount, out string problem);
                if (bought < buyAmount && problem != null)
                    GameLog.Add(problem);
                Sfx.Play(bought > 0 ? Sound.Coins : Sound.Error, 0.7f);
            }

            float y = r.yMax - 76f;
            Divider(r, y - 8f);
            GUI.Label(new Rect(r.x + Gui.Pad, y, 140f, 22f), "Buy / sell:", Gui.Small);
            for (int i = 0; i < Amounts.Length; i++)
            {
                int a = Amounts[i];
                if (GUI.Toggle(new Rect(r.x + Gui.Pad + 120f + i * 46f, y, 42f, 22f), amount == a, a.ToString(), GUI.skin.button))
                    amount = a;
            }
            Gui.Shadowed(new Rect(r.x + Gui.Pad, y + 28f, 160f, 22f), "Your coins: <color=#ffff00>" + Experience.Format(G.Inventory.Count(ItemIds.Coins)) + "</color>", Gui.Label);
            GUI.Label(new Rect(r.x + Gui.Pad + 160f, y + 26f, r.width - Gui.Pad * 2 - 160f, 50f),
                "<color=" + Gui.MutedHex + ">Click: buy / sell that many. Shift: 50 / all\nRight-click: options. Your items: click to sell</color>", Gui.Small);

            if (tooltip != null && view.Contains(e.mousePosition))
                Gui.Tooltip(new Rect(e.mousePosition.x + 14f, e.mousePosition.y - 6f, 300f, 20f), tooltip, Gui.Small);
        }

        List<MenuOption> Options(ShopItem item)
        {
            var def = item.Def;
            string name = MenuOption.Item(def.Name);
            var o = new List<MenuOption> { new MenuOption("Value", name, () => GameLog.Add(def.Name + ": currently costs " + Shop.BuyPrice(def) + " coins.")) };
            foreach (int a in Amounts)
            {
                int n = a;
                o.Add(new MenuOption("Buy " + n, name, () => Buy(def.Id, n)));
            }
            o.Add(new MenuOption("Examine", name, () => GameLog.Add(def.Examine)));
            return o;
        }

        void Buy(string itemId, int n)
        {
            int index = shop.IndexOf(itemId);
            if (index >= 0 && shop.Buy(G.Inventory, index, n, out string problem) < n && problem != null)
                GameLog.Add(problem);
        }

        /// n 0: just say what the shop would pay.
        void Sell(ItemDef def, int n)
        {
            if (n == 0 || !shop.CanSell(def))
            {
                GameLog.Add(shop.CanSell(def) ? def.Name + ": shop will buy for " + shop.SellPrice(def) + " coins." : "You can't sell this item to this shop.");
                return;
            }
            int sold = shop.Sell(G.Inventory, def.Id, n, out string problem);
            if (sold == 0 && problem != null)
                GameLog.Add(problem);
            Sfx.Play(sold > 0 ? Sound.Coins : Sound.Error, 0.7f);
        }

        public override bool InventoryClick(Hud hud, int slot, bool shift)
        {
            var stack = G.Inventory[slot];
            if (!stack.IsEmpty)
                Sell(stack.Def, shift ? int.MaxValue : amount);
            return true;
        }

        public override List<MenuOption> InventoryOptions(Hud hud, int slot)
        {
            var def = G.Inventory[slot].Def;
            string name = MenuOption.Item(def.Name);
            var o = new List<MenuOption> { new MenuOption("Value", name, () => Sell(def, 0)) };
            foreach (int a in Amounts)
            {
                int n = a;
                o.Add(new MenuOption("Sell " + n, name, () => Sell(def, n)));
            }
            o.Add(new MenuOption("Examine", name, () => GameLog.Add(def.Examine)));
            return o;
        }
    }
}
