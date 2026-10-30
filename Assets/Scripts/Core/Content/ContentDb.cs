using System;
using System.Collections.Generic;
using System.Linq;

namespace Epacenur.Core
{
    /// A content module adds a slice of the game: a skill with its items, nodes and recipes, a shop, monsters.
    /// Modules live in Assets/Scripts/Content and are listed in Modules.All.
    public interface IContentModule
    {
        void Register(ContentDb db);
    }

    public static class WeaponKindIds
    {
        /// Fighting with no weapon (punch, kick, block).
        public const string Unarmed = "unarmed";
    }

    /// Every definition in the game, by id. Built once at startup from the content modules
    /// (ContentDb.Load), then read through ContentDb.Current.
    public sealed class ContentDb
    {
        public static ContentDb Current { get; private set; }

        readonly Dictionary<string, ItemDef> items = new Dictionary<string, ItemDef>();
        readonly Dictionary<string, SkillDef> skills = new Dictionary<string, SkillDef>();
        readonly Dictionary<string, NodeDef> nodes = new Dictionary<string, NodeDef>();
        readonly Dictionary<string, RecipeDef> recipes = new Dictionary<string, RecipeDef>();
        readonly Dictionary<string, StationDef> stations = new Dictionary<string, StationDef>();
        readonly Dictionary<string, ShopDef> shops = new Dictionary<string, ShopDef>();
        readonly Dictionary<string, NpcDef> npcs = new Dictionary<string, NpcDef>();
        readonly Dictionary<string, BurnableDef> burnables = new Dictionary<string, BurnableDef>();
        readonly Dictionary<string, WeaponKindDef> weaponKinds = new Dictionary<string, WeaponKindDef>();
        public readonly List<WeaponKindDef> WeaponKinds = new List<WeaponKindDef>();

        // Registration order is kept: it's the order skills show in the Skills tab, tools on the belt, and so on.
        public readonly List<ItemDef> Items = new List<ItemDef>();
        public readonly List<SkillDef> Skills = new List<SkillDef>();
        public readonly List<NodeDef> Nodes = new List<NodeDef>();
        public readonly List<RecipeDef> Recipes = new List<RecipeDef>();
        public readonly List<StationDef> Stations = new List<StationDef>();
        public readonly List<ShopDef> Shops = new List<ShopDef>();
        public readonly List<NpcDef> Npcs = new List<NpcDef>();

        /// Builds the database from modules, checks every cross-reference, and makes it Current.
        public static ContentDb Load(IEnumerable<IContentModule> modules)
        {
            var db = new ContentDb();
            foreach (var m in modules)
                m.Register(db);
            var problems = db.Validate();
            if (problems.Count > 0)
                throw new InvalidOperationException("Content problems:\n" + string.Join("\n", problems));
            Current = db;
            return db;
        }

        // ---- Registering ----

        static T Add<T>(Dictionary<string, T> map, List<T> list, string id, T def, string kind)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException(kind + " with no id");
            if (map.ContainsKey(id))
                throw new ArgumentException("Two " + kind + "s with the id \"" + id + "\"");
            map[id] = def;
            list.Add(def);
            return def;
        }

        public ItemDef Add(ItemDef d) => Add(items, Items, d.Id, d, "item");
        public SkillDef Add(SkillDef d) => Add(skills, Skills, d.Id, d, "skill");
        public NodeDef Add(NodeDef d) => Add(nodes, Nodes, d.Id, d, "node");
        public RecipeDef Add(RecipeDef d) => Add(recipes, Recipes, d.Id, d, "recipe");
        public StationDef Add(StationDef d) => Add(stations, Stations, d.Id, d, "station");
        public ShopDef Add(ShopDef d) => Add(shops, Shops, d.Id, d, "shop");
        public NpcDef Add(NpcDef d) => Add(npcs, Npcs, d.Id, d, "monster");
        public WeaponKindDef Add(WeaponKindDef d) => Add(weaponKinds, WeaponKinds, d.Id, d, "weapon kind");
        public BurnableDef Add(BurnableDef d)
        {
            if (burnables.ContainsKey(d.LogItemId))
                throw new ArgumentException("Two burnables for \"" + d.LogItemId + "\"");
            burnables[d.LogItemId] = d;
            return d;
        }

        // ---- Looking up (null when there's no such id) ----

        public ItemDef Item(string id) => id != null && items.TryGetValue(id, out var d) ? d : null;
        public SkillDef Skill(string id) => id != null && skills.TryGetValue(id, out var d) ? d : null;
        public NodeDef Node(string id) => id != null && nodes.TryGetValue(id, out var d) ? d : null;
        public RecipeDef Recipe(string id) => id != null && recipes.TryGetValue(id, out var d) ? d : null;
        public StationDef Station(string id) => id != null && stations.TryGetValue(id, out var d) ? d : null;
        public ShopDef Shop(string id) => id != null && shops.TryGetValue(id, out var d) ? d : null;
        public NpcDef Npc(string id) => id != null && npcs.TryGetValue(id, out var d) ? d : null;
        public WeaponKindDef WeaponKind(string id) => id != null && weaponKinds.TryGetValue(id, out var d) ? d : null;
        public BurnableDef Burnable(string itemId) => itemId != null && burnables.TryGetValue(itemId, out var d) ? d : null;

        /// The recipes made at a station, in registration order.
        public List<RecipeDef> RecipesAt(string station) => Recipes.Where(r => r.Station == station).ToList();

        /// The recipe that uses this item at this station (e.g. raw shrimps at a fire), or null.
        public RecipeDef RecipeUsing(string station, string itemId) =>
            Recipes.FirstOrDefault(r => r.Station == station && r.Inputs.Any(i => i.ItemId == itemId));

        /// Every tool, in registration order (the tool belt lists them).
        public IEnumerable<ItemDef> Tools => Items.Where(i => i.IsTool);

        // ---- Checking ----

        /// Every reference between definitions points at something that exists.
        public List<string> Validate()
        {
            var p = new List<string>();
            Action<string, string> Item = (id, where) => { if (id != null && !items.ContainsKey(id)) p.Add(where + ": no item \"" + id + "\""); };
            Action<string, string> Skill = (id, where) => { if (id != null && !skills.ContainsKey(id)) p.Add(where + ": no skill \"" + id + "\""); };
            Action<string, string> Tool = (type, where) => { if (type != null && !items.Values.Any(i => i.ToolType == type)) p.Add(where + ": no tool of type \"" + type + "\""); };

            if (!weaponKinds.ContainsKey(WeaponKindIds.Unarmed)) p.Add("no \"" + WeaponKindIds.Unarmed + "\" weapon kind");
            foreach (var i in Items)
            {
                if (i.WeaponKind != null && !weaponKinds.ContainsKey(i.WeaponKind)) p.Add("item " + i.Id + ": no weapon kind \"" + i.WeaponKind + "\"");
                if (i.Slot == EquipSlot.Weapon && i.WeaponKind == null) p.Add("item " + i.Id + ": a weapon with no weapon kind");
                if (i.Icon == null) p.Add("item " + i.Id + ": no icon");
                if (i.GroundModel == null) p.Add("item " + i.Id + ": no ground model");
            }
            foreach (var s in Skills)
                if (s.Icon == null) p.Add("skill " + s.Id + ": no icon");
            foreach (var n in Nodes)
            {
                Skill(n.Skill, "node " + n.Id);
                Tool(n.ToolType, "node " + n.Id);
                if (n.Yields == null || n.Yields.Length == 0) p.Add("node " + n.Id + ": yields nothing");
                else foreach (var y in n.Yields) Item(y.ItemId, "node " + n.Id);
            }
            foreach (var r in Recipes)
            {
                Skill(r.Skill, "recipe " + r.Id);
                Item(r.Product, "recipe " + r.Id);
                Item(r.FailProduct, "recipe " + r.Id);
                Tool(r.ToolType, "recipe " + r.Id);
                foreach (var i in r.Inputs) Item(i.ItemId, "recipe " + r.Id);
            }
            foreach (var s in Shops)
                foreach (var st in s.Stock) Item(st.ItemId, "shop " + s.Id);
            foreach (var it in Items)
                foreach (var r in it.Requires) Skill(r.Skill, "item " + it.Id);
            foreach (var n in Nodes)
                Item(n.BaitItemId, "node " + n.Id);
            foreach (var n in Npcs)
            {
                foreach (var d in n.Drops.Entries) if (d.ItemId != null) Item(d.ItemId, "monster " + n.Id);
                foreach (var d in n.AlwaysDrops) Item(d, "monster " + n.Id);
            }
            foreach (var b in burnables.Values)
                Item(b.LogItemId, "burnable");
            return p;
        }
    }
}
