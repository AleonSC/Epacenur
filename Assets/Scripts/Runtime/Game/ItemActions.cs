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
        }

        /// The handler for an extra verb an item lists in ItemDef.Verbs, e.g. "Rub" for an amulet.
        public static void Register(string verb, Action<int> run) => extra[verb] = run;

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
                slot => G.Equipment.Equip(G.Inventory, slot), always);
            Add(Verbs.AddToPouch, d => d.Id == ItemIds.Coins, slot => G.Inventory.AddToPouch(slot), always);
            Add(Verbs.Bury, d => d.BuryXp > 0, slot => G.Player.Bury(slot), always);
            Add(Verbs.Eat, d => d.Heals > 0, slot => G.Player.Eat(slot), always);
            Add(Verbs.Cook, d => ContentDb.Current.RecipeUsing(FiremakingModule.Fire, d.Id) != null,
                slot => G.Player.CookFromInventory(G.Inventory[slot].ItemId), always);
            Add(Verbs.AddToToolBelt, d => d.IsTool, AddToToolBelt, d => !G.ToolBelt.Has(d.Id));
            Add(Verbs.Light, d => ContentDb.Current.Burnable(d.Id) != null, slot => G.Player.LightFromInventory(slot),
                d => ToolBelt.Find(G.ToolBelt, G.Inventory, "tinderbox") != null);
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
        /// picks the item up to swap it with another slot).
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
            o.Add(new MenuOption(Verbs.Drop, name, guard(drop)));
            o.Add(new MenuOption(Verbs.Examine, name, () => GameLog.Add(def.Examine)));
            return o;
        }
    }
}
