using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Drawing helpers, text styles and the look shared by every part of the HUD and the main menu.
    /// Everything is drawn from flat rectangles (there's no art), in one palette: dark, warm panels with a
    /// thin gold trim, sunken slots, and buttons from a themed copy of Unity's skin (Begin puts it on).
    public static class Gui
    {
        public const float Slot = 40f, Gap = 4f, Pad = 10f, TitleH = 30f;

        public static GUIStyle Label, Small, Title, Centered, Rich, RightLabel, RightSmall, TabStyle, CloseStyle;
        public static GUISkin Skin;

        // ---- The palette ----

        public static readonly Color Panel = new Color(0.13f, 0.115f, 0.095f, 1f);      // panel backgrounds
        public static readonly Color HeaderBg = new Color(0.085f, 0.074f, 0.06f, 1f);  // title bars
        public static readonly Color Edge = new Color(0.03f, 0.025f, 0.02f, 1f);       // outer outlines
        public static readonly Color Trim = new Color(0.66f, 0.53f, 0.31f, 1f);        // gold trim
        public static readonly Color TrimDim = new Color(0.33f, 0.27f, 0.19f, 1f);     // quieter lines and dividers
        public static readonly Color Well = new Color(0.07f, 0.062f, 0.05f, 1f);       // slots, the backs of bars
        public static readonly Color Hover = new Color(1f, 0.95f, 0.8f, 0.08f);        // over a slot or row
        public static readonly Color Picked = new Color(0.95f, 0.76f, 0.32f, 0.24f);   // a slot or row that's on
        public static readonly Color Locked = new Color(0.06f, 0.05f, 0.04f, 0.72f);   // not unlocked yet
        public static readonly Color Text = new Color(0.92f, 0.89f, 0.81f, 1f);
        /// The same colours for rich text.
        public const string GoldHex = "#f0d48a", TextHex = "#ebe4d2", MutedHex = "#a39a86", GoodHex = "#9be38f", BadHex = "#ff6b5e", BlueHex = "#8fc8ff";

        public static readonly Color BevelLight = TrimDim;

        /// Call first in every OnGUI: makes the styles (GUI.skin only exists there) and puts the themed skin on.
        public static void Begin()
        {
            MakeStyles();
            GUI.skin = Skin;
        }

        static void MakeStyles()
        {
            if (Label != null)
                return;
            MakeSkin();
            Label = new GUIStyle(Skin.label) { fontSize = 13, richText = true };
            Label.normal.textColor = Text;
            Small = new GUIStyle(Label) { fontSize = 11 };
            Title = new GUIStyle(Label) { fontSize = 15, fontStyle = FontStyle.Bold };
            Centered = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter };
            Rich = new GUIStyle(Label) { wordWrap = true };
            RightLabel = new GUIStyle(Label) { alignment = TextAnchor.UpperRight };
            RightSmall = new GUIStyle(Small) { alignment = TextAnchor.UpperRight, wordWrap = true };
            TabStyle = new GUIStyle(Skin.button) { fontSize = 10, padding = new RectOffset(1, 1, 2, 2) };
            CloseStyle = new GUIStyle(Skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 0, 1) };
        }

        /// A copy of Unity's skin with buttons, toggles, text fields, scroll bars and windows in the palette.
        static void MakeSkin()
        {
            Skin = Object.Instantiate(GUI.skin);
            var light = new Color(0.94f, 0.9f, 0.8f, 1f);
            var gold = new Color(1f, 0.9f, 0.62f, 1f);

            var b = Skin.button;
            b.fontSize = 12;
            b.richText = true;
            b.alignment = TextAnchor.MiddleCenter;
            b.border = new RectOffset(3, 3, 3, 3);
            b.padding = new RectOffset(6, 6, 3, 3);
            State(b.normal, Tex(Edge, new Color(0.3f, 0.25f, 0.18f), new Color(0.21f, 0.175f, 0.13f), new Color(0.4f, 0.33f, 0.23f)), Text);
            State(b.hover, Tex(Edge, new Color(0.37f, 0.31f, 0.22f), new Color(0.26f, 0.215f, 0.155f), new Color(0.5f, 0.41f, 0.28f)), Color.white);
            State(b.active, Tex(Edge, new Color(0.16f, 0.135f, 0.1f), new Color(0.22f, 0.185f, 0.135f), new Color(0.12f, 0.1f, 0.075f)), light);
            State(b.focused, b.normal.background, Text);
            var on = Tex(Trim * 0.55f, new Color(0.5f, 0.39f, 0.2f), new Color(0.36f, 0.27f, 0.13f), new Color(0.75f, 0.6f, 0.33f));
            State(b.onNormal, on, gold);
            State(b.onHover, on, Color.white);
            State(b.onActive, on, gold);
            State(b.onFocused, on, gold);

            // Toggles drawn in the button style look the same; plain toggles too.
            Skin.toggle = new GUIStyle(b);

            var f = Skin.textField;
            f.fontSize = 12;
            f.border = new RectOffset(3, 3, 3, 3);
            f.padding = new RectOffset(5, 5, 3, 3);
            State(f.normal, Tex(Edge, Well, Well, Well), Text);
            State(f.hover, Tex(TrimDim, Well, Well, Well), Text);
            State(f.focused, Tex(Trim, Well, Well, Well), Color.white);
            State(f.onNormal, f.normal.background, Text);

            var thumb = Tex(Edge, new Color(0.42f, 0.35f, 0.24f), new Color(0.32f, 0.26f, 0.18f), new Color(0.52f, 0.43f, 0.29f));
            Skin.verticalScrollbar.border = new RectOffset(2, 2, 2, 2);
            Skin.verticalScrollbar.fixedWidth = 10f;
            State(Skin.verticalScrollbar.normal, Tex(Edge, Well, Well, Well), Text);
            Skin.verticalScrollbarThumb.border = new RectOffset(3, 3, 3, 3);
            Skin.verticalScrollbarThumb.fixedWidth = 10f;
            State(Skin.verticalScrollbarThumb.normal, thumb, Text);
            Skin.horizontalScrollbar.border = new RectOffset(2, 2, 2, 2);
            Skin.horizontalScrollbar.fixedHeight = 10f;
            State(Skin.horizontalScrollbar.normal, Tex(Edge, Well, Well, Well), Text);
            Skin.horizontalScrollbarThumb.border = new RectOffset(3, 3, 3, 3);
            Skin.horizontalScrollbarThumb.fixedHeight = 10f;
            State(Skin.horizontalScrollbarThumb.normal, thumb, Text);

            var w = Skin.window;
            w.border = new RectOffset(3, 3, 3, 3);
            w.fontStyle = FontStyle.Bold;
            State(w.normal, Tex(Edge, Panel, Panel, TrimDim), gold);
            State(w.onNormal, w.normal.background, gold);

            Skin.label.normal.textColor = Text;
            Skin.box.border = new RectOffset(3, 3, 3, 3);
            State(Skin.box.normal, Tex(Edge, Well, Well, Well), Text);
        }

        static void State(GUIStyleState s, Texture2D background, Color text)
        {
            s.background = background;
            s.textColor = text;
        }

        /// A small texture for a 9-sliced style: a 1-pixel outline, a highlight line just inside its top,
        /// and a fill shading from `top` to `bottom`.
        static Texture2D Tex(Color outline, Color top, Color bottom, Color highlight)
        {
            const int w = 8, h = 16;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int row = h - 1 - y;  // rows count from the bottom in a texture
                    Color c;
                    if (x == 0 || y == 0 || x == w - 1 || row == 0)
                        c = outline;
                    else if (row == 1)
                        c = highlight;
                    else
                    {
                        float t = (row - 2f) / (h - 4f);
                        c = new Color(top.r + (bottom.r - top.r) * t, top.g + (bottom.g - top.g) * t, top.b + (bottom.b - top.b) * t, 1f);
                    }
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Label = null;
            Skin = null;
        }

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// A 1-pixel line round the inside of a rectangle.
        public static void Outline(Rect r, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, 1f), c);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            Fill(new Rect(r.x, r.y + 1f, 1f, r.height - 2f), c);
            Fill(new Rect(r.xMax - 1f, r.y + 1f, 1f, r.height - 2f), c);
        }

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        // ---- Panels ----

        /// A panel: a dark outline, the background, and a thin gold trim just inside. `alpha` < 1 lets the
        /// world show through (the chat box).
        public static void PanelBox(Rect r, float alpha = 0.96f)
        {
            Fill(r, WithAlpha(Edge, Mathf.Min(1f, alpha + 0.1f)));
            Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), WithAlpha(Panel, alpha));
            Outline(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), WithAlpha(TrimDim, alpha));
            Fill(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, 1f), new Color(1f, 0.95f, 0.8f, 0.05f * alpha));
        }

        /// A panel with a title bar (Gui.TitleH tall), the title in gold. Returns whether its close
        /// button (if `closable`) was clicked.
        public static bool TitledPanel(Rect r, string title, bool closable = false)
        {
            PanelBox(r);
            var bar = new Rect(r.x + 3f, r.y + 3f, r.width - 6f, TitleH - 4f);
            Fill(bar, HeaderBg);
            Fill(new Rect(bar.x, bar.yMax, bar.width, 1f), Trim * 0.8f);
            Shadowed(new Rect(r.x + Pad, r.y + 5f, r.width - Pad * 2 - 30f, 22f), "<b><color=" + GoldHex + ">" + title + "</color></b>", Title);
            return closable && GUI.Button(new Rect(r.xMax - 28f, r.y + 6f, 21f, 20f), "\u00d7", CloseStyle);
        }

        /// A small heading over a tab's contents, with an optional note on the right and a divider under it.
        public static void Heading(Rect body, string title, string right = null)
        {
            Shadowed(new Rect(body.x, body.y - 2f, body.width, 18f), "<b><color=" + GoldHex + ">" + title + "</color></b>", Small);
            if (right != null)
                Shadowed(new Rect(body.x, body.y - 2f, body.width, 18f), right, RightSmall);
            Fill(new Rect(body.x, body.y + 14f, body.width, 1f), TrimDim);
        }

        /// A tooltip's box: dark, with a quiet outline and a gold line along the top.
        public static void TipBox(Rect r)
        {
            Fill(r, new Color(0.06f, 0.052f, 0.042f, 0.97f));
            Outline(r, TrimDim);
            Fill(new Rect(r.x, r.y, r.width, 1f), Trim);
        }

        /// The old name for a tooltip's box.
        public static void Box(Rect r) => TipBox(r);

        // ---- Slots and bars ----

        /// A sunken slot: dark inside, shadowed along the top and left, lit along the bottom and right.
        public static void Bevel(Rect r)
        {
            Fill(r, TrimDim);
            Fill(new Rect(r.x, r.y, r.width - 1f, r.height - 1f), Edge);
            Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), Well);
        }

        /// A slot that's just a place to put something (inventory, bank, shop): fainter than a Bevel.
        public static void EmptySlot(Rect r)
        {
            Fill(r, new Color(0f, 0f, 0f, 0.28f));
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), new Color(1f, 0.95f, 0.8f, 0.05f));
        }

        /// Lights a slot or row under the mouse, or one that's picked or on.
        public static void Highlight(Rect r, bool picked)
        {
            var inner = new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f);
            Fill(inner, picked ? Picked : Hover);
            if (picked)
                Outline(r, Trim);
        }

        /// A bar filled `fraction` of the way, with a sheen along its top and text in the middle.
        public static void Bar(Rect r, float fraction, Color fill, Color back, string text = null)
        {
            Fill(r, Edge);
            var inner = new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f);
            Fill(inner, back);
            var full = new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(fraction), inner.height);
            Fill(full, fill);
            Fill(new Rect(full.x, full.y, full.width, Mathf.Max(1f, inner.height * 0.35f)), new Color(1f, 1f, 1f, 0.16f));
            if (text != null)
                Shadowed(r, "<b>" + text + "</b>", Centered);
        }

        /// Text with a dark drop shadow, readable over the world.
        public static void Shadowed(Rect r, string text, GUIStyle style)
        {
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), StripColour(text), style);
            style.normal.textColor = old;
            GUI.Label(r, text, style);
        }

        /// Text in a colour (with alpha), e.g. fading xp drops.
        public static void Tinted(Rect r, string text, GUIStyle style, Color c)
        {
            var old = style.normal.textColor;
            style.normal.textColor = c;
            GUI.Label(r, text, style);
            style.normal.textColor = old;
        }

        /// A one-line tooltip: the text in a small box fitted round it.
        public static void Tooltip(Rect r, string text, GUIStyle style)
        {
            float w = Mathf.Min(r.width, style.CalcSize(new GUIContent(StripColour(text))).x + 2f);
            float x = style.alignment == TextAnchor.UpperRight ? r.xMax - w : r.x;
            TipBox(new Rect(x - 5f, r.y - 1f, w + 10f, r.height + 2f));
            Shadowed(new Rect(x, r.y + 1f, w + 2f, r.height), "<color=" + GoldHex + ">" + text + "</color>", style);
        }

        static string StripColour(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<color=[^>]*>|</color>", "");

        public static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

        /// Fills a rectangle turned by `angle` degrees around `pivot` (for diagonal blades and handles).
        /// It's drawn as a row of small squares along its length, not by rotating GUI.matrix: Unity's
        /// RotateAroundPivot ignores the HUD's scale, which threw rotated icons off their slots.
        public static void FillRotated(Rect r, Color c, float angle, Vector2 pivot)
        {
            float rad = angle * Mathf.Deg2Rad, cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            bool tall = r.height >= r.width;
            float thick = tall ? r.width : r.height;
            float half = ((tall ? r.height : r.width) - thick) / 2f;
            float cx = r.x + r.width / 2f, cy = r.y + r.height / 2f;
            // The two ends of the long axis, turned round the pivot (y points down, so +angle is clockwise).
            float ax = tall ? cx : cx - half, ay = tall ? cy - half : cy;
            float bx = tall ? cx : cx + half, by = tall ? cy + half : cy;
            float x0 = pivot.x + (ax - pivot.x) * cos - (ay - pivot.y) * sin, y0 = pivot.y + (ax - pivot.x) * sin + (ay - pivot.y) * cos;
            float x1 = pivot.x + (bx - pivot.x) * cos - (by - pivot.y) * sin, y1 = pivot.y + (bx - pivot.x) * sin + (by - pivot.y) * cos;
            float length = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(0.75f, thick * 0.5f)));
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float x = x0 + (x1 - x0) * t, y = y0 + (y1 - y0) * t;
                Fill(new Rect(x - thick / 2f, y - thick / 2f, thick, thick), c);
            }
        }

        /// A filled diamond (a square turned 45 degrees), drawn row by row.
        public static void FillDiamond(float cx, float cy, float radius, Color c)
        {
            for (float dy = -radius; dy < radius; dy += 1f)
            {
                float w = (radius - Mathf.Abs(dy + 0.5f)) * 2f;
                Fill(new Rect(cx - w / 2f, cy + dy, w, 1f), c);
            }
        }

        /// Paints an icon (Core: Icon) into a rectangle, scaled to fit.
        public static void DrawIcon(Rect r, Icon icon)
        {
            if (icon == null)
                return;
            float s = r.width / icon.Size;
            foreach (var shape in icon.Shapes)
            {
                var c = ModelBuilder.ToColor(shape.Colour);
                if (shape.Diamond)
                    FillDiamond(r.x + shape.X * s, r.y + shape.Y * s, shape.W * s, c);
                else if (shape.Angle != 0f)
                    FillRotated(new Rect(r.x + shape.X * s, r.y + shape.Y * s, shape.W * s, shape.H * s), c, shape.Angle,
                        new Vector2(r.x + shape.PivotX * s, r.y + shape.PivotY * s));
                else
                    Fill(new Rect(r.x + shape.X * s, r.y + shape.Y * s, shape.W * s, shape.H * s), c);
            }
        }

        public static void DrawItemIcon(Rect slot, ItemDef def)
        {
            if (def != null)
                DrawIcon(slot, def.Icon);
        }

        public static string Count(long n) => n < 100000 ? "<color=#ffff00>" + n + "</color>"
            : n < 10000000 ? "<color=#ffffff>" + (n / 1000) + "K</color>" : "<color=#00ff80>" + (n / 1000000) + "M</color>";
    }
}
