using System;
using System.Collections.Generic;

namespace Epacenur.Core
{
    /// An offer on the trading post: to buy or sell so many of an item at a price each. What it has traded
    /// waits on it to be collected (the items bought, or the coins from selling, plus any coins left over).
    [Serializable]
    public sealed class MarketOffer
    {
        public string item;
        public bool buying;
        public int quantity;     // how many in all
        public int done;         // how many have traded
        public int price;        // each: the most you'll pay, or the least you'll take
        public long held;        // buying: the coins still put by for it; selling: unused (the items left are quantity - done)
        public int collectItems; // waiting to be collected
        public long collectCoins;
        public bool aborted;

        public int Left => aborted ? 0 : quantity - done;
        public bool Finished => aborted || done >= quantity;
        /// Finished and everything taken out: gone from the board.
        public bool Empty => Finished && collectItems == 0 && collectCoins == 0;
    }

    /// The trading post's state, kept with the character: the offers on the board and how the market has
    /// moved for the items players have traded (a price for each item drifts by itself, too: Market.Price).
    [Serializable]
    public sealed class MarketState
    {
        public List<MarketOffer> offers = new List<MarketOffer>();
        public List<string> shiftItems = new List<string>();
        public List<double> shifts = new List<double>();  // each item's push from trading (its price is times e^shift)
        public double minutes;                             // the market's clock
        public long savedAt;                               // when last saved (UTC ticks): offers trade while you're away

        public double Shift(string item)
        {
            int i = shiftItems.IndexOf(item);
            return i >= 0 ? shifts[i] : 0;
        }

        public void SetShift(string item, double v)
        {
            int i = shiftItems.IndexOf(item);
            if (Math.Abs(v) < 1e-4)
            {
                if (i >= 0)
                {
                    shiftItems.RemoveAt(i);
                    shifts.RemoveAt(i);
                }
                return;
            }
            if (i >= 0)
                shifts[i] = v;
            else
            {
                shiftItems.Add(item);
                shifts.Add(v);
            }
        }
    }

    /// The trading post (in every city): put up offers to buy or sell, and they trade over time with the rest of
    /// the world's traders, at the market price or better. Prices follow supply: what's sold in drops in price
    /// and what's bought up rises, easing back over an hour or so; besides that, each item's price wanders on its
    /// own over the days. An offer priced better than the market trades faster (up to 3 times); one priced worse
    /// trades slowly or not at all. Cheap things trade in great numbers, dear ones a few at a time.
    public static class Market
    {
        public const int MaxOffers = 6;
        public const double MinShift = -1.4, MaxShift = 1.1;  // a quarter of normal to three times it
        const double EaseMinutes = 90;                         // trading's push on a price halves over about an hour
        const double PushMinutes = 40;                         // trading this many minutes' worth moves a price by e

        /// Coins can't be traded, nor anything worthless.
        public static bool Tradeable(ItemDef def) => def != null && def.Id != ItemIds.Coins && def.Value > 0;

        /// How many trade a minute at the market price: cheap things by the hundred, dear things one or two.
        public static double Volume(ItemDef def) => Math.Max(0.5, 600.0 / Math.Sqrt(Math.Max(1, def.Value)));

        /// What an item goes for now: its value, times how wanted it is (some items fetch more than their value,
        /// some less), times its slow wander, times how trading has pushed it.
        public static int Price(MarketState s, ItemDef def)
        {
            double p = def.Value * Demand(def.Id) * Wander(def.Id, s.minutes) * Math.Exp(s.Shift(def.Id));
            return (int)Math.Max(1, Math.Min(int.MaxValue / 2, Math.Round(p)));
        }

        /// The price an hour ago, to show which way it's going.
        public static int PriceAgo(MarketState s, ItemDef def, double minutes)
        {
            double p = def.Value * Demand(def.Id) * Wander(def.Id, s.minutes - minutes) * Math.Exp(s.Shift(def.Id) * Math.Exp(minutes / EaseMinutes * Math.Log(2)));
            return (int)Math.Max(1, Math.Min(int.MaxValue / 2, Math.Round(p)));
        }

        static double Demand(string id) => 0.85 + Hash(id, 1) * 0.5;

        /// Each item's own slow wander: within 15% either way, over a few hours of play.
        static double Wander(string id, double minutes)
        {
            var noise = new ValueNoise((int)(Hash(id, 2) * int.MaxValue));
            return 1 + (noise.Fractal(minutes / 240.0, Hash(id, 3) * 100, 2) - 0.5) * 0.3;
        }

        static double Hash(string s, int salt)
        {
            unchecked
            {
                ulong h = 1469598103934665603UL ^ (ulong)salt * 0x9E3779B97F4A7C15UL;
                foreach (char c in s)
                    h = (h ^ c) * 1099511628211UL;
                h ^= h >> 31;
                h *= 0x94D049BB133111EBUL;
                h ^= h >> 29;
                return (h & 0xFFFFFFFFFFFFUL) / (double)0x1000000000000UL;
            }
        }

        /// Puts up an offer to buy: the coins for all of it are taken from your inventory now. Null if it went
        /// up, otherwise why not.
        public static string Buy(MarketState s, Inventory inv, ItemDef def, int quantity, int price)
        {
            string problem = CanOffer(s, def, quantity, price);
            if (problem != null)
                return problem;
            long cost = (long)quantity * price;
            if (cost > inv.Count(ItemIds.Coins))
                return "You don't have enough coins.";
            inv.Use(ItemIds.Coins, (int)cost);
            s.offers.Add(new MarketOffer { item = def.Id, buying = true, quantity = quantity, price = price, held = cost });
            return null;
        }

        /// Puts up an offer to sell: the items are taken from your inventory now. Null if it went up, otherwise
        /// why not.
        public static string Sell(MarketState s, Inventory inv, ItemDef def, int quantity, int price)
        {
            string problem = CanOffer(s, def, quantity, price);
            if (problem != null)
                return problem;
            if (inv.CountInSlots(def.Id) < quantity)
                return "You don't have that many.";
            inv.RemoveFromSlots(def.Id, quantity);
            s.offers.Add(new MarketOffer { item = def.Id, buying = false, quantity = quantity, price = price });
            return null;
        }

        static string CanOffer(MarketState s, ItemDef def, int quantity, int price)
        {
            if (!Tradeable(def))
                return "You can't trade that on the trading post.";
            if (s.offers.Count >= MaxOffers)
                return "You already have " + MaxOffers + " offers up. Collect or abort one first.";
            if (quantity <= 0 || price <= 0)
                return "Set how many and a price first.";
            if ((long)quantity * price > int.MaxValue)
                return "That's too many coins for one offer.";
            return null;
        }

        /// Takes an offer down: what hasn't traded waits to be collected (the coins put by, or the items).
        public static void Abort(MarketOffer o)
        {
            if (o.Finished)
                return;
            if (o.buying)
            {
                o.collectCoins += o.held;
                o.held = 0;
            }
            else
                o.collectItems += o.quantity - o.done;
            o.aborted = true;
        }

        /// Takes what an offer has traded into your inventory (as much as fits), then the rest into the bank
        /// (if given); finished, emptied offers come off the board. Returns how many items and coins came out.
        public static void Collect(MarketState s, MarketOffer o, Inventory inv, Bank bank, out int items, out long coins)
        {
            items = 0;
            coins = 0;
            if (o.collectCoins > 0)
            {
                int c = inv.Add(ItemIds.Coins, (int)Math.Min(o.collectCoins, int.MaxValue));
                o.collectCoins -= c;
                coins += c;
                if (o.collectCoins > 0 && bank != null && bank.HasRoomFor(ItemIds.Coins))
                {
                    bank.Add(ItemIds.Coins, o.collectCoins);
                    coins += o.collectCoins;
                    o.collectCoins = 0;
                }
            }
            if (o.collectItems > 0)
            {
                int n = inv.Add(o.item, o.collectItems);
                o.collectItems -= n;
                items += n;
                if (o.collectItems > 0 && bank != null && bank.HasRoomFor(o.item))
                {
                    bank.Add(o.item, o.collectItems);
                    items += o.collectItems;
                    o.collectItems = 0;
                }
            }
            if (o.Empty)
                s.offers.Remove(o);
        }

        /// The market moves on by `seconds`: offers trade, and trading's push on prices eases. Returns the offers
        /// that finished this time (to tell you).
        public static List<MarketOffer> Advance(MarketState s, double seconds, Random rng, Func<string, ItemDef> items)
        {
            var finished = new List<MarketOffer>();
            double minutes = seconds / 60.0;
            s.minutes += minutes;
            // The push from trading eases back.
            double ease = Math.Exp(-minutes / EaseMinutes * Math.Log(2));
            for (int i = s.shifts.Count - 1; i >= 0; i--)
                s.SetShift(s.shiftItems[i], s.shifts[i] * ease);
            foreach (var o in s.offers)
            {
                if (o.Finished)
                    continue;
                var def = items(o.item);
                if (def == null)
                    continue;
                int market = Price(s, def);
                // How keen the other traders are: 1 at the market price, more the better your price, none when
                // it's much worse (a sixth off the market price).
                double better = o.buying ? (o.price - market) / (double)market : (market - o.price) / (double)market;
                double keen = Math.Max(0, Math.Min(3, 1 + better * 6));
                double expected = Volume(def) * minutes * keen;
                int n = (int)Math.Floor(expected);
                if (rng.NextDouble() < expected - n)
                    n++;
                n = Math.Min(n, o.quantity - o.done);
                if (n <= 0)
                    continue;
                // Trades go through at the market price or better for you.
                if (o.buying)
                {
                    int each = Math.Min(o.price, market);
                    o.held -= (long)o.price * n;
                    o.collectCoins += (long)(o.price - each) * n;
                }
                else
                    o.collectCoins += (long)Math.Max(o.price, market) * n;
                o.done += n;
                o.collectItems += o.buying ? n : 0;
                // Buying pushes the price up, selling pushes it down.
                double push = n / (Volume(def) * PushMinutes) * (o.buying ? 1 : -1);
                s.SetShift(o.item, Math.Max(MinShift, Math.Min(MaxShift, s.Shift(o.item) + push)));
                if (o.Finished)
                {
                    if (o.buying && o.held > 0)
                    {
                        o.collectCoins += o.held;
                        o.held = 0;
                    }
                    finished.Add(o);
                }
            }
            return finished;
        }
    }
}
