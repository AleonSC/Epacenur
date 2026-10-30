using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The melee abilities, keys 1-9, with their cooldowns.
    public sealed class ActionBar
    {
        static Game G => Game.Instance;

        public void Draw(Hud hud)
        {
            var r = hud.BarRect;
            var e = Event.current;
            var p = G.Player;
            float now = Time.time;
            Gui.PanelBox(r);
            AbilityDef tip = null;
            for (int i = 0; i < Abilities.All.Length; i++)
            {
                var def = Abilities.All[i];
                var slot = new Rect(r.x + 6f + i * (Gui.Slot + Gui.Gap), r.y + 6f, Gui.Slot, Gui.Slot);
                Gui.Bevel(slot);
                var inner = new Rect(slot.x + 2f, slot.y + 2f, slot.width - 4f, slot.height - 4f);
                Gui.Fill(inner, Gui.Shade(new Color(def.R, def.G, def.B), 0.5f));
                Gui.Fill(new Rect(inner.x, inner.y, inner.width, inner.height / 2f), new Color(1f, 1f, 1f, 0.07f));
                Gui.Fill(new Rect(inner.x, inner.yMax - 2f, inner.width, 2f), Gui.Shade(new Color(def.R, def.G, def.B), 0.85f));
                string abbr = def.Id == AbilityId.Backhand ? "Bh" : def.Name.Substring(0, 2);
                Gui.Shadowed(inner, "<b><size=15>" + abbr + "</size></b>", Gui.Centered);
                Gui.Shadowed(new Rect(slot.x + 3f, slot.y + 1f, 12f, 14f), "<color=#ffffff>" + (i + 1) + "</color>", Gui.Small);

                // Unusable: no level.
                bool hasLevel = p.HasLevelFor(def);
                if (!hasLevel)
                    Gui.Fill(inner, Gui.Locked);
                if (!hasLevel)
                    Gui.Shadowed(new Rect(slot.x, slot.yMax - 15f, slot.width, 14f), "<color=" + Gui.BadHex + ">Lv " + def.Level + "</color>", Gui.Centered);

                // Cooldown: a dark curtain that shrinks as it comes back, and the seconds left.
                float f = p.Cooldowns.Fraction(def.Id, now);
                if (f > 0f)
                {
                    Gui.Fill(new Rect(inner.x, inner.y, inner.width, inner.height * f), new Color(0f, 0f, 0f, 0.6f));
                    float left = p.Cooldowns.Remaining(def.Id, now);
                    if (left > Abilities.GlobalCooldownSeconds)
                        Gui.Shadowed(inner, "<b>" + Mathf.CeilToInt(left) + "</b>", Gui.Centered);
                }

                if (!slot.Contains(e.mousePosition))
                    continue;
                Gui.Highlight(slot, false);
                tip = def;
                if (e.type == EventType.MouseDown)
                {
                    var id = def.Id;
                    if (e.button == 1)
                        hud.OpenMenu(new List<MenuOption>
                        {
                            new MenuOption("Activate", MenuOption.Item(def.Name), () => G.Player.UseAbility(id)),
                            new MenuOption("Examine", MenuOption.Item(def.Name), () => GameLog.Add(def.Description)),
                        }, false);
                    else
                        p.UseAbility(id);
                    e.Use();
                }
            }

            if (tip != null)
            {
                const float w = 230f, h = 70f;
                var t = new Rect(Mathf.Clamp(e.mousePosition.x - w / 2f, 4f, hud.W - w - 4f), r.y - h - 4f, w, h);
                Gui.TipBox(t);
                bool ok = p.HasLevelFor(tip);
                Gui.Shadowed(new Rect(t.x + 8f, t.y + 3f, w - 16f, 18f), "<b><color=" + Gui.GoldHex + ">" + tip.Name + "</color></b>", Gui.Small);
                GUI.Label(new Rect(t.x + 8f, t.y + 19f, w - 16f, 16f),
                    "<color=" + (ok ? Gui.GoodHex : Gui.BadHex) + ">" + "Requires " + ContentDb.Current.Skill(tip.Skill).Name + " " + tip.Level + "</color>   Cooldown " + tip.CooldownSeconds + " s", Gui.Small);
                GUI.Label(new Rect(t.x + 8f, t.y + 35f, w - 16f, 34f), tip.Description, Gui.Rich);
            }
        }
    }

    /// Your health (left) and prayer points (right): bars that empty as they're used, with the numbers on them.
    public sealed class StatusBars
    {
        static Game G => Game.Instance;

        public void Draw(Hud hud)
        {
            var whole = hud.StatusRect;
            var r = new Rect(whole.x, whole.y, whole.width / 2f - 2f, whole.height);
            DrawPrayer(new Rect(whole.x + whole.width / 2f + 2f, whole.y, whole.width / 2f - 2f, whole.height));
            var hp = G.Health;
            Gui.Bevel(r);
            // A small heart.
            var red = new Color(0.85f, 0.12f, 0.15f);
            float hx = r.x + 6f, hy = r.y + 5f;
            Gui.Fill(new Rect(hx, hy, 5f, 5f), red);
            Gui.Fill(new Rect(hx + 6f, hy, 5f, 5f), red);
            Gui.Fill(new Rect(hx, hy + 3f, 11f, 5f), red);
            Gui.Fill(new Rect(hx + 2f, hy + 8f, 7f, 2f), red);
            Gui.Fill(new Rect(hx + 4f, hy + 10f, 3f, 2f), red);
            var bar = new Rect(r.x + 22f, r.y + 3f, r.width - 25f, r.height - 6f);
            Color fill = hp.Fraction > 0.5f ? new Color(0.72f, 0.12f, 0.13f) : hp.Fraction > 0.25f ? new Color(0.85f, 0.4f, 0.08f) : new Color(1f, 0.15f, 0.1f);
            Gui.Bar(bar, hp.Fraction, fill, new Color(0.22f, 0.05f, 0.05f), hp.Current + " / " + hp.Max);
            if (r.Contains(Event.current.mousePosition))
                Gui.Tooltip(new Rect(r.x, r.y - 20f, 300f, 18f), "Hitpoints: eat cooked food to heal. Lost health returns 1 a minute.", Gui.Small);
        }

        void DrawPrayer(Rect r)
        {
            var pr = G.Prayer;
            var e = Event.current;
            Gui.Bevel(r);
            PrayerTab.DrawStar(new Rect(r.x + 1f, r.y, 22f, 22f), pr.AnyActive);
            var bar = new Rect(r.x + 22f, r.y + 3f, r.width - 25f, r.height - 6f);
            Gui.Bar(bar, pr.Fraction, pr.AnyActive ? new Color(0.25f, 0.72f, 0.92f) : new Color(0.15f, 0.48f, 0.72f), new Color(0.03f, 0.1f, 0.17f), pr.Points + " / " + pr.Max);
            if (r.Contains(e.mousePosition))
            {
                Gui.Tooltip(new Rect(r.x - 60f, r.y - 20f, 320f, 18f), "Prayer points: recharge at the altar. Click to turn all prayers off.", Gui.Small);
                if (e.type == EventType.MouseDown)
                {
                    pr.DeactivateAll();
                    e.Use();
                }
            }
        }
    }
}
