using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Epacenur.Core
{
    /// One page of the encyclopedia: a title, what kind of thing it is, lines of text ("## " starts a heading),
    /// and the pages it mentions (See also).
    public sealed class EncyclopediaEntry
    {
        public string Id, Title, Category;
        public Icon Icon;
        public readonly List<string> Lines = new List<string>();
        public readonly List<string> Links = new List<string>();
        internal string TitleLower, TextLower;

        internal void Line(string text) => Lines.Add(text);
        internal void Heading(string text) => Lines.Add("## " + text);

        internal void Link(string id)
        {
            if (id != Id && !Links.Contains(id))
                Links.Add(id);
        }
    }

    /// A search result: the page, how well it matched, and a line from it that mentions what was searched for.
    public struct EncyclopediaHit
    {
        public EncyclopediaEntry Entry;
        public int Score;
        public string Snippet;
    }

    /// The in-game encyclopedia: a page for everything in the game, made from the content itself (every item,
    /// skill, monster, thing to gather, station, spell, prayer, ability, building piece, agility course and shop,
    /// each saying how it ties to the rest: what an item is made from and into, where it's gathered, who drops
    /// it and who sells it), and guides to how things work. Search finds any word on any page.
    public sealed class Encyclopedia
    {
        public readonly List<EncyclopediaEntry> Entries = new List<EncyclopediaEntry>();
        readonly Dictionary<string, EncyclopediaEntry> byId = new Dictionary<string, EncyclopediaEntry>();

        /// Every category, in the order pages are listed.
        public static readonly string[] Categories =
            { "Guide", "Skill", "Item", "Monster", "Gathering", "Station", "Spell", "Prayer", "Ability", "Building", "Agility course", "Shop" };

        public EncyclopediaEntry Get(string id) => id != null && byId.TryGetValue(id, out var e) ? e : null;

        public static string ItemId(string id) => "item:" + id;
        public static string SkillId(string id) => "skill:" + id;

        EncyclopediaEntry Add(string id, string title, string category, Icon icon = null)
        {
            var e = new EncyclopediaEntry { Id = id, Title = title, Category = category, Icon = icon };
            Entries.Add(e);
            byId[id] = e;
            return e;
        }

        // ---- Searching ----

        /// The pages that mention every word of `query`, best first: a title that is the query, then titles that
        /// start with it or contain it, then pages that mention it most. At most `max`.
        public List<EncyclopediaHit> Search(string query, int max = 80)
        {
            var hits = new List<EncyclopediaHit>();
            string q = (query ?? "").Trim().ToLowerInvariant();
            var words = Words(q);
            if (words.Count == 0)
                return hits;
            foreach (var e in Entries)
            {
                bool all = true;
                foreach (var w in words)
                    if (!e.TitleLower.Contains(w) && !e.TextLower.Contains(w) && !e.Category.ToLowerInvariant().Contains(w))
                    {
                        all = false;
                        break;
                    }
                if (!all)
                    continue;
                int score = e.TitleLower == q ? 1000 : e.TitleLower.StartsWith(q) ? 500 : e.TitleLower.Contains(q) ? 300 : 0;
                foreach (var w in words)
                {
                    if (e.TitleLower.Contains(w))
                        score += e.TitleLower.StartsWith(w) || e.TitleLower.Contains(" " + w) ? 70 : 50;
                    score += Math.Min(30, Count(e.TextLower, w) * 3);
                }
                if (e.TextLower.Contains(q))
                    score += 15;  // the words together, as typed
                hits.Add(new EncyclopediaHit { Entry = e, Score = score, Snippet = Snippet(e, q, words[0]) });
            }
            hits.Sort((a, b) => a.Score != b.Score ? b.Score.CompareTo(a.Score) : string.CompareOrdinal(a.Entry.Title, b.Entry.Title));
            if (hits.Count > max)
                hits.RemoveRange(max, hits.Count - max);
            return hits;
        }

        /// Every page in a category, by title.
        public List<EncyclopediaEntry> InCategory(string category) =>
            Entries.Where(e => e.Category == category).OrderBy(e => e.Title, StringComparer.Ordinal).ToList();

        static List<string> Words(string q)
        {
            var words = new List<string>();
            foreach (var w in q.Split(new[] { ' ', ',', '.', '-', '\'', '(', ')', ':', ';', '/', '?', '!' }, StringSplitOptions.RemoveEmptyEntries))
                if (!words.Contains(w))
                    words.Add(w);
            return words;
        }

        static int Count(string text, string word)
        {
            int n = 0;
            for (int i = text.IndexOf(word, StringComparison.Ordinal); i >= 0; i = text.IndexOf(word, i + word.Length, StringComparison.Ordinal))
                n++;
            return n;
        }

        /// The first line that mentions the query (or its first word), cut down round it.
        static string Snippet(EncyclopediaEntry e, string q, string word)
        {
            foreach (var key in new[] { q, word })
                foreach (var line in e.Lines)
                {
                    int at = line.ToLowerInvariant().IndexOf(key, StringComparison.Ordinal);
                    if (at < 0)
                        continue;
                    string text = line.StartsWith("## ") ? line.Substring(3) : line;
                    at = Math.Max(0, at - (line.Length - text.Length));
                    int from = Math.Max(0, at - 40), to = Math.Min(text.Length, at + key.Length + 60);
                    return (from > 0 ? "..." : "") + text.Substring(from, to - from) + (to < text.Length ? "..." : "");
                }
            return e.Lines.Count > 0 && !e.Lines[0].StartsWith("## ") ? Cut(e.Lines[0], 100) : "";
        }

        static string Cut(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "...";

        // ---- Building it from the content ----

        /// Makes the encyclopedia from the content database and the guides (title, lines).
        public static Encyclopedia Build(ContentDb db, IEnumerable<KeyValuePair<string, string[]>> guides)
        {
            var enc = new Encyclopedia();
            foreach (var g in guides)
            {
                var e = enc.Add("guide:" + g.Key, g.Key, "Guide");
                foreach (var line in g.Value)
                    e.Line(line);
            }
            foreach (var s in db.Skills)
                enc.Add(SkillId(s.Id), s.Name, "Skill", s.Icon);
            foreach (var i in db.Items)
                enc.Add(ItemId(i.Id), i.Name, "Item", i.Icon);
            foreach (var n in db.Npcs)
                enc.Add("npc:" + n.Id, n.Name, "Monster");
            foreach (var n in db.Nodes)
                if (!enc.Entries.Any(x => x.Category == "Gathering" && x.Title == NodeTitle(n)))  // one page for each kind of spot
                    enc.Add("node:" + n.Id, NodeTitle(n), "Gathering");
            foreach (var s in db.Stations)
                enc.Add("station:" + s.Id, s.Name, "Station");
            foreach (var s in db.Spells)
                enc.Add("spell:" + s.Id, s.Name, "Spell", s.Icon);
            foreach (var p in Prayers.All)
                enc.Add("prayer:" + p.Id, p.Name, "Prayer");
            foreach (var a in Abilities.All)
                enc.Add("ability:" + a.Id, a.Name, "Ability");
            foreach (var p in db.Pieces)
                enc.Add("piece:" + p.Id, p.Name, "Building", p.Icon);
            foreach (var c in db.Courses)
                enc.Add("course:" + c.Id, c.Name, "Agility course");
            foreach (var s in db.Shops)
                enc.Add("shop:" + s.Id, s.Name, "Shop");

            foreach (var i in db.Items) enc.DescribeItem(db, i);
            foreach (var s in db.Skills) enc.DescribeSkill(db, s);
            foreach (var n in db.Npcs) enc.DescribeNpc(db, n);
            foreach (var n in db.Nodes) enc.DescribeNode(db, n);
            foreach (var s in db.Stations) enc.DescribeStation(db, s);
            foreach (var s in db.Spells) enc.DescribeSpell(db, s);
            foreach (var p in Prayers.All) enc.DescribePrayer(db, p);
            foreach (var a in Abilities.All) enc.DescribeAbility(db, a);
            foreach (var p in db.Pieces) enc.DescribePiece(db, p);
            foreach (var c in db.Courses) enc.DescribeCourse(db, c);
            foreach (var s in db.Shops) enc.DescribeShop(db, s);

            foreach (var e in enc.Entries)
            {
                e.TitleLower = e.Title.ToLowerInvariant();
                e.TextLower = string.Join("\n", e.Lines.ToArray()).ToLowerInvariant();
            }
            return enc;
        }

        static string NodeTitle(NodeDef n) => n.Look == NodeLook.FishingSpot ? "Fishing spot (" + n.Action.ToLowerInvariant() + ")" : n.Name;

        static string ItemName(ContentDb db, string id) => db.Item(id)?.Name ?? id;
        static string SkillName(ContentDb db, string id) => db.Skill(id)?.Name ?? id;

        static string Costs(ContentDb db, IEnumerable<Ingredient> inputs) =>
            string.Join(", ", inputs.Select(i => i.Count + " x " + ItemName(db, i.ItemId)).ToArray());

        static string Where(ContentDb db, RecipeDef r) =>
            r.Station == Making.InHand ? "in your hands" : "at " + Article(db.Station(r.Station)?.Name ?? r.Station);

        static string Article(string name) => (name.Length > 0 && "AEIOUaeiou".IndexOf(name[0]) >= 0 ? "an " : "a ") + name.ToLowerInvariant();

        /// "safe ground", "wilderness level 6 and deeper", "wilderness levels 3 to 39"...
        static string Danger(int min, int max)
        {
            if (min <= 0 && max >= int.MaxValue) return "anywhere";
            if (min <= 0 && max <= 1) return "on the safe ground round towns";
            if (min <= 0) return "from the safe ground round towns out to wilderness level " + (max - 1);
            if (max >= int.MaxValue) return "from wilderness level " + min + " deeper";
            return "at wilderness levels " + min + " to " + (max - 1);
        }

        static string Distance(int min, int max)
        {
            if (min <= 0 && max >= int.MaxValue) return "";
            if (max >= int.MaxValue) return ", in towns " + min + " or more tiles from Epacenur";
            return ", in towns up to " + max + " tiles from Epacenur";
        }

        static string TerrainName(Terrain t)
        {
            switch (t)
            {
                case Terrain.Tree: return "Trees grow";
                case Terrain.Rock: return "Rocks lie in quarries";
                case Terrain.Water: return "Fishing spots are on pond shores";
                case Terrain.Flax: return "Grows in flax fields (meadows)";
                case Terrain.Essence: return "Boulders ring every altar's shrine";
                case Terrain.Stall: return "Stands in town squares";
                case Terrain.Field: return "In every town's farm";
                default: return "Found";
            }
        }

        void DescribeItem(ContentDb db, ItemDef item)
        {
            var e = Get(ItemId(item.Id));
            e.Line(item.Examine);
            e.Line("Worth " + item.Value + " coin" + (item.Value == 1 ? "" : "s") + "." + (item.Stackable ? " Stacks in one inventory slot." : ""));
            if (item.ToolType != null)
                e.Line("A tool (" + item.ToolType + (item.ToolTier > 1 ? ", tier " + item.ToolTier : "") + "): it works from the inventory or the tool belt.");
            if (item.Slot != EquipSlot.None)
            {
                e.Line("Worn in the " + item.Slot.ToString().ToLowerInvariant() + " slot." + (item.TwoHanded ? " Two-handed." : ""));
                var b = item.Bonuses;
                string att = Bonus("stab", b.StabAttack) + Bonus("slash", b.SlashAttack) + Bonus("crush", b.CrushAttack) + Bonus("ranged", b.RangedAttack) + Bonus("magic", b.MagicAttack);
                string def = Bonus("stab", b.StabDefence) + Bonus("slash", b.SlashDefence) + Bonus("crush", b.CrushDefence) + Bonus("ranged", b.RangedDefence) + Bonus("magic", b.MagicDefence);
                if (att.Length > 0) e.Line("Attack bonuses:" + att.TrimEnd(','));
                if (def.Length > 0) e.Line("Defence bonuses:" + def.TrimEnd(','));
                if (b.Strength != 0 || b.RangedStrength != 0)
                    e.Line("Strength" + (b.Strength != 0 ? " +" + b.Strength : "") + (b.RangedStrength != 0 ? ", ranged strength +" + b.RangedStrength : "") + ".");
                if (item.Slot == EquipSlot.Weapon)
                    e.Line("Attacks every " + (item.AttackTicks * GameTime.TickSeconds).ToString("0.0") + " seconds" + (item.Range > 0 ? ", from up to " + item.Range + " tiles" : "") + ".");
                if (item.InfiniteRune != null)
                {
                    e.Line("Gives endless " + ItemName(db, item.InfiniteRune).ToLowerInvariant() + "s for spells.");
                    e.Link(ItemId(item.InfiniteRune));
                }
            }
            foreach (var req in item.Requires)
            {
                e.Line("Needs " + SkillName(db, req.Skill) + " " + req.Level + " to " + (item.ToolType != null && item.Slot == EquipSlot.None ? "use" : "wear") + ".");
                e.Link(SkillId(req.Skill));
            }
            if (item.Heals > 0) e.Line("Eat it to heal " + item.Heals + " hitpoints.");
            if (item.BuryXp > 0)
            {
                e.Line("Bury it for " + item.BuryXp + " Prayer xp.");
                e.Link(SkillId(SkillIds.Prayer));
            }
            if (item.Potion != null)
            {
                foreach (var boost in item.Potion.Boosts)
                    e.Line("Drink it: " + SkillName(db, boost.Skill) + " +" + boost.Flat + (boost.Percent > 0 ? " and +" + (boost.Percent * 100).ToString("0") + "%" : "") + " for a while.");
                if (item.Potion.PrayerFlat > 0) e.Line("Drink it to restore " + item.Potion.PrayerFlat + " + " + (item.Potion.PrayerPercent * 100).ToString("0") + "% of your Prayer level in prayer points.");
                if (item.Potion.Energy > 0) e.Line("Drink it to restore " + item.Potion.Energy + "% run energy.");
            }
            var burn = db.Burnable(item.Id);
            if (burn != null)
            {
                e.Line("Burns on a fire at Firemaking " + burn.Level + " for " + burn.Xp + " xp.");
                e.Link(SkillId("firemaking"));
            }

            var made = db.Recipes.Where(r => r.Product == item.Id || r.FailProduct == item.Id).ToList();
            if (made.Count > 0)
            {
                e.Heading("Made by");
                foreach (var r in made)
                {
                    e.Line((r.FailProduct == item.Id ? "(failing at) " : "") + r.Name + ": " + SkillName(db, r.Skill) + " " + r.Level + " (" + r.Xp + " xp), " + Where(db, r)
                        + ", from " + Costs(db, r.Inputs) + (r.ToolType != null ? ", with a " + r.ToolType : "") + (r.ProductCount > 1 ? " (makes " + r.ProductCount + ")" : "") + ".");
                    e.Link(SkillId(r.Skill));
                    if (r.Station != Making.InHand) e.Link("station:" + r.Station);
                    foreach (var i in r.Inputs) e.Link(ItemId(i.ItemId));
                }
            }
            var uses = db.Recipes.Where(r => r.Inputs.Any(i => i.ItemId == item.Id)).ToList();
            if (uses.Count > 0)
            {
                e.Heading("Used to make");
                foreach (var r in uses)
                {
                    e.Line(ItemName(db, r.Product) + " (" + SkillName(db, r.Skill) + " " + r.Level + ", " + Where(db, r) + ").");
                    e.Link(ItemId(r.Product));
                }
            }
            var from = db.Nodes.Where(n => n.Yields.Any(y => y.ItemId == item.Id)).ToList();
            if (from.Count > 0)
            {
                e.Heading("Gathered from");
                foreach (var n in from)
                {
                    var y = n.Yields.First(v => v.ItemId == item.Id);
                    e.Line(NodeTitle(n) + ": " + SkillName(db, n.Skill) + " " + y.Level + " (" + y.Xp + " xp)" + (n.ToolType != null ? ", with a " + n.ToolType : "") + ".");
                    e.Link(NodeLink(n));
                    e.Link(SkillId(n.Skill));
                }
            }
            var droppers = db.Npcs.Where(n => n.AlwaysDrops.Contains(item.Id) || n.Drops.Entries.Any(d => d.ItemId == item.Id)
                || (n.Pickpocket != null && n.Pickpocket.Loot.Entries.Any(d => d.ItemId == item.Id))).ToList();
            if (droppers.Count > 0)
            {
                e.Heading("Dropped or stolen");
                foreach (var n in droppers)
                {
                    bool always = n.AlwaysDrops.Contains(item.Id);
                    bool pocket = n.Pickpocket != null && n.Pickpocket.Loot.Entries.Any(d => d.ItemId == item.Id);
                    e.Line(n.Name + (always ? ": always drops it" : pocket && !n.Drops.Entries.Any(d => d.ItemId == item.Id) ? ": from its pocket" : ": sometimes drops it") + ".");
                    e.Link("npc:" + n.Id);
                }
            }
            var sellers = db.Shops.Where(s => s.Stock.Any(st => st.ItemId == item.Id)).ToList();
            if (sellers.Count > 0)
            {
                e.Heading("Sold by");
                foreach (var s in sellers)
                {
                    e.Line(s.Name + ", for " + Shop.BuyPrice(item) + " coins.");
                    e.Link("shop:" + s.Id);
                }
            }
            var pieces = db.Pieces.Where(p => p.Inputs.Any(i => i.ItemId == item.Id)).ToList();
            if (pieces.Count > 0)
            {
                e.Heading("Used to build");
                foreach (var p in pieces)
                {
                    e.Line(p.Name + " (Construction " + p.Level + ").");
                    e.Link("piece:" + p.Id);
                }
            }
            var spells = db.Spells.Where(s => s.Runes.Any(r => r.ItemId == item.Id)).ToList();
            if (spells.Count > 0)
            {
                e.Heading("Spells that use it");
                foreach (var s in spells)
                {
                    e.Line(s.Name + " (Magic " + s.Level + "): " + Costs(db, s.Runes) + ".");
                    e.Link("spell:" + s.Id);
                }
            }
        }

        static string Bonus(string name, int v) => v == 0 ? "" : " " + name + " " + (v > 0 ? "+" : "") + v + ",";

        /// A node's page (fishing spots of one kind share the first's).
        string NodeLink(NodeDef n) => Get("node:" + n.Id) != null ? "node:" + n.Id : Entries.First(e => e.Category == "Gathering" && e.Title == NodeTitle(n)).Id;

        void DescribeSkill(ContentDb db, SkillDef skill)
        {
            var e = Get(SkillId(skill.Id));
            e.Line("Starts at level " + skill.StartLevel + ". Levels go up to " + Experience.MaxLevel + ", at OSRS rates.");
            // Everything it unlocks, by level.
            var unlocks = new List<KeyValuePair<int, string>>();
            Action<int, string, string> Unlock = (level, text, link) =>
            {
                unlocks.Add(new KeyValuePair<int, string>(level, text));
                if (link != null) e.Link(link);
            };
            foreach (var n in db.Nodes.Where(n => n.Skill == skill.Id))
                foreach (var y in n.Yields)
                    Unlock(y.Level, ItemName(db, y.ItemId) + " from " + NodeTitle(n).ToLowerInvariant() + " (" + y.Xp + " xp)", NodeLink(n));
            foreach (var r in db.Recipes.Where(r => r.Skill == skill.Id))
                Unlock(r.Level, r.Name + ", " + Where(db, r) + " (" + r.Xp + " xp)", ItemId(r.Product));
            foreach (var i in db.Items)
                foreach (var req in i.Requires.Where(q => q.Skill == skill.Id))
                    Unlock(req.Level, (i.ToolType != null && i.Slot == EquipSlot.None ? "Use " : "Wear ") + i.Name.ToLowerInvariant(), ItemId(i.Id));
            foreach (var p in db.Pieces.Where(p => skill.Id == "construction"))
                Unlock(p.Level, "Build " + p.Name.ToLowerInvariant() + " (" + p.Xp + " xp)", "piece:" + p.Id);
            foreach (var s in db.Spells.Where(s => skill.Id == SkillIds.Magic))
                Unlock(s.Level, "Cast " + s.Name, "spell:" + s.Id);
            foreach (var p in Prayers.All.Where(p => skill.Id == SkillIds.Prayer))
                Unlock(p.Level, "Pray " + p.Name, "prayer:" + p.Id);
            foreach (var a in Abilities.All.Where(a => a.Skill == skill.Id))
                Unlock(a.Level, "The " + a.Name + " ability", "ability:" + a.Id);
            foreach (var c in db.Courses.Where(c => skill.Id == "agility"))
                Unlock(c.Level, "The " + c.Name.ToLowerInvariant() + " (" + c.LapXp + " xp a lap)", "course:" + c.Id);
            foreach (var n in db.Npcs.Where(n => n.Pickpocket != null && skill.Id == "thieving"))
                Unlock(n.Pickpocket.Level, "Pickpocket " + n.Name.ToLowerInvariant() + "s (" + n.Pickpocket.Xp + " xp)", "npc:" + n.Id);
            foreach (var i in db.Items.Where(i => i.BuryXp > 0 && skill.Id == SkillIds.Prayer))
                Unlock(1, "Bury " + i.Name.ToLowerInvariant() + " (" + i.BuryXp + " xp)", ItemId(i.Id));
            foreach (var i in db.Items)
            {
                var burn = db.Burnable(i.Id);
                if (burn != null && skill.Id == "firemaking")
                    Unlock(burn.Level, "Burn " + i.Name.ToLowerInvariant() + " (" + burn.Xp + " xp)", ItemId(i.Id));
            }
            if (unlocks.Count == 0)
                return;
            e.Heading("By level");
            foreach (var u in unlocks.OrderBy(u => u.Key).ThenBy(u => u.Value, StringComparer.Ordinal))
                e.Line("Level " + u.Key + ": " + u.Value + ".");
        }

        void DescribeNpc(ContentDb db, NpcDef n)
        {
            var e = Get("npc:" + n.Id);
            e.Line(n.Examine);
            e.Line("Combat level " + n.CombatLevel + ", " + n.Hitpoints + " hitpoints. Hits up to " + n.MaxHit + " (" + n.AttackType.ToString().ToLowerInvariant() + ")"
                + (n.AttackRange > 1 ? " from " + n.AttackRange + " tiles away" : "") + ", every " + (n.AttackTicks * GameTime.TickSeconds).ToString("0.0") + " seconds.");
            e.Line(n.Aggressive ? "Aggressive: it attacks you if you come close, unless your combat level is more than twice its own." : "Not aggressive: it only fights back.");
            if (n.AlwaysDrops.Count > 0 || n.Drops.Entries.Any(d => d.ItemId != null))
            {
                e.Heading("Drops");
                foreach (var a in n.AlwaysDrops)
                {
                    e.Line(ItemName(db, a) + " (always).");
                    e.Link(ItemId(a));
                }
                int total = n.Drops.Entries.Sum(d => d.Weight);
                foreach (var d in n.Drops.Entries.Where(d => d.ItemId != null))
                {
                    e.Line(ItemName(db, d.ItemId) + (d.Max > 1 ? " (" + d.Min + "-" + d.Max + ")" : "") + ", " + (100.0 * d.Weight / Math.Max(1, total)).ToString("0") + "% of kills.");
                    e.Link(ItemId(d.ItemId));
                }
            }
            if (n.Pickpocket != null)
            {
                e.Heading("Pickpocketing");
                e.Line("Thieving " + n.Pickpocket.Level + ", " + n.Pickpocket.Xp + " xp. Caught, it hits you for up to " + n.Pickpocket.StunDamage + " and you're stunned.");
                e.Link(SkillId("thieving"));
                foreach (var d in n.Pickpocket.Loot.Entries.Where(d => d.ItemId != null))
                    e.Link(ItemId(d.ItemId));
            }
            if (n.Spawns.Count > 0)
            {
                e.Heading("Where");
                foreach (var r in n.Spawns)
                {
                    string where = r.Region == RegionKind.Town ? (r.Resident ? "In towns, among the townsfolk" : "In towns")
                        : r.Region == RegionKind.Camp ? "In camps" : "In the wild";
                    e.Line(where + ", " + (r.Region == RegionKind.Town ? "always safe" : Danger(r.MinDanger, r.MaxDanger)) + Distance(r.MinDistance, r.MaxDistance)
                        + ", in groups of " + r.MinGroup + (r.MaxGroup > r.MinGroup ? " to " + r.MaxGroup : "") + ".");
                }
                e.Link("guide:The wilderness");
            }
        }

        void DescribeNode(ContentDb db, NodeDef n)
        {
            var e = Get("node:" + n.Id) ?? Get(NodeLink(n));
            bool shared = e.Id != "node:" + n.Id;
            if (!shared)
            {
                e.Line(n.Examine);
                e.Line(n.Action + " it: " + SkillName(db, n.Skill) + (n.ToolType != null ? ", with a " + n.ToolType : "")
                    + (n.BaitItemId != null ? ", using " + ItemName(db, n.BaitItemId).ToLowerInvariant() : "") + "."
                    + (n.MaxLives <= 0 ? " It never runs out." : " It runs out after a few, and comes back after " + n.RespawnSeconds.ToString("0") + " seconds."));
                e.Link(SkillId(n.Skill));
                e.Heading("Gives");
            }
            foreach (var y in n.Yields)
            {
                e.Line(ItemName(db, y.ItemId) + ": " + SkillName(db, n.Skill) + " " + y.Level + ", " + y.Xp + " xp.");
                e.Link(ItemId(y.ItemId));
            }
            if (n.Spawns.Count > 0 && !shared)
            {
                e.Heading("Where");
                foreach (var r in n.Spawns)
                    e.Line(TerrainName(r.On) + " " + Danger(r.MinDanger, r.MaxDanger) + Distance(r.MinDistance, r.MaxDistance)
                        + (r.Biome != null ? ", more of them in " + r.Biome.Value.ToString().ToLowerInvariant() : "") + ".");
                e.Link("guide:The wilderness");
            }
        }

        void DescribeStation(ContentDb db, StationDef s)
        {
            var e = Get("station:" + s.Id);
            e.Line(s.Examine);
            var made = db.RecipesAt(s.Id);
            if (s.Kind == StationKind.Bank) e.Line("Opens your bank. Every town has a bank.");
            if (s.Kind == StationKind.Altar) e.Line("Pray at it to recharge your prayer points. Every town's chapel has one.");
            if (made.Count > 0)
            {
                e.Heading("Made here");
                foreach (var r in made)
                {
                    e.Line(r.Name + " (" + SkillName(db, r.Skill) + " " + r.Level + "): " + Costs(db, r.Inputs) + ".");
                    e.Link(ItemId(r.Product));
                }
            }
            foreach (var r in s.Spawns)
                e.Line("Its shrines lie " + Danger(r.MinDanger, r.MaxDanger) + (r.Starter ? "; one is always just east of Epacenur" : "") + ".");
            foreach (var p in db.Pieces.Where(p => p.Station == s.Id))
            {
                e.Line("You can build one in a base (" + p.Name + ", Construction " + p.Level + ").");
                e.Link("piece:" + p.Id);
            }
        }

        void DescribeSpell(ContentDb db, SpellDef s)
        {
            var e = Get("spell:" + s.Id);
            e.Line(s.Description);
            e.Line("Magic " + s.Level + (s.Xp > 0 ? ", " + s.Xp + " xp" : "") + (s.MaxHit > 0 ? ", hits up to " + s.MaxHit : "") + ".");
            if (s.Runes.Length > 0)
                e.Line("Runes: " + Costs(db, s.Runes) + ".");
            e.Link(SkillId(SkillIds.Magic));
            foreach (var r in s.Runes)
                e.Link(ItemId(r.ItemId));
        }

        void DescribePrayer(ContentDb db, PrayerDef p)
        {
            var e = Get("prayer:" + p.Id);
            e.Line(p.Description);
            e.Line("Prayer " + p.Level + ". Drains a point every " + (PrayerBook.DrainResistance / (float)p.DrainEffect * GameTime.TickSeconds).ToString("0.#") + " seconds while it's on.");
            e.Link(SkillId(SkillIds.Prayer));
        }

        void DescribeAbility(ContentDb db, AbilityDef a)
        {
            var e = Get("ability:" + a.Id);
            e.Line(a.Description);
            e.Line(SkillName(db, a.Skill) + " " + a.Level + ". Cooldown " + a.CooldownSeconds + " seconds. On the action bar (keys 1-9).");
            e.Link(SkillId(a.Skill));
        }

        void DescribePiece(ContentDb db, PieceDef p)
        {
            var e = Get("piece:" + p.Id);
            e.Line(p.Examine);
            e.Line(p.Material + " " + p.Kind.ToString().ToLowerInvariant() + ". Construction " + p.Level + ", " + p.Xp + " xp. Costs " + Costs(db, p.Inputs) + ".");
            if (p.Use == FurnitureUse.Chest) e.Line("A storage chest: put items in it like a bank (it holds 30 kinds of item).");
            if (p.Use == FurnitureUse.Bed) e.Line("Sleep in it and you'll wake up beside it when you die.");
            if (p.Use == FurnitureUse.Station)
            {
                e.Line("Works like " + Article(db.Station(p.Station).Name) + " in town.");
                e.Link("station:" + p.Station);
            }
            e.Link(SkillId("construction"));
            e.Link("guide:Building a base");
            foreach (var i in p.Inputs)
                e.Link(ItemId(i.ItemId));
        }

        void DescribeCourse(ContentDb db, CourseDef c)
        {
            var e = Get("course:" + c.Id);
            e.Line("Agility " + c.Level + ". Five obstacles round a loop; crossing all five in order is a lap, worth " + c.LapXp + " xp more.");
            foreach (var r in c.Spawns)
                e.Line("Found " + Danger(r.MinDanger, r.MaxDanger) + (r.Starter ? "; one is always just west of Epacenur" : "") + ".");
            e.Heading("Obstacles");
            foreach (var o in c.Obstacles)
                e.Line(o.Name + " (" + o.Action + "): Agility " + o.Level + ", " + o.Xp + " xp" + (o.LowChance >= 256 ? ", never fails" : "") + ".");
            e.Link(SkillId("agility"));
        }

        void DescribeShop(ContentDb db, ShopDef s)
        {
            var e = Get("shop:" + s.Id);
            e.Line(s.BuysAnything ? "Buys almost anything, for " + (s.SellRate * 100).ToString("0") + "% of its value. Every town has one." : "Buys only what it sells.");
            e.Heading("Stock");
            foreach (var st in s.Stock)
            {
                var item = db.Item(st.ItemId);
                e.Line(item.Name + ": " + Shop.BuyPrice(item) + " coins (" + st.Count + " in stock).");
                e.Link(ItemId(st.ItemId));
            }
        }
    }
}
