using System.Collections.Generic;
using System.Numerics;
using Epacenur.Core;
using UnityEngine;
using Matrix4x4 = UnityEngine.Matrix4x4;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Epacenur.Runtime
{
    /// Everything on screen: inventory and skills panel, chat box, xp drops, level-ups,
    /// the action under the mouse, the first-person crosshair and the dev tools (F1).
    public sealed class Hud : MonoBehaviour
    {
        const float Slot = 40f, Gap = 4f, Pad = 10f, TabH = 26f;
        const float PanelW = Pad * 2 + Slot * 4 + Gap * 3;
        const float PanelH = Pad * 2 + TabH + 6f + Slot * 7 + Gap * 6;

        enum Tab { Inventory, Skills }

        struct XpDrop
        {
            public SkillId Skill;
            public string Text;
            public float Born;
        }

        Tab tab = Tab.Inventory;
        bool showDev, showHelp = true;
        int selectedSlot = -1;
        Rect devRect = new Rect(0f, 0f, 300f, 10f);
        bool devPlaced;
        readonly List<XpDrop> drops = new List<XpDrop>();
        string levelUpText;
        float levelUpUntil;
        float scale = 1f;
        GUIStyle label, small, title, centered, rich, rightLabel, rightSmall;

        Game G => Game.Instance;
        float W => Screen.width / scale;
        float H => Screen.height / scale;
        Rect PanelRect => new Rect(W - PanelW - 10f, H - PanelH - 10f, PanelW, PanelH);
        Rect ChatRect => new Rect(10f, H - 160f, Mathf.Clamp(W - PanelW - 40f, 200f, 520f), 150f);

        void Update()
        {
            scale = Mathf.Clamp(Screen.height / 760f, 0.75f, 3f);
            if (Input.GetKeyDown(KeyCode.F1))
                showDev = !showDev;
            if (Input.GetKeyDown(KeyCode.H))
                showHelp = !showHelp;
        }

        /// Whether the mouse is over a panel (so clicks there don't walk or chop).
        public bool IsMouseOverUi()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
                return false;
            var m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / scale;
            return PanelRect.Contains(m) || ChatRect.Contains(m) || (showDev && devRect.Contains(m));
        }

        public void AddXpDrop(SkillId skill, BigInteger tenths)
        {
            string xp = tenths % 10 == 0 ? Experience.Format(tenths / 10) : Experience.Format(tenths / 10) + "." + (tenths % 10);
            drops.Add(new XpDrop { Skill = skill, Text = "+" + xp + " xp", Born = Time.time });
        }

        public void ShowLevelUp(SkillId skill, int level)
        {
            levelUpText = "Congratulations! " + SkillDefs.Get(skill).Name + " level " + level;
            levelUpUntil = Time.time + 4f;
        }

        void MakeStyles()
        {
            if (label != null)
                return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 11 };
            title = new GUIStyle(label) { fontSize = 15, fontStyle = FontStyle.Bold };
            centered = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            rich = new GUIStyle(label) { wordWrap = true };
            rightLabel = new GUIStyle(label) { alignment = TextAnchor.UpperRight };
            rightSmall = new GUIStyle(small) { alignment = TextAnchor.UpperRight, wordWrap = true };
        }

        void OnGUI()
        {
            MakeStyles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            DrawWorldLabels();
            DrawTopLeft();
            DrawXpDrops();
            DrawLevelUp();
            DrawChat();
            DrawPanel();
            if (G.Player.Mode == ViewMode.FirstPerson)
                DrawCrosshair();
            if (showDev)
            {
                if (!devPlaced)
                {
                    devRect = new Rect(W - 310f, 10f, 300f, 10f);
                    devPlaced = true;
                }
                devRect = GUILayout.Window(1, devRect, DrawDevWindow, "Dev tools (F1)");
            }
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void Shadowed(Rect r, string text, GUIStyle style)
        {
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), StripColour(text), style);
            style.normal.textColor = old;
            GUI.Label(r, text, style);
        }

        static string StripColour(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<color=[^>]*>|</color>", "");

        void DrawWorldLabels()
        {
            var cam = G.Rig.Cam;
            foreach (var l in G.World.Labels)
            {
                Vector3 sp = cam.WorldToScreenPoint(l.Position);
                if (sp.z <= 0f || sp.z > 60f)
                    continue;
                var c = l.Color;
                c.a = Mathf.Clamp01((60f - sp.z) / 15f);
                var r = new Rect(sp.x / scale - 120f, (Screen.height - sp.y) / scale - 12f, 240f, 24f);
                var old = centered.normal.textColor;
                centered.normal.textColor = c;
                GUI.Label(r, "<b>" + l.Text + "</b>", centered);
                centered.normal.textColor = old;
            }
        }

        void DrawTopLeft()
        {
            var p = G.Player;
            string action = p.HoverAction;
            if (action.Length > 0 && p.HoverTarget.Length > 0)
                action += " <color=#ffff00>" + p.HoverTarget + "</color>";
            if (p.IsChopping)
                action = "Woodcutting...";
            Shadowed(new Rect(10f, 8f, 600f, 22f), action, title);

            string mode = p.Mode == ViewMode.Tile ? "Tile mode" : "First person";
            Shadowed(new Rect(10f, 30f, 600f, 20f), "<color=#ffd966>" + mode + "</color>   (V to switch, H for help)", small);
            if (!showHelp)
                return;
            string help = p.Mode == ViewMode.Tile
                ? "Left-click: walk / chop a tree\nWASD: walk freely\nArrow keys or middle-drag: rotate camera\nScroll: zoom\nRight- or shift-click an item: drop it\nF1: dev tools"
                : "Mouse: look    WASD: walk\nLeft-click a tree: one chop per click\nEsc: free the mouse (click the view to grab it again)\nRight- or shift-click an item: drop it\nF1: dev tools";
            Shadowed(new Rect(10f, 50f, 420f, 110f), help, small);
        }

        void DrawCrosshair()
        {
            var p = G.Player;
            Color c = p.AimingAtTree ? (p.SwingReady ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.85f, 0.3f)) : new Color(1f, 1f, 1f, 0.8f);
            float cx = W / 2f, cy = H / 2f;
            Fill(new Rect(cx - 8f, cy - 1f, 16f, 2f), c);
            Fill(new Rect(cx - 1f, cy - 8f, 2f, 16f), c);
            if (Cursor.lockState != CursorLockMode.Locked)
                Shadowed(new Rect(cx - 150f, cy + 20f, 300f, 22f), "Click to look around", centered);
        }

        void DrawXpDrops()
        {
            drops.RemoveAll(d => Time.time - d.Born > 2.2f);
            float x = PanelRect.x - 170f, baseY = 90f;
            for (int i = 0; i < drops.Count; i++)
            {
                var d = drops[i];
                float age = Time.time - d.Born;
                var def = SkillDefs.Get(d.Skill);
                float alpha = Mathf.Clamp01((2.2f - age) / 0.6f);
                var r = new Rect(x, baseY - age * 30f + (drops.Count - 1 - i) * 18f, 160f, 20f);
                Fill(new Rect(r.x, r.y + 4f, 12f, 12f), new Color(def.R, def.G, def.B, alpha));
                var old = label.normal.textColor;
                label.normal.textColor = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(r.x + 16f, r.y, r.width, r.height), d.Text, label);
                label.normal.textColor = old;
            }
        }

        void DrawLevelUp()
        {
            if (Time.time > levelUpUntil || levelUpText == null)
                return;
            var r = new Rect(W / 2f - 220f, 70f, 440f, 44f);
            Fill(r, new Color(0.1f, 0.08f, 0.04f, 0.8f));
            Shadowed(r, "<color=#ffd966><b>" + levelUpText + "</b></color>", centered);
        }

        void DrawChat()
        {
            var r = ChatRect;
            Fill(r, new Color(0.12f, 0.1f, 0.07f, 0.7f));
            var msgs = GameLog.Messages;
            int lines = 7;
            float y = r.y + 4f;
            for (int i = Mathf.Max(0, msgs.Count - lines); i < msgs.Count; i++)
            {
                Shadowed(new Rect(r.x + 6f, y, r.width - 12f, 20f), msgs[i], small);
                y += 20f;
            }
        }

        // ---- Inventory / skills panel ----

        void DrawPanel()
        {
            var r = PanelRect;
            Fill(r, new Color(0.24f, 0.2f, 0.15f, 0.92f));
            float tabW = (r.width - Pad * 2 - 4f) / 2f;
            if (GUI.Toggle(new Rect(r.x + Pad, r.y + Pad, tabW, TabH), tab == Tab.Inventory, "Inventory", GUI.skin.button))
                tab = Tab.Inventory;
            if (GUI.Toggle(new Rect(r.x + Pad + tabW + 4f, r.y + Pad, tabW, TabH), tab == Tab.Skills, "Skills", GUI.skin.button))
                tab = Tab.Skills;
            var body = new Rect(r.x + Pad, r.y + Pad + TabH + 6f, r.width - Pad * 2, r.height - Pad * 2 - TabH - 6f);
            if (tab == Tab.Inventory)
                DrawInventory(body);
            else
                DrawSkills(body);
        }

        void DrawInventory(Rect body)
        {
            var inv = G.Inventory;
            var e = Event.current;
            string tooltip = null;
            for (int i = 0; i < Inventory.Size; i++)
            {
                var slot = new Rect(body.x + (i % 4) * (Slot + Gap), body.y + (i / 4) * (Slot + Gap), Slot, Slot);
                Fill(slot, i == selectedSlot ? new Color(0.6f, 0.5f, 0.2f, 0.9f) : new Color(0f, 0f, 0f, 0.25f));
                var stack = inv[i];
                if (!stack.IsEmpty)
                {
                    DrawItemIcon(slot, stack.Def);
                    if (stack.Def.Stackable && stack.Count > 1)
                        Shadowed(new Rect(slot.x + 2f, slot.y, slot.width, 16f), "<color=#ffff00>" + stack.Count + "</color>", small);
                    if (slot.Contains(e.mousePosition))
                        tooltip = stack.Def.Name;
                }

                if (e.type == EventType.MouseDown && slot.Contains(e.mousePosition))
                {
                    bool drop = e.button == 1 || e.shift;
                    if (drop && !stack.IsEmpty)
                    {
                        inv.RemoveAt(i);
                        GameLog.Add("You drop the " + stack.Def.Name.ToLowerInvariant() + ".");
                        selectedSlot = -1;
                    }
                    else if (!drop)
                    {
                        // Click one slot, then another, to swap them.
                        if (selectedSlot == -1 && !stack.IsEmpty)
                            selectedSlot = i;
                        else if (selectedSlot >= 0)
                        {
                            if (selectedSlot != i)
                                inv.Swap(selectedSlot, i);
                            selectedSlot = -1;
                        }
                    }
                    e.Use();
                }
            }
            if (tooltip != null)
                Shadowed(new Rect(e.mousePosition.x + 14f, e.mousePosition.y - 6f, 200f, 20f), "<color=#ffcc66>" + tooltip + "</color>", small);
        }

        /// No art yet: items are drawn from coloured rectangles.
        static void DrawItemIcon(Rect slot, ItemDef def)
        {
            var c = new Color(def.R, def.G, def.B);
            if (def == ItemDefs.Logs)
            {
                var end = new Color(0.85f, 0.7f, 0.45f);
                for (int k = 0; k < 3; k++)
                {
                    float y = slot.y + 9f + k * 8f, x = slot.x + 6f + (k == 1 ? 4f : 0f);
                    Fill(new Rect(x, y, 24f, 7f), k == 1 ? c * 0.85f : c);
                    Fill(new Rect(x + 24f, y, 4f, 7f), end);
                }
                return;
            }
            Fill(new Rect(slot.x + 8f, slot.y + 8f, slot.width - 16f, slot.height - 16f), c);
        }

        void DrawSkills(Rect body)
        {
            float y = body.y;
            foreach (var def in SkillDefs.All)
            {
                BigInteger xp = G.Skills.GetXp(def.Id);
                int level = Experience.LevelForXp(xp);
                Fill(new Rect(body.x, y, body.width, 92f), new Color(0f, 0f, 0f, 0.25f));
                Fill(new Rect(body.x + 4f, y + 6f, 14f, 14f), new Color(def.R, def.G, def.B));
                GUI.Label(new Rect(body.x + 22f, y + 2f, body.width, 20f), "<b>" + def.Name + "</b>", label);
                GUI.Label(new Rect(body.x + 4f, y + 2f, body.width - 8f, 20f), "<color=#ffff00><b>" + level + "</b></color> / " + Experience.MaxLevel, rightLabel);
                GUI.Label(new Rect(body.x + 4f, y + 22f, body.width - 8f, 18f), "XP: " + Experience.FormatShort(xp), small);
                if (level < Experience.MaxLevel)
                {
                    GUI.Label(new Rect(body.x + 4f, y + 38f, body.width - 8f, 18f), "Next level at: " + Experience.FormatShort(Experience.XpForLevel(level + 1)), small);
                    GUI.Label(new Rect(body.x + 4f, y + 54f, body.width - 8f, 18f), "Remaining: " + Experience.FormatShort(Experience.XpToNextLevel(xp)), small);
                }
                else
                {
                    GUI.Label(new Rect(body.x + 4f, y + 38f, body.width - 8f, 18f), "Maxed!", small);
                }
                var bar = new Rect(body.x + 4f, y + 74f, body.width - 8f, 10f);
                Fill(bar, new Color(0.5f, 0.1f, 0.1f));
                Fill(new Rect(bar.x, bar.y, bar.width * (float)Experience.LevelProgress(xp), bar.height), new Color(0.2f, 0.75f, 0.2f));
                if (new Rect(body.x, y, body.width, 92f).Contains(Event.current.mousePosition))
                    Shadowed(new Rect(body.x - 230f, y, 225f, 40f), "<color=#ffcc66>" + def.Name + " XP: " + Experience.Format(xp) + "</color>", rightSmall);
                y += 96f;
            }
            GUI.Label(new Rect(body.x, body.yMax - 22f, body.width, 20f), "Total level: <b>" + G.Skills.TotalLevel + "</b>", label);
        }

        // ---- Dev tools ----

        void DrawDevWindow(int id)
        {
            foreach (var def in SkillDefs.All)
            {
                int level = G.Skills.GetLevel(def.Id);
                GUILayout.Label("<b>" + def.Name + "</b>  level " + level + "  (" + Experience.FormatShort(G.Skills.GetXp(def.Id)) + " xp)", rich);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("-1")) G.Skills.SetLevel(def.Id, Mathf.Max(1, level - 1));
                if (GUILayout.Button("+1")) G.Skills.SetLevel(def.Id, Mathf.Min(Experience.MaxLevel, level + 1));
                foreach (int l in new[] { 1, 99, 120, 999 })
                    if (GUILayout.Button(l.ToString())) G.Skills.SetLevel(def.Id, l);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("+1K xp")) G.Skills.AddXp(def.Id, 1e3);
                if (GUILayout.Button("+1M xp")) G.Skills.AddXp(def.Id, 1e6);
                if (GUILayout.Button("+1B xp")) G.Skills.AddXp(def.Id, 1e9);
                GUILayout.EndHorizontal();
                GUILayout.Space(6f);
            }

            GUILayout.Label("<b>Inventory</b>", rich);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Empty it")) { G.Inventory.Clear(); selectedSlot = -1; }
            if (GUILayout.Button("Fill with logs")) G.Inventory.Add(ItemDefs.Logs.Id, Inventory.Size);
            GUILayout.EndHorizontal();

            GUILayout.Label("<b>World</b>", rich);
            if (GUILayout.Button("Regrow every tree"))
                foreach (var t in G.World.Trees)
                    if (t.IsStump)
                        t.Regrow();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Go to spawn"))
                G.Player.Teleport(G.World.Spawn);
            foreach (var area in G.World.Areas)
                if (!(area is EmptyPlot) && GUILayout.Button("Go to " + area.Name))
                    G.Player.Teleport(G.World.NearestWalkable(area.TeleportTile));
            GUILayout.EndHorizontal();

            GUILayout.Label("<b>Save</b>", rich);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save now")) { SaveGame.Save(G); GameLog.Add("Saved."); }
            if (GUILayout.Button("Reset everything"))
            {
                SaveGame.Delete();
                foreach (var def in SkillDefs.All)
                    G.Skills.SetLevel(def.Id, 1);
                G.Inventory.Clear();
                G.Player.Teleport(G.World.Spawn);
                GameLog.Add("Progress reset.");
            }
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }
    }
}
