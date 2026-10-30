using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// "What would you like to make?" at a station (furnace, anvil, fire): its recipes, what each needs,
    /// and how many to make (1, 5, 10, X or All, remembered between uses). Click a row to make it; Space or
    /// Enter makes the first one you can. Using an item on the station shows only what's made with it.
    public sealed class MakeWindow : Window
    {
        const float RowH = Gui.Slot + 8f;
        static readonly int[] Amounts = { 1, 5, 10, -1, int.MaxValue };  // -1 = X
        static int amount = int.MaxValue;  // remembered between uses
        static int customAmount = 14;

        readonly StationObject station;
        readonly List<RecipeDef> recipes;
        string xText;
        bool typingX;

        public MakeWindow(StationObject station, string prefer)
        {
            this.station = station;
            var all = ContentDb.Current.RecipesAt(station.Def.Id);
            recipes = new List<RecipeDef>();
            if (prefer != null)
                recipes.AddRange(all.FindAll(r => System.Array.Exists(r.Inputs, i => i.ItemId == prefer)));
            if (recipes.Count == 0)
                recipes.AddRange(all);
            // What you can make right now first, otherwise in their usual order.
            var order = new List<RecipeDef>(recipes);
            recipes.Sort((a, b) =>
            {
                int c = (Making.CanMake(b, G.Inventory) > 0).CompareTo(Making.CanMake(a, G.Inventory) > 0);
                return c != 0 ? c : order.IndexOf(a).CompareTo(order.IndexOf(b));
            });
            xText = customAmount.ToString();
        }

        int Count => amount == -1 ? customAmount : amount;

        public override void Draw(Hud hud, Rect r)
        {
            var e = Event.current;
            var db = ContentDb.Current;
            Frame(hud, r, "What would you like to make?");

            // How many: 1, 5, 10, X (type a number) or All.
            GUI.Label(new Rect(r.x + Gui.Pad, r.y + 38f, 80f, 22f), "How many:", Gui.Small);
            float bx = r.x + Gui.Pad + 74f;
            foreach (int a in Amounts)
            {
                string label = a == int.MaxValue ? "All" : a == -1 ? "X" + (amount == -1 ? " (" + customAmount + ")" : "") : a.ToString();
                float w = a == -1 ? 62f : 42f;  // X shows its number too
                if (GUI.Toggle(new Rect(bx, r.y + 38f, w, 22f), amount == a, label, GUI.skin.button))
                {
                    if (a == -1 && amount != -1)
                        typingX = true;
                    amount = a;
                }
                bx += w + 4f;
            }
            if (typingX && amount == -1)
            {
                GUI.Label(new Rect(r.x + Gui.Pad, r.y + 66f, 120f, 22f), "Make how many?", Gui.Small);
                var field = new Rect(r.x + Gui.Pad + 110f, r.y + 66f, 70f, 22f);
                xText = GUI.TextField(field, xText, 6);
                if (GUI.Button(new Rect(field.xMax + 4f, field.y, 60f, 22f), "OK") || (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)))
                {
                    if (int.TryParse(xText, out int n) && n > 0)
                        customAmount = n;
                    xText = customAmount.ToString();
                    typingX = false;
                    if (e.type == EventType.KeyDown)
                        e.Use();
                }
            }
            else if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                // The first thing you can make, in the chosen amount.
                var first = recipes.Find(rc => Making.CanMake(rc, G.Inventory) > 0);
                if (first != null)
                    Make(hud, first);
                e.Use();
                return;
            }

            // One row per recipe: icon, name, what it needs and the level. Click to make.
            float top = typingX && amount == -1 ? 96f : 68f;
            var view = new Rect(r.x + Gui.Pad, r.y + top, r.width - Gui.Pad * 2, r.height - top - (station.Def.Help != null ? 50f : 10f));
            var content = new Rect(0f, 0f, view.width - 18f, Mathf.Max(view.height - 2f, recipes.Count * (RowH + 6f)));
            scroll = GUI.BeginScrollView(view, scroll, content);
            RecipeDef clicked = null;
            for (int i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                var product = db.Item(recipe.Product);
                var row = new Rect(0f, i * (RowH + 6f), content.width, RowH);
                Gui.Bevel(row);
                bool levelOk = G.Skills.GetLevel(recipe.Skill) >= recipe.Level;
                int canMake = Making.CanMake(recipe, G.Inventory);
                if (row.Contains(e.mousePosition))
                    Gui.Fill(new Rect(row.x + 1f, row.y + 1f, row.width - 2f, row.height - 2f), new Color(1f, 1f, 1f, 0.07f));
                Gui.DrawItemIcon(new Rect(row.x + 4f, row.y + 4f, Gui.Slot, Gui.Slot), product);
                Gui.Shadowed(new Rect(row.x + Gui.Slot + 12f, row.y + 4f, 260f, 20f), "<b>" + recipe.Name + "</b>"
                    + (canMake > 0 && levelOk ? "  <color=#a0ffa0>(" + canMake + ")</color>" : ""), Gui.Label);
                GUI.Label(new Rect(row.x + Gui.Slot + 12f, row.y + 22f, row.width - Gui.Slot - 16f, 18f),
                    (canMake > 0 ? "<color=#a0ffa0>" : "<color=#ff6060>") + Making.Needs(recipe) + "</color>   "
                    + (levelOk ? "<color=#a0ffa0>" : "<color=#ff6060>") + db.Skill(recipe.Skill).Name + " " + recipe.Level + "</color>"
                    + "   " + recipe.Xp + " xp", Gui.Small);
                if (e.type == EventType.MouseDown && row.Contains(e.mousePosition))
                {
                    if (e.button == 1)
                    {
                        var rc = recipe;
                        string name = MenuOption.Item(rc.Name);
                        hud.OpenMenu(new List<MenuOption>
                        {
                            new MenuOption("Make 1", name, () => MakeCount(hud, rc, 1)),
                            new MenuOption("Make 5", name, () => MakeCount(hud, rc, 5)),
                            new MenuOption("Make 10", name, () => MakeCount(hud, rc, 10)),
                            new MenuOption("Make " + customAmount, name, () => MakeCount(hud, rc, customAmount)),
                            new MenuOption("Make All", name, () => MakeCount(hud, rc, int.MaxValue)),
                            new MenuOption("Examine", name, () => GameLog.Add(product.Examine)),
                        }, false);
                    }
                    else
                    {
                        clicked = recipe;
                    }
                    e.Use();
                }
            }
            GUI.EndScrollView();
            if (clicked != null)
                Make(hud, clicked);
            if (station.Def.Help != null)
                GUI.Label(new Rect(r.x + Gui.Pad, r.yMax - 46f, r.width - Gui.Pad * 2, 40f), station.Def.Help, Gui.Rich);
        }

        void Make(Hud hud, RecipeDef recipe) => MakeCount(hud, recipe, Count);

        void MakeCount(Hud hud, RecipeDef recipe, int count)
        {
            hud.CloseWindow();
            G.Player.StartMaking(station, recipe, count);
        }
    }
}
