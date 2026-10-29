using System;
using System.Collections.Generic;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// One line of the right-click menu: "Attack" + "Goblin (level-2)".
    public sealed class MenuOption
    {
        public readonly string Action, Target;
        public readonly Action Run;

        public MenuOption(string action, string target, Action run)
        {
            Action = action;
            Target = target ?? "";
            Run = run;
        }

        /// The option as the menu and the top-left hover text show it, with the target coloured by kind.
        public string Text => Target.Length == 0 ? Action : Action + " " + Target;

        // RuneScape's colours for what an option is about.
        public static string Npc(string name) => "<color=#ffff00>" + name + "</color>";
        public static string Item(string name) => "<color=#ff9040>" + name + "</color>";
        public static string Thing(string name) => "<color=#00ffff>" + name + "</color>";
    }

    /// The right-click "Choose Option" menu. It closes when you pick an option, click outside it,
    /// or move the mouse away from it, as in RuneScape.
    public sealed class OptionMenu
    {
        const float RowH = 17f, HeaderH = 18f, CloseMargin = 12f;

        List<MenuOption> options;
        Rect rect;
        int openedFrame;
        float openedTime;
        bool relockCursor;
        GUIStyle style, header;

        public bool IsOpen => options != null;
        public Rect Rect => rect;

        /// Opens at a GUI position (top-left origin, HUD scale). "Cancel" is added at the end.
        /// relockCursor: first person; grab the mouse again once an option is chosen.
        public void Open(List<MenuOption> opts, Vector2 at, Vector2 screenSize, bool relockCursor)
        {
            if (opts == null || opts.Count == 0)
                return;
            options = new List<MenuOption>(opts) { new MenuOption("Cancel", "", null) };
            this.relockCursor = relockCursor;
            openedFrame = Time.frameCount;
            openedTime = Time.time;
            MakeStyles();
            float w = style.CalcSize(new GUIContent("Choose Option")).x;
            foreach (var o in options)
                w = Mathf.Max(w, style.CalcSize(new GUIContent(o.Text)).x);
            w += 10f;
            float h = HeaderH + options.Count * RowH + 3f;
            // The cursor lands on the header, centred, like RuneScape; kept on screen.
            float x = Mathf.Clamp(at.x - w / 2f, 2f, screenSize.x - w - 2f);
            float y = Mathf.Clamp(at.y - 6f, 2f, screenSize.y - h - 2f);
            rect = new Rect(x, y, w, h);
        }

        public void Close()
        {
            options = null;
            if (relockCursor && Game.Instance.Player.Mode == ViewMode.FirstPerson)
                Game.Instance.Rig.LockCursor(true);
            relockCursor = false;
        }

        /// Call first thing in OnGUI: a click either picks an option or closes the menu, and is used
        /// up so nothing under the menu reacts to it.
        public void HandleInput()
        {
            if (!IsOpen)
                return;
            var e = Event.current;
            if (e.type == EventType.MouseMove || e.type == EventType.Repaint)
            {
                var m = e.mousePosition;
                var far = new Rect(rect.x - CloseMargin, rect.y - CloseMargin, rect.width + CloseMargin * 2, rect.height + CloseMargin * 2);
                // (A moment's grace: in first person the freed cursor takes a frame to show up.)
                if (!far.Contains(m) && Time.time - openedTime > 0.15f)
                    Close();
                return;
            }
            if (e.type != EventType.MouseDown || Time.frameCount == openedFrame)
                return;
            int row = Row(e.mousePosition);
            e.Use();
            if (row == -2)
                return;  // clicked the header
            var chosen = row >= 0 ? options[row] : null;
            Close();
            chosen?.Run?.Invoke();
        }

        /// Call last in OnGUI so it draws on top.
        public void Draw()
        {
            if (!IsOpen || Event.current.type != EventType.Repaint)
                return;
            MakeStyles();
            Gui.Fill(rect, new Color(0.36f, 0.33f, 0.27f, 1f));
            Gui.Fill(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, HeaderH - 1f), Color.black);
            GUI.Label(new Rect(rect.x + 4f, rect.y + 1f, rect.width, HeaderH), "Choose Option", header);
            int hover = Row(Event.current.mousePosition);
            for (int i = 0; i < options.Count; i++)
            {
                var r = new Rect(rect.x + 4f, rect.y + HeaderH + i * RowH, rect.width - 6f, RowH);
                var old = style.normal.textColor;
                style.normal.textColor = i == hover ? new Color(1f, 1f, 0f) : Color.white;
                GUI.Label(r, options[i].Text, style);
                style.normal.textColor = old;
            }
        }

        // -1 outside, -2 the header, otherwise the option index.
        int Row(Vector2 m)
        {
            if (!rect.Contains(m))
                return -1;
            if (m.y < rect.y + HeaderH)
                return -2;
            int i = (int)((m.y - rect.y - HeaderH) / RowH);
            return i >= 0 && i < options.Count ? i : -1;
        }

        void MakeStyles()
        {
            if (style != null)
                return;
            style = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, padding = new RectOffset(0, 0, 1, 0) };
            style.normal.textColor = Color.white;
            header = new GUIStyle(style);
            header.normal.textColor = new Color(0.36f, 0.33f, 0.27f);
        }
    }
}
