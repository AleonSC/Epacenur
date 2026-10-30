using System.Collections.Generic;
using System.Numerics;
using Epacenur.Core;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Epacenur.Runtime
{
    /// What's drawn over the view: names floating over places, hitsplats and health bars, the action
    /// under the mouse and the help text (top left), xp drops, level-ups, the chat box and the crosshair.
    public sealed class Overlays
    {
        struct XpDrop
        {
            public string Skill;
            public string Text;
            public float Born;
        }

        static Game G => Game.Instance;
        readonly List<XpDrop> drops = new List<XpDrop>();
        string levelUpText;
        float levelUpUntil;

        public void AddXpDrop(string skill, BigInteger tenths)
        {
            string xp = tenths % 10 == 0 ? Experience.Format(tenths / 10) : Experience.Format(tenths / 10) + "." + (tenths % 10);
            drops.Add(new XpDrop { Skill = skill, Text = "+" + xp + " xp", Born = Time.time });
        }

        public void ShowLevelUp(string skill, int level)
        {
            levelUpText = "Congratulations! " + ContentDb.Current.Skill(skill).Name + " level " + level;
            levelUpUntil = Time.time + 4f;
        }

        /// Things attached to the world: labels, health bars, hitsplats.
        public void DrawWorld(Hud hud)
        {
            DrawLabels(hud);
            DrawCombat(hud);
        }

        /// Fixed on screen: top left, xp drops, level-up, chat.
        public void DrawScreen(Hud hud)
        {
            DrawTopLeft(hud);
            DrawXpDrops(hud);
            DrawLevelUp(hud);
            DrawChat(hud);
        }

        static Vector2 ToGui(Hud hud, Vector3 screen) => new Vector2(screen.x / hud.Scale, (Screen.height - screen.y) / hud.Scale);

        void DrawLabels(Hud hud)
        {
            var cam = G.Rig.Cam;
            foreach (var l in G.World.Labels)
            {
                Vector3 sp = cam.WorldToScreenPoint(World.ToWorld(l.Position));
                if (sp.z <= 0f || sp.z > 60f)
                    continue;
                var c = l.Color;
                c.a = Mathf.Clamp01((60f - sp.z) / 15f);
                var at = ToGui(hud, sp);
                Gui.Tinted(new Rect(at.x - 120f, at.y - 12f, 240f, 24f), "<b>" + l.Text + "</b>", Gui.Centered, c);
            }
        }

        void DrawTopLeft(Hud hud)
        {
            var p = G.Player;
            // What left-click does, e.g. "Attack Goblin (level-2) / 2 more options", else what you're doing.
            string action = p.HoverText;
            if (action.Length == 0 && p.Activity != null)
                action = p.Activity.Status;
            else if (action.Length == 0 && p.IsFighting)
                action = "Fighting...";
            Gui.Shadowed(new Rect(10f, 8f, 600f, 22f), action, Gui.Title);

            string mode = p.Mode == ViewMode.Tile ? "Tile mode" : "First person";
            string pace = p.IsStunned ? "   <color=#ffff66>Stunned!</color>" : p.IsRunning ? "   <color=#7fd4ff>Running</color>" : "";
            Gui.Shadowed(new Rect(10f, 30f, 600f, 20f), "<color=#ffd966>" + mode + "</color>" + pace + "   (V to switch, H for help)", Gui.Small);
            if (!hud.ShowHelp)
                return;
            string help = p.Mode == ViewMode.Tile
                ? "Left-click: walk / chop / attack / take\nWASD: walk freely.  Hold Shift: run\nArrow keys or middle-drag: rotate camera\nScroll: zoom\nRight-click anything: all its options\n1-9: melee abilities\nClick an item: wear, eat... or Use it on something\nDrag items to move them.  Shift-click: drop.  Minimap: click to walk.  M: world map\nC: show or hide chat.  B: show or hide the inventory panel\nF1: dev tools"
                : "Mouse: look    WASD: walk    Hold Shift: run\nLeft-click: one chop or attack per click, take items\nEsc: free the mouse (click the view to grab it again)\nRight-click: all the options for what you aim at\n1-9: melee abilities\nClick an item: wear, eat... or Use it on something\nDrag items to move them.  Shift-click: drop.  Minimap: click to walk.  M: world map\nC: show or hide chat.  B: show or hide the inventory panel\nF1: dev tools";
            Gui.Shadowed(new Rect(10f, 50f, 440f, 150f), help, Gui.Small);
        }

        public void DrawCrosshair(Hud hud)
        {
            var p = G.Player;
            Color c = p.AimingAtTarget ? (p.SwingReady ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.85f, 0.3f)) : new Color(1f, 1f, 1f, 0.8f);
            float cx = hud.W / 2f, cy = hud.H / 2f;
            Gui.Fill(new Rect(cx - 8f, cy - 1f, 16f, 2f), c);
            Gui.Fill(new Rect(cx - 1f, cy - 8f, 2f, 16f), c);
            if (Cursor.lockState != CursorLockMode.Locked)
                Gui.Shadowed(new Rect(cx - 150f, cy + 20f, 300f, 22f), "Click to look around", Gui.Centered);
        }

        void DrawXpDrops(Hud hud)
        {
            drops.RemoveAll(d => Time.time - d.Born > 2.2f);
            float x = hud.PanelRect.x - 170f, baseY = 90f;
            for (int i = 0; i < drops.Count; i++)
            {
                var d = drops[i];
                float age = Time.time - d.Born;
                var colour = ModelBuilder.ToColor(ContentDb.Current.Skill(d.Skill).Colour);
                float alpha = Mathf.Clamp01((2.2f - age) / 0.6f);
                colour.a = alpha;
                var r = new Rect(x, baseY - age * 30f + (drops.Count - 1 - i) * 18f, 160f, 20f);
                Gui.Fill(new Rect(r.x, r.y + 4f, 12f, 12f), colour);
                Gui.Tinted(new Rect(r.x + 16f, r.y, r.width, r.height), d.Text, Gui.Label, new Color(1f, 1f, 1f, alpha));
            }
        }

        void DrawLevelUp(Hud hud)
        {
            if (Time.time > levelUpUntil || levelUpText == null)
                return;
            var r = new Rect(hud.W / 2f - 220f, 70f, 440f, 44f);
            Gui.Fill(r, new Color(0.1f, 0.08f, 0.04f, 0.8f));
            Gui.Shadowed(r, "<color=#ffd966><b>" + levelUpText + "</b></color>", Gui.Centered);
        }

        void DrawChat(Hud hud)
        {
            if (!hud.ChatOpen)
                return;
            var r = hud.ChatRect;
            Gui.Fill(r, new Color(0.12f, 0.1f, 0.07f, 0.7f));
            var msgs = GameLog.Messages;
            const int lines = 7;
            float y = r.y + 4f;
            for (int i = Mathf.Max(0, msgs.Count - lines); i < msgs.Count; i++)
            {
                Gui.Shadowed(new Rect(r.x + 6f, y, r.width - 12f, 20f), msgs[i], Gui.Small);
                y += 20f;
            }
        }

        /// Health bars and hitsplats over monsters you've hit, and over you while hurt (tile mode).
        void DrawCombat(Hud hud)
        {
            var cam = G.Rig.Cam;
            var p = G.Player;
            if (G.Prayer.ProtectsFromMelee)
            {
                // The protection prayer's symbol over your head (top of the view in first person).
                Vector3 over = cam.WorldToScreenPoint(p.transform.position + Vector3.up * 2.5f);
                Vector2 at = p.Mode == ViewMode.Tile && over.z > 0f ? ToGui(hud, over) : new Vector2(hud.W / 2f, 40f);
                PrayerTab.DrawIcon(new Rect(at.x - 11f, at.y - 11f, 22f, 22f), Prayers.Get(PrayerId.ProtectFromMelee));
            }
            if (p.Mode == ViewMode.Tile && (!G.Health.IsFull || p.Hitsplats.Count > 0))
            {
                Vector3 head = cam.WorldToScreenPoint(p.transform.position + Vector3.up * 2.15f);
                if (head.z > 0f)
                {
                    var at = ToGui(hud, head);
                    HealthBar(at, G.Health.Fraction);
                    for (int i = 0; i < p.Hitsplats.Count; i++)
                        Hitsplat(new Rect(at.x - 12f + i * 8f, at.y + 14f - i * 6f, 24f, 22f), p.Hitsplats[i].Damage);
                }
            }
            foreach (var npc in G.World.Npcs)
            {
                if (Time.time - npc.LastHitTime > 6f || (npc.IsDead && npc.Hitsplats.Count == 0))
                    continue;
                Vector3 head = cam.WorldToScreenPoint(npc.transform.position + Vector3.up * (npc.Def.Height + 0.25f));
                if (head.z <= 0f)
                    continue;
                var at = ToGui(hud, head);
                if (npc.IsStunned)
                    Gui.Shadowed(new Rect(at.x - 40f, at.y - 22f, 80f, 16f), "<color=#ffff66>Stunned</color>", Gui.Centered);
                HealthBar(at, (float)npc.Hitpoints / npc.Def.Hitpoints);
                var body = ToGui(hud, cam.WorldToScreenPoint(npc.transform.position + Vector3.up * 0.8f));
                for (int i = 0; i < npc.Hitsplats.Count; i++)
                    Hitsplat(new Rect(body.x - 12f + i * 8f, body.y - 12f - i * 6f, 24f, 22f), npc.Hitsplats[i].Damage);
            }
        }

        /// Green for what's left, red for what's lost.
        static void HealthBar(Vector2 at, float fraction)
        {
            var bar = new Rect(at.x - 20f, at.y - 6f, 40f, 5f);
            Gui.Fill(bar, new Color(0.8f, 0.1f, 0.1f));
            Gui.Fill(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), new Color(0.1f, 0.85f, 0.15f));
        }

        /// Red with the damage, or blue for a miss.
        static void Hitsplat(Rect r, int damage)
        {
            Gui.Fill(r, damage > 0 ? new Color(0.75f, 0.08f, 0.08f, 0.95f) : new Color(0.15f, 0.35f, 0.8f, 0.95f));
            Gui.Shadowed(r, "<b>" + damage + "</b>", Gui.Centered);
        }
    }
}
