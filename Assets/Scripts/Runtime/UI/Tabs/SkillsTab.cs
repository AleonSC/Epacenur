using System.Numerics;
using Epacenur.Core;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;

namespace Epacenur.Runtime
{
    /// Every skill, laid out like RuneScape 3's skills panel, with a hover box for each.
    public sealed class SkillsTab : PanelTab
    {
        public override string Name => "Skills";

        public override void Draw(Hud hud, Rect body)
        {
            const int cols = 3;
            const float gap = 3f, cellH = 30f, footH = 28f;
            float cellW = (body.width - gap * (cols - 1)) / cols;
            var e = Event.current;
            Gui.Shadowed(new Rect(body.x, body.y - 2f, body.width, 18f), "<b>SKILLS</b>", Gui.Small);
            float top = body.y + 18f;
            SkillDef hovered = null;
            Rect hoveredRect = default(Rect);

            // One cell per skill: its icon, then the level twice, stacked (current over base,
            // as in RuneScape; they're the same until there are boosts).
            var defs = ContentDb.Current.Skills;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                var cell = new Rect(body.x + (i % cols) * (cellW + gap), top + (i / cols) * (cellH + gap), cellW, cellH);
                Gui.Bevel(cell);
                if (cell.Contains(e.mousePosition))
                {
                    Gui.Fill(new Rect(cell.x + 1f, cell.y + 1f, cell.width - 2f, cell.height - 2f), new Color(1f, 1f, 1f, 0.08f));
                    hovered = def;
                    hoveredRect = cell;
                }
                Gui.DrawIcon(new Rect(cell.x + 3f, cell.y + 4f, 22f, 22f), def.Icon);

                int level = G.Skills.GetLevel(def.Id);
                var numbers = new Rect(cell.x + 25f, cell.y + 1f, cell.width - 28f, cell.height - 2f);
                Gui.Fill(new Rect(numbers.x + 4f, numbers.y + numbers.height / 2f, numbers.width - 4f, 1f), new Color(1f, 1f, 1f, 0.12f));
                string text = "<color=#e8e2d0>" + level + "</color>";
                Gui.Shadowed(new Rect(numbers.x, numbers.y - 1f, numbers.width, 16f), text, Gui.RightSmall);
                Gui.Shadowed(new Rect(numbers.x, numbers.y + numbers.height / 2f - 1f, numbers.width, 16f), text, Gui.RightSmall);
            }

            // Footer: total level, combat level, total xp.
            var foot = new Rect(body.x, body.yMax - footH, body.width, footH);
            float boxW = (foot.width - gap * 2) / 3f;
            BigInteger totalXp = BigInteger.Zero;
            foreach (var def in defs)
                totalXp += G.Skills.GetXp(def.Id);
            string[] values = { G.Skills.TotalLevel.ToString(), CombatRules.CombatLevel(G.Skills).ToString(), Experience.FormatShort(totalXp) };
            string[] tips = { "Total level", "Combat level", "Total XP: " + Experience.Format(totalXp) };
            string footTip = null;
            for (int i = 0; i < 3; i++)
            {
                var box = new Rect(foot.x + i * (boxW + gap), foot.y, boxW, foot.height);
                Gui.Bevel(box);
                DrawFooterIcon(new Rect(box.x + 3f, box.y + 4f, 20f, 20f), i);
                Gui.Shadowed(new Rect(box.x + 22f, box.y + 6f, box.width - 25f, 18f), "<color=#e8e2d0>" + values[i] + "</color>", Gui.RightSmall);
                if (box.Contains(e.mousePosition))
                    footTip = tips[i];
            }
            if (footTip != null)
                Gui.Tooltip(new Rect(e.mousePosition.x - 210f, e.mousePosition.y - 22f, 200f, 20f), footTip, Gui.RightSmall);

            if (hovered != null)
                DrawTooltip(hud, hovered, hoveredRect);
        }

        /// The hover box: name, xp, next level, xp left, and a progress bar.
        void DrawTooltip(Hud hud, SkillDef def, Rect cell)
        {
            BigInteger xp = G.Skills.GetXp(def.Id);
            int level = Experience.LevelForXp(xp);
            const float w = 210f, h = 84f;
            // To the left of the panel, level with the cell.
            var r = new Rect(Mathf.Max(4f, hud.PanelRect.x - w - 6f), Mathf.Clamp(cell.y - 10f, 4f, hud.H - h - 4f), w, h);
            Gui.Box(r);
            Gui.Shadowed(new Rect(r.x + 8f, r.y + 4f, w, 18f), "<b><color=#ffcc66>" + def.Name + "</color></b>  level " + level + " / " + Experience.MaxLevel, Gui.Small);
            GUI.Label(new Rect(r.x + 8f, r.y + 22f, w - 16f, 16f), "XP: " + Experience.Format(xp), Gui.Small);
            if (level < Experience.MaxLevel)
            {
                GUI.Label(new Rect(r.x + 8f, r.y + 37f, w - 16f, 16f), "Next level at: " + Experience.FormatShort(Experience.XpForLevel(level + 1)), Gui.Small);
                GUI.Label(new Rect(r.x + 8f, r.y + 52f, w - 16f, 16f), "Remaining: " + Experience.FormatShort(Experience.XpToNextLevel(xp)), Gui.Small);
            }
            else
            {
                GUI.Label(new Rect(r.x + 8f, r.y + 37f, w - 16f, 16f), "Maxed!", Gui.Small);
            }
            var bar = new Rect(r.x + 8f, r.yMax - 12f, w - 16f, 6f);
            Gui.Fill(bar, new Color(0.5f, 0.1f, 0.1f));
            Gui.Fill(new Rect(bar.x, bar.y, bar.width * (float)Experience.LevelProgress(xp), bar.height), new Color(0.2f, 0.75f, 0.2f));
        }

        static void DrawFooterIcon(Rect b, int which)
        {
            float cx = b.center.x, cy = b.center.y;
            if (which == 0)
            {
                // Total level: three bars.
                Gui.Fill(new Rect(cx - 7f, cy + 1f, 4f, 7f), new Color(0.85f, 0.2f, 0.2f));
                Gui.Fill(new Rect(cx - 2f, cy - 3f, 4f, 11f), new Color(0.25f, 0.75f, 0.3f));
                Gui.Fill(new Rect(cx + 3f, cy - 7f, 4f, 15f), new Color(0.3f, 0.5f, 0.9f));
            }
            else if (which == 1)
            {
                // Combat level: crossed swords.
                var steel = new Color(0.78f, 0.8f, 0.84f);
                Gui.FillRotated(new Rect(cx - 1f, cy - 9f, 2f, 18f), steel, 45f, b.center);
                Gui.FillRotated(new Rect(cx - 1f, cy - 9f, 2f, 18f), steel, -45f, b.center);
                Gui.FillRotated(new Rect(cx - 4f, cy + 4f, 8f, 2f), new Color(0.9f, 0.72f, 0.2f), 45f, b.center);
                Gui.FillRotated(new Rect(cx - 4f, cy + 4f, 8f, 2f), new Color(0.9f, 0.72f, 0.2f), -45f, b.center);
            }
            else
            {
                // Total xp: a blue star.
                var blue = new Color(0.35f, 0.6f, 1f);
                Gui.Fill(new Rect(cx - 7f, cy - 2f, 14f, 4f), blue);
                Gui.Fill(new Rect(cx - 2f, cy - 7f, 4f, 14f), blue);
                Gui.FillDiamond(cx, cy, 7f, blue);
                Gui.Fill(new Rect(cx - 2f, cy - 2f, 4f, 4f), Color.white);
            }
        }
    }
}
