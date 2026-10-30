using System.Collections.Generic;
using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The prayers: click one to turn it on or off.
    public sealed class PrayerTab : PanelTab
    {
        public override string Name => "Pray";

        static Icon litStar, dimStar;

        /// The prayer symbol, glowing when a prayer is on.
        public static void DrawStar(Rect r, bool lit)
        {
            if (litStar == null)
            {
                litStar = PrayerModule.Star(new Rgb(1f, 0.95f, 0.55f));
                dimStar = PrayerModule.Star(new Rgb(0.75f, 0.72f, 0.55f));
            }
            Gui.DrawIcon(r, lit ? litStar : dimStar);
        }

        public override void Draw(Hud hud, Rect body)
        {
            var e = Event.current;
            var pr = G.Prayer;
            int level = G.Skills.GetLevel(SkillIds.Prayer);
            Gui.Heading(body, "PRAYER", "<color=" + Gui.BlueHex + ">" + pr.Points + " / " + pr.Max + "</color>");
            float top = body.y + 20f;
            PrayerDef tip = null;
            for (int i = 0; i < Prayers.All.Length; i++)
            {
                var def = Prayers.All[i];
                var slot = new Rect(body.x + (i % 4) * (Gui.Slot + Gui.Gap), top + (i / 4) * (Gui.Slot + Gui.Gap), Gui.Slot, Gui.Slot);
                bool on = pr.IsActive(def.Id), unlocked = level >= def.Level;
                var inner = new Rect(slot.x + 1f, slot.y + 1f, slot.width - 2f, slot.height - 2f);
                Gui.Bevel(slot);
                if (on)
                    Gui.Highlight(slot, true);
                DrawIcon(new Rect(slot.x + 9f, slot.y + 9f, 22f, 22f), def);
                if (!unlocked)
                {
                    Gui.Fill(inner, Gui.Locked);
                    Gui.Shadowed(new Rect(slot.x, slot.yMax - 15f, slot.width, 14f), "<color=" + Gui.BadHex + ">Lv " + def.Level + "</color>", Gui.Centered);
                }
                if (!slot.Contains(e.mousePosition))
                    continue;
                Gui.Highlight(slot, false);
                tip = def;
                if (e.type == EventType.MouseDown)
                {
                    var id = def.Id;
                    if (e.button == 1)
                        hud.OpenMenu(new List<MenuOption> { new MenuOption(on ? "Deactivate" : "Activate", MenuOption.Item(def.Name), () => G.TogglePrayer(id)) }, false);
                    else
                        G.TogglePrayer(id);
                    e.Use();
                }
            }
            int rows = (Prayers.All.Length + 3) / 4;
            GUI.Label(new Rect(body.x, top + rows * (Gui.Slot + Gui.Gap) + 4f, body.width, 90f),
                "<color=" + Gui.MutedHex + ">Click a prayer to turn it on or off. Prayers drain your points; recharge them at the altar by the bank. Bury bones to train Prayer.</color>", Gui.Rich);
            if (tip != null)
            {
                float seconds = PrayerBook.DrainResistance / (float)tip.DrainEffect * GameTime.TickSeconds;
                var t = new Rect(Mathf.Max(4f, hud.PanelRect.x - 236f), Mathf.Clamp(e.mousePosition.y - 30f, 4f, hud.H - 74f), 230f, 70f);
                Gui.TipBox(t);
                Gui.Shadowed(new Rect(t.x + 8f, t.y + 3f, t.width - 16f, 18f), "<b><color=" + Gui.GoldHex + ">" + tip.Name + "</color></b>  (level " + tip.Level + ")", Gui.Small);
                GUI.Label(new Rect(t.x + 8f, t.y + 20f, t.width - 16f, 30f), tip.Description, Gui.Small);
                GUI.Label(new Rect(t.x + 8f, t.y + 48f, t.width - 16f, 18f), "<color=" + Gui.MutedHex + ">Drains a point every " + seconds.ToString("0.#") + " s</color>", Gui.Small);
            }
        }

        /// No art yet: each prayer's icon is its skill's icon (shield, fist, sword), with a mark for the stronger tier.
        public static void DrawIcon(Rect b, PrayerDef def)
        {
            if (def.Group == PrayerGroup.Protection)
            {
                // A blue shield with a sword across it.
                float cx = b.center.x, cy = b.center.y;
                var blue = new Color(0.35f, 0.7f, 1f);
                Gui.Fill(new Rect(cx - 8f, cy - 9f, 16f, 11f), blue);
                Gui.Fill(new Rect(cx - 6f, cy + 2f, 12f, 4f), blue);
                Gui.Fill(new Rect(cx - 4f, cy + 6f, 8f, 3f), blue);
                Gui.FillRotated(new Rect(cx - 1f, cy - 10f, 2f, 20f), new Color(0.9f, 0.92f, 0.95f), 45f, b.center);
                return;
            }
            var skill = def.Group == PrayerGroup.Defence ? SkillIds.Defence : def.Group == PrayerGroup.Strength ? SkillIds.Strength : SkillIds.Attack;
            Gui.DrawIcon(b, ContentDb.Current.Skill(skill).Icon);
            if (def.Boost > 0.05)
                Gui.Fill(new Rect(b.xMax - 3f, b.y - 2f, 4f, 4f), new Color(1f, 0.9f, 0.4f));
        }
    }
}
