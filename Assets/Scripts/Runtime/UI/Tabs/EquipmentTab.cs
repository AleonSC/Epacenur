using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Worn equipment, laid out like RuneScape's, with the bonuses and the attack style.
    public sealed class EquipmentTab : PanelTab
    {
        public override string Name => "Worn";

        // Where each slot sits in the 3-column layout (column, row).
        static readonly EquipSlot[] Slots =
        {
            EquipSlot.Head, EquipSlot.Cape, EquipSlot.Neck, EquipSlot.Ammo, EquipSlot.Weapon, EquipSlot.Body,
            EquipSlot.Shield, EquipSlot.Legs, EquipSlot.Hands, EquipSlot.Feet, EquipSlot.Ring,
        };
        static readonly int[] Cols = { 1, 0, 1, 2, 0, 1, 2, 1, 0, 1, 2 };
        static readonly int[] Rows = { 0, 1, 1, 1, 2, 2, 2, 3, 4, 4, 4 };

        public override void Draw(Hud hud, Rect body)
        {
            var eq = G.Equipment;
            var e = Event.current;
            const float rowH = 46f, slotSize = Gui.Slot;
            float colGap = (body.width - slotSize * 3) / 2f;
            Gui.Shadowed(new Rect(body.x, body.y - 2f, body.width, 18f), "<b>Worn Equipment</b>", Gui.Small);
            float top = body.y + 18f;

            // Connecting lines: down the middle column, across the rows with side slots,
            // and from the weapon and shield down to the gloves and ring.
            var line = new Color(0.1f, 0.08f, 0.06f, 0.9f);
            float cx = body.x + slotSize + colGap + slotSize / 2f;
            Gui.Fill(new Rect(cx - 1f, top + slotSize, 2f, rowH * 4 - slotSize), line);
            foreach (int row in new[] { 1, 2, 4 })
            {
                float y = top + row * rowH + slotSize / 2f;
                Gui.Fill(new Rect(body.x + slotSize, y - 1f, body.width - slotSize * 2, 2f), line);
            }
            Gui.Fill(new Rect(body.x + slotSize / 2f - 1f, top + 2 * rowH + slotSize, 2f, rowH * 2 - slotSize), line);
            Gui.Fill(new Rect(body.x + body.width - slotSize / 2f - 1f, top + 2 * rowH + slotSize, 2f, rowH * 2 - slotSize), line);

            string tooltip = null;
            for (int i = 0; i < Slots.Length; i++)
            {
                var slot = Slots[i];
                var r = new Rect(body.x + Cols[i] * (slotSize + colGap), top + Rows[i] * rowH, slotSize, slotSize);
                Gui.Fill(r, new Color(0.12f, 0.1f, 0.07f, 1f));
                Gui.Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), new Color(0.3f, 0.26f, 0.2f, 1f));
                var worn = eq[slot];
                if (worn.IsEmpty)
                {
                    // An empty slot shows what goes there, faintly.
                    Gui.Tinted(r, "<size=9>" + slot + "</size>", Gui.Centered, new Color(1f, 1f, 1f, 0.25f));
                    continue;
                }
                Gui.DrawItemIcon(r, worn.Def);
                if (r.Contains(e.mousePosition))
                    tooltip = "Remove " + worn.Def.Name;
                if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
                {
                    if (e.button == 1)
                        hud.OpenMenu(Options(slot, worn.Def), false);
                    else
                        Unequip(slot);
                    e.Use();
                }
            }

            // Bonuses (attack and defence for stab / slash / crush, strength, speed) and the attack styles.
            float y0 = top + 5 * rowH - 6f;
            var b = eq.Bonuses;
            // A small table: stab, slash, crush and ranged columns.
            float[] cols = { body.x + 50f, body.x + 82f, body.x + 116f, body.x + 150f };
            string[] heads = { "Stab", "Slash", "Crush", "Rngd" };
            for (int i = 0; i < 4; i++)
                GUI.Label(new Rect(cols[i], y0, 40f, 16f), "<color=#c8c0a8>" + heads[i] + "</color>", Gui.Small);
            GUI.Label(new Rect(body.x, y0 + 13f, 50f, 16f), "Attack", Gui.Small);
            GUI.Label(new Rect(body.x, y0 + 26f, 50f, 16f), "Defence", Gui.Small);
            int[] att = { b.StabAttack, b.SlashAttack, b.CrushAttack, b.RangedAttack };
            int[] def = { b.StabDefence, b.SlashDefence, b.CrushDefence, b.RangedDefence };
            for (int i = 0; i < 4; i++)
            {
                GUI.Label(new Rect(cols[i], y0 + 13f, 40f, 16f), Bonus(att[i]), Gui.Small);
                GUI.Label(new Rect(cols[i], y0 + 26f, 40f, 16f), Bonus(def[i]), Gui.Small);
            }
            GUI.Label(new Rect(body.x, y0 + 39f, body.width, 16f), "Str " + Bonus(b.Strength) + "  Ranged str " + Bonus(b.RangedStrength) + "  <color=#ffff00>"
                + (eq.AttackTicks * GameTime.TickSeconds).ToString("0.0") + " s</color>", Gui.Small);
            var kind = ContentDb.Current.WeaponKind(eq.WeaponKind);
            var current = G.CurrentStyle;
            float bw = (body.width - 3f * (kind.Styles.Length - 1)) / kind.Styles.Length;
            for (int i = 0; i < kind.Styles.Length; i++)
            {
                var st = kind.Styles[i];
                var r = new Rect(body.x + i * (bw + 3f), y0 + 56f, bw, 22f);
                if (GUI.Toggle(r, st == current, st.Name, Gui.TabStyle))
                    G.StyleIndex = i;
                if (r.Contains(e.mousePosition))
                    tooltip = st.Name + ": " + st.Type.ToString().ToLowerInvariant() + ", trains "
                        + (kind.Ranged ? (st.Style == CombatStyle.Longrange ? "Ranged and Defence" : "Ranged") : ContentDb.Current.Skill(CombatRules.TrainedSkill(st.Style)).Name);
            }
            if (tooltip != null)
                Gui.Tooltip(new Rect(e.mousePosition.x - 150f, e.mousePosition.y + 16f, 200f, 20f), tooltip, Gui.Small);
        }

        /// "+4" in yellow, "-2" in red.
        static string Bonus(int n) => (n < 0 ? "<color=#ff6060>" : "<color=#ffff00>+") + n + "</color>";

        static List<MenuOption> Options(EquipSlot slot, ItemDef def)
        {
            string name = MenuOption.Item(def.Name);
            return new List<MenuOption>
            {
                new MenuOption("Remove", name, () => Unequip(slot)),
                new MenuOption("Examine", name, () => GameLog.Add(def.Examine)),
            };
        }

        static void Unequip(EquipSlot slot)
        {
            if (!G.Equipment.Unequip(slot, G.Inventory))
                GameLog.Add("You don't have enough free inventory space to do that.");
        }
    }
}
