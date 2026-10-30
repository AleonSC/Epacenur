using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The trading post: your offers on the board (left), and a form to put up a new one (right). Buy: search
    /// for an item. Sell: click it in your inventory. Then set how many and the price each, and confirm.
    public sealed class TradingPostWindow : Window
    {
        const string SearchField = "TradingSearch";
        const float RowH = 58f;

        MarketState M => G.Trading;

        ItemDef item;         // the item the form is for
        bool buying;
        string query = "", qtyText = "1", priceText = "";
        bool searching;       // picking an item to buy
        bool focus;
        Vector2 resultScroll;

        public override Rect Place(Hud hud)
        {
            float w = Mathf.Min(hud.W - SidePanel.Width - 40f, 700f), h = Mathf.Min(hud.H - 120f, 470f);
            return new Rect(Mathf.Max(10f, (hud.W - SidePanel.Width - 20f - w) / 2f), Mathf.Max(60f, (hud.H - h) / 2f - 50f), w, h);
        }

        public override void Draw(Hud hud, Rect r)
        {
            Frame(hud, r, "Trading Post");
            float split = r.x + Gui.Pad + 330f;
            DrawOffers(new Rect(r.x + Gui.Pad, r.y + 38f, split - r.x - Gui.Pad - 8f, r.height - 46f));
            Gui.Fill(new Rect(split, r.y + 38f, 1f, r.height - 46f), Gui.TrimDim);
            DrawForm(new Rect(split + 10f, r.y + 38f, r.xMax - split - 10f - Gui.Pad, r.height - 46f));
        }

        // ---- The board: your offers ----

        void DrawOffers(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y, r.width, 20f), "Your offers (" + M.offers.Count + " / " + Core.Market.MaxOffers + ")", Gui.Title);
            MarketOffer collect = null, abort = null;
            for (int i = 0; i < Core.Market.MaxOffers; i++)
            {
                var row = new Rect(r.x, r.y + 26f + i * (RowH + 4f), r.width, RowH);
                Gui.Fill(row, Gui.Well);
                if (i >= M.offers.Count)
                {
                    GUI.Label(new Rect(row.x + 8f, row.y + 18f, row.width - 16f, 20f), "<color=" + Gui.MutedHex + ">Empty slot</color>", Gui.Small);
                    continue;
                }
                var o = M.offers[i];
                var def = ContentDb.Current.Item(o.item);
                var slot = new Rect(row.x + 6f, row.y + 6f, RowH - 12f, RowH - 12f);
                Gui.EmptySlot(slot);
                Gui.DrawItemIcon(slot, def);
                string verb = o.buying ? "<color=" + Gui.BlueHex + ">Buying</color>" : "<color=" + Gui.GoldHex + ">Selling</color>";
                GUI.Label(new Rect(slot.xMax + 8f, row.y + 3f, row.width - slot.width - 90f, 20f),
                    verb + " " + Experience.Format(o.quantity) + " x " + (def?.Name ?? o.item), Gui.Small);
                GUI.Label(new Rect(slot.xMax + 8f, row.y + 18f, row.width - slot.width - 90f, 20f),
                    "<color=" + Gui.MutedHex + ">" + Experience.Format(o.price) + " gp each</color>", Gui.Small);
                var bar = new Rect(slot.xMax + 8f, row.y + 38f, row.width - slot.width - 96f, 12f);
                var colour = o.aborted ? new Color(0.6f, 0.2f, 0.15f) : o.Finished ? new Color(0.25f, 0.6f, 0.2f) : new Color(0.75f, 0.6f, 0.2f);
                Gui.Bar(bar, o.quantity > 0 ? o.done / (float)o.quantity : 0f, colour, Gui.Panel,
                    o.aborted ? "Aborted" : Experience.Format(o.done) + " / " + Experience.Format(o.quantity));
                float bx = row.xMax - 78f;
                bool waiting = o.collectItems > 0 || o.collectCoins > 0;
                if (waiting && GUI.Button(new Rect(bx, row.y + 6f, 72f, 22f), "Collect"))
                    collect = o;
                if (!o.Finished && GUI.Button(new Rect(bx, row.y + 31f, 72f, 22f), "Abort"))
                    abort = o;
                if (row.Contains(Event.current.mousePosition) && waiting)
                    Gui.Tooltip(new Rect(Event.current.mousePosition.x + 14f, Event.current.mousePosition.y - 6f, 260f, 20f),
                        "To collect: " + (o.collectItems > 0 ? Experience.Format(o.collectItems) + " x " + def?.Name : "")
                        + (o.collectItems > 0 && o.collectCoins > 0 ? " and " : "") + (o.collectCoins > 0 ? Experience.Format(o.collectCoins) + " coins" : ""), Gui.Small);
            }
            if (collect != null)
            {
                Core.Market.Collect(M, collect, G.Inventory, G.Bank, out int items, out long coins);
                bool left = collect.collectItems > 0 || collect.collectCoins > 0;
                if (items == 0 && coins == 0)
                    GameLog.Add("You don't have room to collect that.");
                else if (left)
                    GameLog.Add("You collect what you can carry; the rest waits on the board.");
                Sfx.Play(items > 0 || coins > 0 ? Sound.Coins : Sound.Error, 0.7f);
            }
            if (abort != null)
            {
                Core.Market.Abort(abort);
                GameLog.Add("You abort your offer. Collect what's left of it from the board.");
                Sfx.Play(Sound.Click, 0.6f);
            }
        }

        // ---- The form: a new offer ----

        void DrawForm(Rect r)
        {
            float y = r.y;
            if (GUI.Toggle(new Rect(r.x, y, 110f, 24f), searching, "Buy...", GUI.skin.button) != searching)
            {
                searching = !searching;
                focus = searching;
                if (searching)
                    item = null;
            }
            GUI.Label(new Rect(r.x + 120f, y + 3f, r.width - 120f, 22f), "<color=" + Gui.MutedHex + ">Sell: click an item in your inventory.</color>", Gui.Small);
            y += 32f;
            if (searching)
            {
                DrawSearch(new Rect(r.x, y, r.width, r.yMax - y));
                return;
            }
            if (item == null)
            {
                GUI.Label(new Rect(r.x, y, r.width, 120f),
                    "<color=" + Gui.MutedHex + ">Put up an offer to buy or sell, and other traders across the land fill it over time: "
                    + "at the market price or better for you. Price it above the market (to buy) or below it (to sell) and it trades faster. "
                    + "Prices fall when lots is sold and rise when lots is bought.\n\nCome back to collect what's traded. Offers keep trading while you're away.</color>",
                    Gui.Rich);
                return;
            }

            int market = Core.Market.Price(M, item), before = Core.Market.PriceAgo(M, item, 60);
            var slot = new Rect(r.x, y, Gui.Slot, Gui.Slot);
            Gui.EmptySlot(slot);
            Gui.DrawItemIcon(slot, item);
            GUI.Label(new Rect(slot.xMax + 10f, y, r.width - slot.width - 10f, 22f), (buying ? "Buy " : "Sell ") + item.Name, Gui.Title);
            string trend = market > before ? " <color=" + Gui.GoodHex + ">rising</color>" : market < before ? " <color=" + Gui.BadHex + ">falling</color>" : "";
            Gui.Shadowed(new Rect(slot.xMax + 10f, y + 22f, r.width - slot.width - 10f, 20f),
                "Market price: <color=#ffff00>" + Experience.Format(market) + "</color> gp" + trend, Gui.Small);
            y += Gui.Slot + 14f;

            // How many.
            GUI.Label(new Rect(r.x, y + 3f, 80f, 22f), "Quantity:", Gui.Small);
            qtyText = Digits(GUI.TextField(new Rect(r.x + 80f, y, 90f, 24f), qtyText, 10));
            int have = buying ? 0 : G.Inventory.CountInSlots(item.Id);
            int[] steps = { 1, 10, 100 };
            for (int i = 0; i < steps.Length; i++)
                if (GUI.Button(new Rect(r.x + 178f + i * 40f, y, 36f, 24f), "+" + steps[i]))
                    qtyText = (Parse(qtyText) + steps[i]).ToString();
            if (!buying && GUI.Button(new Rect(r.x + 178f + 3 * 40f, y, 40f, 24f), "All"))
                qtyText = have.ToString();
            y += 32f;

            // The price each.
            GUI.Label(new Rect(r.x, y + 3f, 80f, 22f), "Price each:", Gui.Small);
            priceText = Digits(GUI.TextField(new Rect(r.x + 80f, y, 90f, 24f), priceText, 10));
            if (GUI.Button(new Rect(r.x + 178f, y, 44f, 24f), "-5%"))
                priceText = Mathf.Max(1, Mathf.FloorToInt(Parse(priceText) * 0.95f)).ToString();
            if (GUI.Button(new Rect(r.x + 226f, y, 60f, 24f), "Market"))
                priceText = market.ToString();
            if (GUI.Button(new Rect(r.x + 290f, y, 44f, 24f), "+5%"))
                priceText = Mathf.Max(Parse(priceText) + 1, Mathf.CeilToInt(Parse(priceText) * 1.05f)).ToString();
            y += 34f;

            int qty = Parse(qtyText), price = Parse(priceText);
            long total = (long)qty * price;
            Gui.Shadowed(new Rect(r.x, y, r.width, 20f), (buying ? "Total cost: " : "You'll get at least: ") + "<color=#ffff00>" + Experience.Format(total) + "</color> coins", Gui.Label);
            y += 22f;
            string speed = price <= 0 ? "" : SpeedHint(market, price);
            GUI.Label(new Rect(r.x, y, r.width, 20f), "<color=" + Gui.MutedHex + ">" + speed + "</color>", Gui.Small);
            if (!buying)
                GUI.Label(new Rect(r.x, y + 16f, r.width, 20f), "<color=" + Gui.MutedHex + ">You have " + Experience.Format(have) + ".</color>", Gui.Small);
            y += 44f;

            if (GUI.Button(new Rect(r.x, y, 150f, 30f), "Confirm offer"))
            {
                string problem = buying ? Core.Market.Buy(M, G.Inventory, item, qty, price) : Core.Market.Sell(M, G.Inventory, item, qty, price);
                if (problem != null)
                {
                    GameLog.Add(problem);
                    Sfx.Play(Sound.Error, 0.7f);
                }
                else
                {
                    GameLog.Add("You put up an offer to " + (buying ? "buy " : "sell ") + Experience.Format(qty) + " x " + item.Name + " at " + Experience.Format(price) + " gp each.");
                    Sfx.Play(Sound.Coins, 0.7f);
                    item = null;
                }
            }
            if (item != null && GUI.Button(new Rect(r.x + 160f, y, 80f, 30f), "Cancel"))
                item = null;
        }

        string SpeedHint(int market, int price)
        {
            double better = buying ? (price - market) / (double)market : (market - price) / (double)market;
            return better >= 0.3 ? "This should trade very quickly." : better >= 0.05 ? "This should trade quickly."
                : better > -0.05 ? "This should trade at a steady pace." : better > -0.16 ? "This will trade slowly." : "Nobody will trade at that price.";
        }

        void DrawSearch(Rect r)
        {
            GUI.Label(new Rect(r.x, r.y + 3f, 60f, 20f), "Search:", Gui.Small);
            GUI.SetNextControlName(SearchField);
            query = GUI.TextField(new Rect(r.x + 60f, r.y, r.width - 60f, 24f), query ?? "", 30);
            if (focus)
            {
                GUI.FocusControl(SearchField);
                focus = false;
            }
            var hits = new List<ItemDef>();
            string q = query.Trim().ToLowerInvariant();
            if (q.Length > 0)
                foreach (var d in ContentDb.Current.Items)
                    if (Core.Market.Tradeable(d) && d.Name.ToLowerInvariant().Contains(q))
                        hits.Add(d);
            hits.Sort((a, b) => a.Name.Length != b.Name.Length ? a.Name.Length.CompareTo(b.Name.Length) : string.CompareOrdinal(a.Name, b.Name));
            var view = new Rect(r.x, r.y + 32f, r.width, r.height - 32f);
            if (q.Length == 0)
            {
                GUI.Label(view, "<color=" + Gui.MutedHex + ">Type part of an item's name.</color>", Gui.Small);
                return;
            }
            if (hits.Count == 0)
            {
                GUI.Label(view, "<color=" + Gui.MutedHex + ">Nothing tradeable by that name.</color>", Gui.Small);
                return;
            }
            const float h = 38f;
            var content = new Rect(0f, 0f, view.width - 18f, hits.Count * (h + 2f));
            resultScroll = GUI.BeginScrollView(view, resultScroll, content);
            ItemDef pick = null;
            for (int i = 0; i < hits.Count; i++)
            {
                var row = new Rect(0f, i * (h + 2f), content.width, h);
                if (row.Contains(Event.current.mousePosition))
                    Gui.Fill(row, Gui.Hover);
                Gui.DrawItemIcon(new Rect(row.x + 2f, row.y + 2f, h - 4f, h - 4f), hits[i]);
                GUI.Label(new Rect(row.x + h + 6f, row.y + 3f, row.width - h - 6f, 18f), hits[i].Name, Gui.Label);
                GUI.Label(new Rect(row.x + h + 6f, row.y + 19f, row.width - h - 6f, 18f),
                    "<color=" + Gui.MutedHex + ">" + Experience.Format(Core.Market.Price(M, hits[i])) + " gp</color>", Gui.Small);
                if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition))
                {
                    pick = hits[i];
                    Event.current.Use();
                }
            }
            GUI.EndScrollView();
            if (pick != null)
                Choose(pick, true);
        }

        void Choose(ItemDef def, bool buy)
        {
            item = def;
            buying = buy;
            searching = false;
            int market = Core.Market.Price(M, def);
            priceText = market.ToString();
            qtyText = buy ? "1" : G.Inventory.CountInSlots(def.Id).ToString();
            Sfx.Play(Sound.Click, 0.4f);
        }

        static string Digits(string s)
        {
            var b = new System.Text.StringBuilder();
            foreach (char c in s)
                if (c >= '0' && c <= '9')
                    b.Append(c);
            return b.ToString();
        }

        static int Parse(string s) => int.TryParse(s, out int n) ? Mathf.Max(0, n) : 0;

        public override bool InventoryClick(Hud hud, int slot, bool shift)
        {
            var stack = G.Inventory[slot];
            if (stack.IsEmpty)
                return true;
            if (!Core.Market.Tradeable(stack.Def))
            {
                GameLog.Add("You can't trade that on the trading post.");
                return true;
            }
            Choose(stack.Def, false);
            return true;
        }

        public override List<MenuOption> InventoryOptions(Hud hud, int slot)
        {
            var def = G.Inventory[slot].Def;
            string name = MenuOption.Item(def.Name);
            var o = new List<MenuOption>();
            if (Core.Market.Tradeable(def))
                o.Add(new MenuOption("Offer", name, () => Choose(def, false)));
            o.Add(new MenuOption("Examine", name, () => GameLog.Add(def.Examine)));
            return o;
        }
    }
}
