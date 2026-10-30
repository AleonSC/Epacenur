using System;
using System.Collections.Generic;
using Epacenur.Content;
using Epacenur.Core;

namespace Epacenur.Runtime
{
    /// What you can do with an item in the inventory. Most verbs come from what the item is (anything with
    /// an equipment slot can be worn, anything that heals eaten, ...); an item can list extra verbs
    /// (ItemDef.Verbs) whose handlers are added here with Register.
    public static class ItemActions
    {
        sealed class Verb
        {
            public Func<ItemDef, string> Name;
            public Func<ItemDef, bool> Applies;
            public Func<ItemDef, bool> OnClick;  // whether left-clicking the item does this (null: never)
            public Action<int> Run;              // given the inventory slot
        }

        static Game G => Game.Instance;
        static readonly List<Verb> builtIn = new List<Verb>();
        static readonly Dictionary<string, Action<int>> extra = new Dictionary<string, Action<int>>();

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            builtIn.Clear();
            extra.Clear();
            useRules.Clear();
        }

        /// The handler for an extra verb an item lists in ItemDef.Verbs, e.g. "Rub" for an amulet.
        public static void Register(string verb, Action<int> run) => extra[verb] = run;

        // ---- Use one item on another ----

        static readonly List<Func<int, int, bool>> useRules = new List<Func<int, int, bool>>();

        /// A rule for using one inventory item on another: given (used slot, target slot), does it and returns
        /// true if it applies. Rules are tried both ways round.
        public static void RegisterUse(Func<int, int, bool> rule) => useRules.Add(rule);

        static void MakeUseRules()
        {
            if (useRules.Count > 0)
                return;
            // Things made in your hands: a knife on logs, a bowstring on a bow, feathers on arrow shafts... The make
            // window lists every such recipe that uses both (or one, with the other as its tool).
            RegisterUse((a, b) =>
            {
                var da = G.Inventory[a].Def;
                var db = G.Inventory[b].Def;
                var list = ContentDb.Current.RecipesAt(Making.InHand).FindAll(r =>
                    System.Array.Exists(r.Inputs, i => i.ItemId == db.Id)
                    && (System.Array.Exists(r.Inputs, i => i.ItemId == da.Id) || (r.ToolType != null && r.ToolType == da.ToolType)));
                if (list.Count == 0)
                    return false;
                G.Hud.OpenMakingInHand(list);
                return true;
            });
            // A tinderbox on logs lights them.
            RegisterUse((a, b) =>
            {
                var tool = G.Inventory[a].Def;
                if (tool.ToolType != "tinderbox" || ContentDb.Current.Burnable(G.Inventory[b].ItemId) == null)
                    return false;
                G.Player.LightFromInventory(b);
                return true;
            });
        }

        public static void UseOnItem(int used, int target)
        {
            MakeUseRules();
            foreach (var rule in useRules)
                if (rule(used, target) || rule(target, used))
                    return;
            GameLog.Add("Nothing interesting happens.");
        }

        static void Add(string name, Func<ItemDef, bool> applies, Action<int> run, Func<ItemDef, bool> onClick)
            => AddNamed(d => name, applies, run, onClick);

        static void AddNamed(Func<ItemDef, string> name, Func<ItemDef, bool> applies, Action<int> run, Func<ItemDef, bool> onClick)
            => builtIn.Add(new Verb { Name = name, Applies = applies, Run = run, OnClick = onClick });

        // In the order left-click picks them (the first that applies), which is also the menu order.
        static void MakeBuiltIn()
        {
            if (builtIn.Count > 0)
                return;
            Func<ItemDef, bool> always = d => true;
            AddNamed(d => d.Slot == EquipSlot.Weapon ? Verbs.Wield : Verbs.Wear, d => d.Slot != EquipSlot.None,
                Wear, always);
            Add(Verbs.AddToPouch, d => d.Id == ItemIds.Coins, slot => G.Inventory.AddToPouch(slot), always);
            Add(Verbs.Bury, d => d.BuryXp > 0, slot => G.Player.Bury(slot), always);
            Add(Verbs.Eat, d => d.Heals > 0, slot => G.Player.Eat(slot), always);
            Add(Verbs.Drink, d => d.Potion != null, slot => G.Player.Drink(slot), always);
            AddNamed(d => d.ProcessVerb, d => d.Process != null, slot => G.Player.Process(slot), always);
            Add(Verbs.Cook, d => ContentDb.Current.RecipeUsing(FiremakingModule.Fire, d.Id) != null,
                slot => G.Player.CookFromInventory(G.Inventory[slot].ItemId), always);
            Add(Verbs.AddToToolBelt, d => d.IsTool, AddToToolBelt, d => !G.ToolBelt.Has(d.Id));
            Add(Verbs.Light, d => ContentDb.Current.Burnable(d.Id) != null, slot => G.Player.LightFromInventory(slot),
                d => ToolBelt.Find(G.ToolBelt, G.Inventory, "tinderbox", G.Skills) != null);
        }

        static void Wear(int slot)
        {
            if (!G.Equipment.Equip(G.Inventory, slot, G.Skills, out string problem) && problem != null)
                GameLog.Add(problem);
        }

        static void AddToToolBelt(int slot)
        {
            var def = G.Inventory[slot].Def;
            if (G.ToolBelt.AddFromInventory(G.Inventory, slot, out string problem))
                GameLog.Add("You add the " + def.Name.ToLowerInvariant() + " to your tool belt.");
            else
                GameLog.Add(problem);
        }

        /// Left-click on an inventory item: does its main verb. False when it has none (the HUD then
        /// picks it to Use on something).
        public static bool Click(int slot)
        {
            MakeBuiltIn();
            var def = G.Inventory[slot].Def;
            if (def == null)
                return false;
            foreach (var v in builtIn)
                if (v.OnClick != null && v.Applies(def) && v.OnClick(def))
                {
                    v.Run(slot);
                    return true;
                }
            return false;
        }

        /// The right-click options: every verb that applies, then Drop and Examine.
        public static List<MenuOption> Options(int slot, Action<int> drop)
        {
            MakeBuiltIn();
            var def = G.Inventory[slot].Def;
            var o = new List<MenuOption>();
            if (def == null)
                return o;
            string name = MenuOption.Item(def.Name);
            // The item could have moved by the time an option is picked: only act if it's still there.
            Func<Action<int>, Action> guard = run => () => { if (G.Inventory[slot].ItemId == def.Id) run(slot); };
            foreach (var v in builtIn)
                if (v.Applies(def))
                    o.Add(new MenuOption(v.Name(def), name, guard(v.Run)));
            foreach (var verb in def.Verbs)
                if (extra.TryGetValue(verb, out var run))
                    o.Add(new MenuOption(verb, name, guard(run)));
            o.Add(new MenuOption(Verbs.Use, name, guard(s => G.Player.BeginUse(s))));
            o.Add(new MenuOption(Verbs.Drop, name, guard(drop)));
            o.Add(new MenuOption(Verbs.Examine, name, () => GameLog.Add(def.Examine)));
            o.Add(new MenuOption("Look up", name, () => G.Hud.OpenEncyclopedia(def.Name)));
            return o;
        }
    }
}
