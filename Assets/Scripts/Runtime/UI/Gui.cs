using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Drawing helpers and text styles shared by every part of the HUD. Everything is drawn from flat
    /// rectangles: there's no art.
    public static class Gui
    {
        public const float Slot = 40f, Gap = 4f, Pad = 10f;

        public static GUIStyle Label, Small, Title, Centered, Rich, RightLabel, RightSmall, TabStyle;

        static readonly Color CellBg = new Color(0.12f, 0.11f, 0.09f, 1f);
        public static readonly Color BevelLight = new Color(0.36f, 0.32f, 0.25f, 1f);
        static readonly Color BevelDark = new Color(0.04f, 0.04f, 0.03f, 1f);

        /// Made on the first OnGUI (GUI.skin only exists there).
        public static void MakeStyles()
        {
            if (Label != null)
                return;
            Label = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            Label.normal.textColor = Color.white;
            Small = new GUIStyle(Label) { fontSize = 11 };
            Title = new GUIStyle(Label) { fontSize = 15, fontStyle = FontStyle.Bold };
            Centered = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter };
            Rich = new GUIStyle(Label) { wordWrap = true };
            RightLabel = new GUIStyle(Label) { alignment = TextAnchor.UpperRight };
            RightSmall = new GUIStyle(Small) { alignment = TextAnchor.UpperRight, wordWrap = true };
            TabStyle = new GUIStyle(GUI.skin.button) { fontSize = 10, padding = new RectOffset(1, 1, 2, 2) };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Label = null;

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
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

        public static void Tooltip(Rect r, string text, GUIStyle style) => Shadowed(r, "<color=#ffcc66>" + text + "</color>", style);

        static string StripColour(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<color=[^>]*>|</color>", "");

        public static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

        /// A sunken box with a light bottom-right edge and a dark top-left edge.
        public static void Bevel(Rect r)
        {
            Fill(r, BevelLight);
            Fill(new Rect(r.x, r.y, r.width - 1f, r.height - 1f), BevelDark);
            Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), CellBg);
        }

        /// A dark box with a light outline (tooltips).
        public static void Box(Rect r)
        {
            Fill(r, new Color(0.06f, 0.05f, 0.04f, 0.95f));
            Fill(new Rect(r.x, r.y, r.width, 1f), BevelLight);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), BevelLight);
            Fill(new Rect(r.x, r.y, 1f, r.height), BevelLight);
            Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), BevelLight);
        }

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
