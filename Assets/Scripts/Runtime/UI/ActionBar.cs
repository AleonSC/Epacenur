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
            Gui.Fill(r, new Color(0.24f, 0.2f, 0.15f, 0.92f));
            AbilityDef tip = null;
            for (int i = 0; i < Abilities.All.Length; i++)
            {
                var def = Abilities.All[i];
                var slot = new Rect(r.x + 6f + i * (Gui.Slot + Gui.Gap), r.y + 6f, Gui.Slot, Gui.Slot);
                Gui.Bevel(slot);
                var inner = new Rect(slot.x + 2f, slot.y + 2f, slot.width - 4f, slot.height - 4f);
                Gui.Fill(inner, Gui.Shade(new Color(def.R, def.G, def.B), 0.55f));
                Gui.Fill(new Rect(inner.x, inner.y, inner.width, inner.height / 2f), new Color(1f, 1f, 1f, 0.08f));
                string abbr = def.Id == AbilityId.Backhand ? "Bh" : def.Name.Substring(0, 2);
                Gui.Shadowed(inner, "<b><size=15>" + abbr + "</size></b>", Gui.Centered);
                Gui.Shadowed(new Rect(slot.x + 3f, slot.y + 1f, 12f, 14f), "<color=#ffffff>" + (i + 1) + "</color>", Gui.Small);

                // Unusable: no level.
                bool hasLevel = p.HasLevelFor(def);
                if (!hasLevel)
                    Gui.Fill(inner, new Color(0f, 0f, 0f, 0.55f));
                if (!hasLevel)
                    Gui.Shadowed(new Rect(slot.x, slot.yMax - 15f, slot.width, 14f), "<color=#ff6060>Lv " + def.Level + "</color>", Gui.Centered);

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
                Gui.Fill(t, new Color(0.06f, 0.05f, 0.04f, 0.95f));
                bool ok = p.HasLevelFor(tip);
                Gui.Shadowed(new Rect(t.x + 8f, t.y + 3f, w - 16f, 18f), "<b><color=#ffcc66>" + tip.Name + "</color></b>", Gui.Small);
                GUI.Label(new Rect(t.x + 8f, t.y + 19f, w - 16f, 16f),
                    (ok ? "<color=#a0ffa0>" : "<color=#ff6060>") + "Requires " + ContentDb.Current.Skill(tip.Skill).Name + " " + tip.Level + "</color>   Cooldown " + tip.CooldownSeconds + " s", Gui.Small);
                GUI.Label(new Rect(t.x + 8f, t.y + 35f, w - 16f, 34f), tip.Description, Gui.Rich);
            }
        }
    }

    /// Your health, prayer points and run energy: bars that empty as they're used, with the numbers on them.
    public sealed class StatusBars
    {
        static Game G => Game.Instance;

        public void Draw(Hud hud)
        {
            var whole = hud.StatusRect;
            float w = (whole.width - 8f) / 3f;
            var r = new Rect(whole.x, whole.y, w, whole.height);
            DrawPrayer(new Rect(whole.x + w + 4f, whole.y, w, whole.height));
            DrawEnergy(new Rect(whole.x + 2f * (w + 4f), whole.y, w, whole.height));
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
            var bar = new Rect(r.x + 22f, r.y + 4f, r.width - 26f, r.height - 8f);
            Gui.Fill(bar, new Color(0.25f, 0.04f, 0.04f));
            Color fill = hp.Fraction > 0.5f ? new Color(0.75f, 0.1f, 0.12f) : hp.Fraction > 0.25f ? new Color(0.85f, 0.4f, 0.08f) : new Color(1f, 0.15f, 0.1f);
            Gui.Fill(new Rect(bar.x, bar.y, bar.width * hp.Fraction, bar.height), fill);
            Gui.Fill(new Rect(bar.x, bar.y, bar.width * hp.Fraction, 2f), new Color(1f, 1f, 1f, 0.15f));
            Gui.Shadowed(bar, "<b>" + hp.Current + " / " + hp.Max + "</b>", Gui.Centered);
            if (r.Contains(Event.current.mousePosition))
                Gui.Tooltip(new Rect(r.x, r.y - 20f, 300f, 18f), "Hitpoints: eat cooked food to heal. Lost health returns 1 a minute.", Gui.Small);
        }

        void DrawEnergy(Rect r)
        {
            var en = G.Energy;
            Gui.Bevel(r);
            // A little boot.
            var boot = new Color(0.9f, 0.75f, 0.3f);
            Gui.Fill(new Rect(r.x + 7f, r.y + 4f, 5f, 9f), boot);
            Gui.Fill(new Rect(r.x + 7f, r.y + 12f, 10f, 5f), boot);
            var bar = new Rect(r.x + 22f, r.y + 4f, r.width - 26f, r.height - 8f);
            float f = (float)(en.Value / RunEnergy.Max);
            Gui.Fill(bar, new Color(0.2f, 0.16f, 0.04f));
            Gui.Fill(new Rect(bar.x, bar.y, bar.width * f, bar.height), G.Player.IsRunning ? new Color(0.95f, 0.8f, 0.2f) : new Color(0.75f, 0.6f, 0.15f));
            Gui.Fill(new Rect(bar.x, bar.y, bar.width * f, 2f), new Color(1f, 1f, 1f, 0.15f));
            Gui.Shadowed(bar, "<b>" + Mathf.FloorToInt((float)en.Value) + "%</b>", Gui.Centered);
            if (r.Contains(Event.current.mousePosition))
                Gui.Tooltip(new Rect(r.x - 120f, r.y - 20f, 340f, 18f), "Run energy: running uses it, walking brings it back (faster with Agility).", Gui.Small);
        }

        void DrawPrayer(Rect r)
        {
            var pr = G.Prayer;
            var e = Event.current;
            Gui.Bevel(r);
            PrayerTab.DrawStar(new Rect(r.x + 1f, r.y, 22f, 22f), pr.AnyActive);
            var bar = new Rect(r.x + 22f, r.y + 4f, r.width - 26f, r.height - 8f);
            Gui.Fill(bar, new Color(0.03f, 0.12f, 0.2f));
            Gui.Fill(new Rect(bar.x, bar.y, bar.width * pr.Fraction, bar.height), pr.AnyActive ? new Color(0.25f, 0.75f, 0.95f) : new Color(0.15f, 0.5f, 0.75f));
            Gui.Fill(new Rect(bar.x, bar.y, bar.width * pr.Fraction, 2f), new Color(1f, 1f, 1f, 0.15f));
            Gui.Shadowed(bar, "<b>" + pr.Points + " / " + pr.Max + "</b>", Gui.Centered);
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
