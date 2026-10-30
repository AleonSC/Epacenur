using System.Collections.Generic;
using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The build menu (G, with a hammer): every building piece by material, with its level and what it costs.
    /// Click one to start placing it (PlayerController.StartBuilding).
    public sealed class BuildWindow : Window
    {
        const float RowH = Gui.Slot + 6f;
        static string material = "Wood";  // the tab, remembered between uses

        public BuildWindow() { Anywhere = true; }

        public override void Draw(Hud hud, Rect r)
        {
            var e = Event.current;
            var db = ContentDb.Current;
            Frame(hud, r, "Build");
            int level = G.Skills.GetLevel(ConstructionModule.Skill);
            Gui.Shadowed(new Rect(r.x + r.width - 220f, r.y + 8f, 180f, 20f), "<color=" + Gui.MutedHex + ">Construction " + level + "</color>", Gui.RightSmall);

            // A tab for each material, in the order they were registered.
            var materials = new List<string>();
            foreach (var p in db.Pieces)
                if (!materials.Contains(p.Material))
                    materials.Add(p.Material);
            float tx = r.x + Gui.Pad;
            foreach (var m in materials)
            {
                if (GUI.Toggle(new Rect(tx, r.y + 38f, 90f, 22f), material == m, m, GUI.skin.button))
                    material = m;
                tx += 94f;
            }

            var list = db.Pieces.FindAll(p => p.Material == material);
            var view = new Rect(r.x + Gui.Pad, r.y + 68f, r.width - Gui.Pad * 2, r.height - 68f - 46f);
            var content = new Rect(0f, 0f, view.width - 18f, Mathf.Max(view.height - 2f, list.Count * (RowH + 4f)));
            scroll = GUI.BeginScrollView(view, scroll, content);
            PieceDef chosen = null;
            for (int i = 0; i < list.Count; i++)
            {
                var piece = list[i];
                var row = new Rect(0f, i * (RowH + 4f), content.width, RowH);
                Gui.Bevel(row);
                if (row.Contains(e.mousePosition))
                    Gui.Highlight(row, false);
                Gui.DrawIcon(new Rect(row.x + 3f, row.y + 3f, Gui.Slot, Gui.Slot), piece.Icon);
                bool levelOk = level >= piece.Level;
                Gui.Shadowed(new Rect(row.x + Gui.Slot + 10f, row.y + 3f, 260f, 20f), "<b>" + piece.Name + "</b>", Gui.Label);
                Gui.Shadowed(new Rect(row.x, row.y + 3f, row.width - 8f, 20f), "<color=" + (levelOk ? Gui.GoodHex : Gui.BadHex) + ">Level " + piece.Level + "</color>"
                    + "  <color=" + Gui.MutedHex + ">" + piece.Xp + " xp</color>", Gui.RightSmall);
                GUI.Label(new Rect(row.x + Gui.Slot + 10f, row.y + 23f, row.width - Gui.Slot - 14f, 18f), Costs(piece), Gui.Small);
                if (e.type == EventType.MouseDown && row.Contains(e.mousePosition))
                {
                    chosen = piece;
                    e.Use();
                }
            }
            GUI.EndScrollView();
            Divider(r, r.yMax - 42f);
            GUI.Label(new Rect(r.x + Gui.Pad, r.yMax - 38f, r.width - Gui.Pad * 2, 36f),
                "<color=" + Gui.MutedHex + ">Pick a piece, then click where it goes. R turns it; right-click or Esc stops. With a hammer, right-click a piece to take it down.</color>", Gui.Rich);
            if (chosen != null)
            {
                hud.CloseWindow();
                G.Player.StartBuilding(chosen);
            }
        }

        /// "2 planks, 2 nails", each green if you have enough, red if not.
        static string Costs(PieceDef piece)
        {
            var parts = new List<string>();
            foreach (var input in piece.Inputs)
            {
                var def = ContentDb.Current.Item(input.ItemId);
                bool have = G.Inventory.Count(input.ItemId) >= input.Count;
                parts.Add("<color=" + (have ? Gui.GoodHex : Gui.BadHex) + ">" + input.Count + " " + (def != null ? def.Name.ToLowerInvariant() : input.ItemId) + "</color>");
            }
            return string.Join(", ", parts.ToArray());
        }
    }
}
