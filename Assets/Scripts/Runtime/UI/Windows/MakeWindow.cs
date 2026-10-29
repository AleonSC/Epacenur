using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// "What would you like to make?": the recipes at a station (an anvil), with what each needs and
    /// buttons to make 1, 5 or all.
    public sealed class MakeWindow : Window
    {
        readonly StationObject station;

        public MakeWindow(StationObject station) { this.station = station; }

        public override void Draw(Hud hud, Rect r)
        {
            var e = Event.current;
            var db = ContentDb.Current;
            var recipes = db.RecipesAt(station.Def.Id);
            Frame(hud, r, "What would you like to make?");

            // What you have of everything these recipes use, your level, and any missing tool.
            var parts = new List<string>();
            var seen = new List<string>();
            string skill = null, toolType = null;
            foreach (var rc in recipes)
            {
                skill = skill ?? rc.Skill;
                toolType = toolType ?? rc.ToolType;
                foreach (var i in rc.Inputs)
                    if (!seen.Contains(i.ItemId))
                    {
                        seen.Add(i.ItemId);
                        parts.Add(db.Item(i.ItemId).Name + "s: <color=#ffff00>" + G.Inventory.Count(i.ItemId) + "</color>");
                    }
            }
            if (skill != null)
                parts.Add(db.Skill(skill).Name + " level: <color=#ffff00>" + G.Skills.GetLevel(skill) + "</color>");
            if (toolType != null && ToolBelt.Find(G.ToolBelt, G.Inventory, toolType) == null)
                parts.Add("<color=#ff6060>You need " + Making.Article(toolType) + "</color>");
            Gui.Shadowed(new Rect(r.x + Gui.Pad, r.y + 36f, r.width - Gui.Pad * 2, 18f), string.Join("    ", parts.ToArray()), Gui.Small);

            // One row per recipe: icon, name, what it needs and the level, and Make buttons.
            float y = r.y + 60f;
            foreach (var recipe in recipes)
            {
                var product = db.Item(recipe.Product);
                var row = new Rect(r.x + Gui.Pad, y, r.width - Gui.Pad * 2, Gui.Slot + 8f);
                Gui.Bevel(row);
                Gui.DrawItemIcon(new Rect(row.x + 4f, row.y + 4f, Gui.Slot, Gui.Slot), product);
                bool levelOk = G.Skills.GetLevel(recipe.Skill) >= recipe.Level;
                int canMake = Making.CanMake(recipe, G.Inventory);
                Gui.Shadowed(new Rect(row.x + Gui.Slot + 12f, row.y + 4f, 200f, 20f), "<b>" + recipe.Name + "</b>", Gui.Label);
                GUI.Label(new Rect(row.x + Gui.Slot + 12f, row.y + 22f, 220f, 18f),
                    (canMake > 0 ? "<color=#a0ffa0>" : "<color=#ff6060>") + Making.Needs(recipe) + "</color>   "
                    + (levelOk ? "<color=#a0ffa0>" : "<color=#ff6060>") + "Level " + recipe.Level + "</color>", Gui.Small);
                float bx = row.xMax - 3 * 46f;
                foreach (int n in new[] { 1, 5, int.MaxValue })
                {
                    if (GUI.Button(new Rect(bx, row.y + 12f, 42f, 22f), n == int.MaxValue ? "All" : n.ToString()))
                        Make(hud, recipe, n);
                    bx += 46f;
                }
                if (row.Contains(e.mousePosition) && e.type == EventType.MouseDown && e.button == 1)
                {
                    var rc = recipe;
                    string name = MenuOption.Item(rc.Name);
                    hud.OpenMenu(new List<MenuOption>
                    {
                        new MenuOption("Make 1", name, () => Make(hud, rc, 1)),
                        new MenuOption("Make 5", name, () => Make(hud, rc, 5)),
                        new MenuOption("Make All", name, () => Make(hud, rc, int.MaxValue)),
                        new MenuOption("Examine", name, () => GameLog.Add(product.Examine)),
                    }, false);
                    e.Use();
                }
                y += Gui.Slot + 14f;
            }
            if (station.Def.Help != null)
                GUI.Label(new Rect(r.x + Gui.Pad, r.yMax - 46f, r.width - Gui.Pad * 2, 40f), station.Def.Help, Gui.Rich);
        }

        void Make(Hud hud, RecipeDef recipe, int count)
        {
            hud.CloseWindow();
            G.Player.StartMaking(station, recipe, count);
        }
    }
}
