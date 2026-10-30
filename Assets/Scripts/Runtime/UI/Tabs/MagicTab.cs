using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The spellbook: click a combat spell to autocast it (again to stop), a teleport to cast it, or an
    /// alchemy spell and then an item in the inventory.
    public sealed class MagicTab : PanelTab
    {
        public override string Name => "Magic";

        public override void Draw(Hud hud, Rect body)
        {
            var e = Event.current;
            var player = G.Player;
            var spells = ContentDb.Current.Spells;
            int level = G.Skills.GetLevel(SkillIds.Magic);
            Gui.Shadowed(new Rect(body.x, body.y - 2f, body.width, 18f), "<b>MAGIC</b>", Gui.Small);
            Gui.Shadowed(new Rect(body.x, body.y - 2f, body.width, 18f), "<color=#7f9fff>Level " + level + "</color>", Gui.RightSmall);
            float top = body.y + 20f;
            SpellDef tip = null;
            for (int i = 0; i < spells.Count; i++)
            {
                var spell = spells[i];
                var slot = new Rect(body.x + (i % 4) * (Gui.Slot + Gui.Gap), top + (i / 4) * (Gui.Slot + Gui.Gap), Gui.Slot, Gui.Slot);
                var inner = new Rect(slot.x + 1f, slot.y + 1f, slot.width - 2f, slot.height - 2f);
                bool on = G.Autocast == spell || player.SelectedSpell == spell;
                bool unlocked = level >= spell.Level;
                bool castable = unlocked && Magic.Problem(spell, G.Skills, G.Inventory, G.Equipment) == null;
                Gui.Bevel(slot);
                if (on)
                    Gui.Fill(inner, new Color(0.45f, 0.55f, 1f, 0.4f));
                if (spell.Icon != null)
                    Gui.DrawIcon(new Rect(slot.x + 9f, slot.y + 9f, 22f, 22f), spell.Icon);
                if (!unlocked)
                {
                    Gui.Fill(inner, new Color(0.12f, 0.11f, 0.09f, 0.7f));
                    Gui.Shadowed(new Rect(slot.x, slot.yMax - 15f, slot.width, 14f), "<color=#ff6060>Lv " + spell.Level + "</color>", Gui.Centered);
                }
                else if (!castable)
                    Gui.Fill(inner, new Color(0.12f, 0.11f, 0.09f, 0.45f));  // not enough runes
                if (!slot.Contains(e.mousePosition))
                    continue;
                tip = spell;
                if (e.type == EventType.MouseDown)
                {
                    var s = spell;
                    if (e.button == 1)
                        hud.OpenMenu(new List<MenuOption> { new MenuOption(Verb(s), MenuOption.Item(s.Name), () => G.Player.CastSpell(s)) }, false);
                    else
                        player.CastSpell(s);
                    e.Use();
                }
            }
            int rows = (spells.Count + 3) / 4;
            string status = G.Autocast != null ? "Autocasting <color=#9fb8ff>" + G.Autocast.Name + "</color>. Click it again to fight with your weapon."
                : "Click a strike or bolt to cast it when you attack.";
            GUI.Label(new Rect(body.x, top + rows * (Gui.Slot + Gui.Gap) + 4f, body.width, 90f),
                status + " Runes come from altars out in the wilds (Runecrafting); a staff gives endless runes of its element.", Gui.Rich);
            if (tip != null)
                DrawTip(hud, e, tip);
        }

        static GUIStyle wrap;

        static string Verb(SpellDef s) => s.Kind == SpellKind.Combat ? (G.Autocast == s ? "Stop autocasting" : "Autocast") : "Cast";

        static void DrawTip(Hud hud, Event e, SpellDef s)
        {
            var lines = new List<string>();
            foreach (var r in s.Runes)
            {
                var def = ContentDb.Current.Item(r.ItemId);
                int have = G.Inventory.Count(r.ItemId);
                var weapon = G.Equipment.IsWearing(EquipSlot.Weapon) ? G.Equipment[EquipSlot.Weapon].Def : null;
                bool staff = weapon != null && weapon.InfiniteRune == r.ItemId;
                string colour = staff || have >= r.Count ? "#a0ffa0" : "#ff7070";
                lines.Add("<color=" + colour + ">" + r.Count + " " + def.Name.ToLowerInvariant() + (staff ? " (staff)" : " (" + have + ")") + "</color>");
            }
            string runes = lines.Count > 0 ? string.Join(", ", lines.ToArray()) : "No runes";
            float h = 78f;
            var t = new Rect(Mathf.Max(4f, hud.PanelRect.x - 256f), Mathf.Clamp(e.mousePosition.y - 30f, 4f, hud.H - h - 4f), 250f, h);
            Gui.Fill(t, new Color(0.06f, 0.05f, 0.04f, 0.95f));
            string xp = s.Xp > 0 ? "  " + s.Xp + " xp" : "";
            Gui.Shadowed(new Rect(t.x + 8f, t.y + 3f, t.width - 16f, 18f), "<b><color=#9fb8ff>" + s.Name + "</color></b>  (level " + s.Level + ")" + xp, Gui.Small);
            if (wrap == null)
                wrap = new GUIStyle(Gui.Small) { wordWrap = true };
            GUI.Label(new Rect(t.x + 8f, t.y + 20f, t.width - 16f, 34f), s.Description, wrap);
            GUI.Label(new Rect(t.x + 8f, t.y + 54f, t.width - 16f, 20f), runes, wrap);
        }
    }
}
