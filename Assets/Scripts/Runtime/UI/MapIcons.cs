using System;
using System.Collections.Generic;
using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// What a map icon marks.
    public enum MapIcon
    {
        Bank, Shop, Trading, Chapel, RuneAltar, Mine, Cave, Slayer, Smithy, Furnace, Workshop, Farm, Well, Agility, Camp,
        Landmark, Fishing, Ladder,
    }

    /// The icons on the minimap and the world map, RuneScape style: a round badge with a little picture on it.
    /// Each is painted once into a small texture (no art files), and the places they mark are worked out from the
    /// overworld's plans (Features), so the world map shows them for regions that aren't loaded.
    public static class MapIcons
    {
        public struct Feature
        {
            public Tile At;
            public MapIcon Icon;
            public Feature(Tile at, MapIcon icon) { At = at; Icon = icon; }
        }

        const int S = 32;  // texture size
        static Texture2D[] textures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => textures = null;

        public static string Name(MapIcon i)
        {
            switch (i)
            {
                case MapIcon.Trading: return "Trading post";
                case MapIcon.Chapel: return "Chapel (prayer altar)";
                case MapIcon.RuneAltar: return "Runecrafting altar";
                case MapIcon.Mine: return "Mine shaft";
                case MapIcon.Cave: return "Cave (dungeon)";
                case MapIcon.Slayer: return "Slayer master";
                case MapIcon.Smithy: return "Smithy (anvil)";
                case MapIcon.Workshop: return "Workshop (spinning, tanning)";
                case MapIcon.Agility: return "Agility course";
                case MapIcon.Camp: return "Monster camp";
                case MapIcon.Fishing: return "Fishing spot";
                case MapIcon.Ladder: return "Ladder up";
                default: return i.ToString();
            }
        }

        /// Draws an icon centred on a point, `size` across.
        public static void Draw(Vector2 at, MapIcon icon, float size)
        {
            if (textures == null)
                Make();
            GUI.DrawTexture(new Rect(at.x - size / 2f, at.y - size / 2f, size, size), textures[(int)icon]);
        }

        // ---- Where they go ----

        /// The icons a region of the overworld has: a town's bank, shop, chapel, smithy, workshop, farm, slayer
        /// master, well and trading post; a shrine's altar; a mine shaft; a cave; a course; a camp; a landmark.
        public static void Features(RegionPlan plan, List<Feature> into)
        {
            switch (plan.Kind)
            {
                case RegionKind.Town:
                    var master = plan.Buildings.Find(x => x.Kind == BuildingKind.House);  // the slayer master's house (OverworldBuilder)
                    foreach (var b in plan.Buildings)
                    {
                        var at = b.Footprint.Center;
                        switch (b.Kind)
                        {
                            case BuildingKind.Bank: into.Add(new Feature(at, MapIcon.Bank)); break;
                            case BuildingKind.Shop: into.Add(new Feature(at, MapIcon.Shop)); break;
                            case BuildingKind.Chapel: into.Add(new Feature(at, MapIcon.Chapel)); break;
                            case BuildingKind.Smithy: into.Add(new Feature(at, MapIcon.Smithy)); break;
                            case BuildingKind.Workshop: into.Add(new Feature(at, MapIcon.Workshop)); break;
                            case BuildingKind.Farm: into.Add(new Feature(b.Field.Center, MapIcon.Farm)); break;
                            case BuildingKind.House: if (b == master) into.Add(new Feature(at, MapIcon.Slayer)); break;
                        }
                    }
                    if (plan.HasTradingPost)
                        into.Add(new Feature(plan.TradingPost, MapIcon.Trading));
                    if (plan.HasWell)
                        into.Add(new Feature(plan.Well, MapIcon.Well));
                    break;
                case RegionKind.Shrine: into.Add(new Feature(plan.Altar, MapIcon.RuneAltar)); break;
                case RegionKind.Quarry: into.Add(new Feature(plan.CaveMouth, MapIcon.Mine)); break;
                case RegionKind.Cave: into.Add(new Feature(plan.CaveMouth, MapIcon.Cave)); break;
                case RegionKind.Camp: into.Add(new Feature(plan.Camp.Center, MapIcon.Camp)); break;
                case RegionKind.Course:
                    into.Add(new Feature(new Tile(plan.CourseCorner.X + AgilityCourse.Width / 2, plan.CourseCorner.Z + AgilityCourse.Depth / 2), MapIcon.Agility));
                    break;
                case RegionKind.Wild:
                    if (plan.Landmark != LandmarkKind.None)
                        into.Add(new Feature(plan.LandmarkArea.Center, MapIcon.Landmark));
                    break;
            }
        }

        /// The icons round you that come from what's been built rather than the overworld's plans: on the dev plot
        /// and in dungeons, the stations, shop keepers and the ladder up.
        public static void Built(World w, List<Feature> into)
        {
            foreach (var st in w.Stations)
                if (st != null)
                {
                    var icon = ForStation(st.Def);
                    if (icon.HasValue)
                        into.Add(new Feature(st.Tile, icon.Value));
                }
            foreach (var k in w.ShopKeepers)
                if (k != null)
                    into.Add(new Feature(k.Tile, MapIcon.Shop));
            if (w.Map == MapId.Dungeon && w.CurrentDungeon != null)
                into.Add(new Feature(w.CurrentDungeon.Ladder, MapIcon.Ladder));
        }

        /// The icon for a station that's been built (on the dev plot), or null for none.
        public static MapIcon? ForStation(StationDef def)
        {
            switch (def.Kind)
            {
                case StationKind.Bank: return MapIcon.Bank;
                case StationKind.Altar: return MapIcon.Chapel;
                case StationKind.Craft: return MapIcon.RuneAltar;
                case StationKind.SlayerMaster: return MapIcon.Slayer;
                case StationKind.Farmer: return MapIcon.Farm;
            }
            if (def.Id == SmithingModule.Furnace) return MapIcon.Furnace;
            if (def.Id == SmithingModule.Anvil) return MapIcon.Smithy;
            if (def.Id == FiremakingModule.Fire) return null;
            return MapIcon.Workshop;  // spinning wheel, tanning rack
        }

        // ---- Painting them ----

        static Color32[] px;

        static void Make()
        {
            textures = new Texture2D[Enum.GetValues(typeof(MapIcon)).Length];
            foreach (MapIcon i in Enum.GetValues(typeof(MapIcon)))
            {
                px = new Color32[S * S];
                Paint(i);
                var t = new Texture2D(S, S, TextureFormat.RGBA32, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                t.SetPixels32(px);
                t.Apply();
                textures[(int)i] = t;
            }
        }

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static void Paint(MapIcon i)
        {
            // The badge: a dark rim round a coloured disc, lighter at the top.
            Color bg;
            switch (i)
            {
                case MapIcon.Bank: bg = C(0.18f, 0.42f, 0.2f); break;
                case MapIcon.Shop: bg = C(0.5f, 0.3f, 0.12f); break;
                case MapIcon.Trading: bg = C(0.14f, 0.2f, 0.45f); break;
                case MapIcon.Chapel: bg = C(0.32f, 0.42f, 0.72f); break;
                case MapIcon.RuneAltar: bg = C(0.42f, 0.2f, 0.58f); break;
                case MapIcon.Mine: bg = C(0.45f, 0.38f, 0.3f); break;
                case MapIcon.Cave: bg = C(0.55f, 0.52f, 0.48f); break;
                case MapIcon.Slayer: bg = C(0.5f, 0.1f, 0.1f); break;
                case MapIcon.Smithy: bg = C(0.24f, 0.24f, 0.28f); break;
                case MapIcon.Furnace: bg = C(0.32f, 0.14f, 0.08f); break;
                case MapIcon.Workshop: bg = C(0.55f, 0.4f, 0.24f); break;
                case MapIcon.Farm: bg = C(0.4f, 0.27f, 0.12f); break;
                case MapIcon.Well: bg = C(0.14f, 0.3f, 0.6f); break;
                case MapIcon.Agility: bg = C(0.12f, 0.48f, 0.52f); break;
                case MapIcon.Camp: bg = C(0.5f, 0.2f, 0.12f); break;
                case MapIcon.Landmark: bg = C(0.38f, 0.36f, 0.33f); break;
                case MapIcon.Fishing: bg = C(0.1f, 0.35f, 0.62f); break;
                default: bg = C(0.3f, 0.25f, 0.2f); break;
            }
            Fill((x, y) => Circle(x, y, 16, 16, 15f), C(0.04f, 0.03f, 0.02f));
            Fill((x, y) => Circle(x, y, 16, 16, 13.4f), bg);
            Fill((x, y) => Math.Max(Circle(x, y, 16, 16, 13.4f), Circle(x, y, 16, 9, 10f)), Color.Lerp(bg, Color.white, 0.18f));

            var light = C(0.96f, 0.94f, 0.88f);
            switch (i)
            {
                case MapIcon.Bank:  // a stack of gold coins
                    foreach (float y in new[] { 21f, 16.5f, 12f })
                    {
                        Fill((x, yy) => Box(x, yy, 9.5f, y - 1.2f, 22.5f, y + 1.2f) - 2.2f, C(0.35f, 0.25f, 0.02f));
                        Fill((x, yy) => Box(x, yy, 10.5f, y - 0.6f, 21.5f, y + 0.6f) - 1.7f, C(1f, 0.82f, 0.25f));
                    }
                    break;
                case MapIcon.Shop:  // a money bag
                    Fill((x, y) => Math.Min(Circle(x, y, 16, 19.5f, 7.5f), Box(x, y, 13, 9, 19, 13)), C(0.92f, 0.8f, 0.55f));
                    Fill((x, y) => Box(x, y, 11.5f, 12, 20.5f, 13.6f), C(0.45f, 0.25f, 0.1f));
                    Fill((x, y) => Box(x, y, 15f, 16, 17f, 23), C(0.75f, 0.55f, 0.2f));
                    break;
                case MapIcon.Trading:  // arrows both ways
                    var gold = C(1f, 0.85f, 0.35f);
                    Fill((x, y) => Math.Min(Segment(x, y, 8, 12, 21, 12) - 1.4f, Poly(x, y, 21, 7.5f, 26.5f, 12, 21, 16.5f)), gold);
                    Fill((x, y) => Math.Min(Segment(x, y, 11, 20, 24, 20) - 1.4f, Poly(x, y, 11, 24.5f, 5.5f, 20, 11, 15.5f)), gold);
                    break;
                case MapIcon.Chapel:  // a cross
                    Fill((x, y) => Math.Min(Box(x, y, 14.5f, 6, 17.5f, 26), Box(x, y, 10, 11, 22, 14)), light);
                    break;
                case MapIcon.RuneAltar:  // a rune stone
                    Fill((x, y) => Poly(x, y, 16, 5.5f, 26, 16, 16, 26.5f, 6, 16), C(0.85f, 0.8f, 0.98f));
                    Fill((x, y) => Poly(x, y, 16, 11, 21, 16, 16, 21, 11, 16), C(0.55f, 0.35f, 0.85f));
                    break;
                case MapIcon.Mine:  // a pickaxe
                    Fill((x, y) => Segment(x, y, 9, 25, 20, 12) - 1.5f, C(0.75f, 0.55f, 0.3f));
                    Fill((x, y) => Math.Min(Segment(x, y, 12, 9, 19, 9.5f) - 1.7f, Segment(x, y, 19, 9.5f, 25, 15) - 1.7f), C(0.85f, 0.87f, 0.9f));
                    break;
                case MapIcon.Cave:  // a dark opening
                    Fill((x, y) => Math.Min(Box(x, y, 10, 16, 22, 25), Circle(x, y, 16, 16, 6)), C(0.04f, 0.04f, 0.05f));
                    Fill((x, y) => Box(x, y, 6, 24, 26, 26), C(0.3f, 0.28f, 0.25f));
                    break;
                case MapIcon.Slayer:  // a skull
                    Fill((x, y) => Math.Min(Circle(x, y, 16, 14, 8), Box(x, y, 12, 17, 20, 24) - 1f), light);
                    Fill((x, y) => Math.Min(Circle(x, y, 12.8f, 14.5f, 2.3f), Circle(x, y, 19.2f, 14.5f, 2.3f)), C(0.15f, 0.05f, 0.05f));
                    Fill((x, y) => Math.Min(Box(x, y, 14, 21, 15, 24), Box(x, y, 17, 21, 18, 24)), C(0.15f, 0.05f, 0.05f));
                    break;
                case MapIcon.Smithy:  // an anvil
                    Fill((x, y) => Math.Min(Math.Min(Box(x, y, 9, 11, 24, 15), Poly(x, y, 4, 11, 9, 11, 9, 14.5f)),
                        Math.Min(Box(x, y, 13, 15, 19, 20), Box(x, y, 10, 20, 22, 23))), C(0.78f, 0.8f, 0.84f));
                    break;
                case MapIcon.Furnace:  // a flame
                    Fill((x, y) => Math.Min(Circle(x, y, 16, 20, 5.5f), Poly(x, y, 10.6f, 19, 16, 5.5f, 21.4f, 19)), C(1f, 0.55f, 0.12f));
                    Fill((x, y) => Math.Min(Circle(x, y, 16, 21.5f, 2.8f), Poly(x, y, 13.3f, 21, 16, 13, 18.7f, 21)), C(1f, 0.9f, 0.4f));
                    break;
                case MapIcon.Workshop:  // a spinning wheel
                    Fill((x, y) => Math.Abs(Circle(x, y, 16, 16, 8)) - 1.3f, C(0.95f, 0.82f, 0.6f));
                    Fill((x, y) => Math.Min(Segment(x, y, 16, 8, 16, 24), Segment(x, y, 8, 16, 24, 16)) - 0.8f, C(0.95f, 0.82f, 0.6f));
                    Fill((x, y) => Math.Min(Segment(x, y, 10.3f, 10.3f, 21.7f, 21.7f), Segment(x, y, 10.3f, 21.7f, 21.7f, 10.3f)) - 0.7f, C(0.85f, 0.7f, 0.5f));
                    Fill((x, y) => Circle(x, y, 16, 16, 2.2f), C(0.4f, 0.25f, 0.12f));
                    break;
                case MapIcon.Farm:  // a sprout
                    Fill((x, y) => Segment(x, y, 16, 25, 16, 13) - 1.2f, C(0.45f, 0.8f, 0.3f));
                    Fill((x, y) => Math.Min(Circle(x, y, 11.5f, 13.5f, 4.2f), Circle(x, y, 20.5f, 11.5f, 4.6f)), C(0.55f, 0.92f, 0.35f));
                    Fill((x, y) => Box(x, y, 9, 24, 23, 26), C(0.6f, 0.42f, 0.22f));
                    break;
                case MapIcon.Well:  // a drop of water
                    Fill((x, y) => Math.Min(Circle(x, y, 16, 19.5f, 6.2f), Poly(x, y, 10.3f, 17.5f, 16, 5.5f, 21.7f, 17.5f)), C(0.65f, 0.88f, 1f));
                    Fill((x, y) => Circle(x, y, 13.8f, 19.5f, 1.7f), C(0.95f, 1f, 1f));
                    break;
                case MapIcon.Agility:  // running chevrons
                    Fill((x, y) => Math.Min(Math.Min(Segment(x, y, 8, 9, 14, 16), Segment(x, y, 14, 16, 8, 23)),
                        Math.Min(Segment(x, y, 16, 9, 22, 16), Segment(x, y, 22, 16, 16, 23))) - 1.6f, light);
                    break;
                case MapIcon.Camp:  // a tent
                    Fill((x, y) => Poly(x, y, 6, 24, 16, 7, 26, 24), C(0.85f, 0.65f, 0.42f));
                    Fill((x, y) => Poly(x, y, 13, 24, 16, 16.5f, 19, 24), C(0.2f, 0.1f, 0.05f));
                    break;
                case MapIcon.Landmark:  // an obelisk
                    Fill((x, y) => Math.Min(Poly(x, y, 13, 24, 14, 10, 16, 6.5f, 18, 10, 19, 24), Box(x, y, 10, 23.5f, 22, 26)), C(0.85f, 0.83f, 0.78f));
                    break;
                case MapIcon.Fishing:  // a fish
                    Fill((x, y) => Math.Min((float)(Math.Sqrt((x - 14) * (x - 14) / 49.0 + (y - 16) * (y - 16) / 16.0) - 1) * 4.5f, Poly(x, y, 19.5f, 16, 26, 10.5f, 26, 21.5f)),
                        C(0.75f, 0.9f, 1f));
                    Fill((x, y) => Circle(x, y, 10.5f, 15, 1.2f), C(0.05f, 0.15f, 0.3f));
                    break;
                case MapIcon.Ladder:
                    var wood = C(0.9f, 0.72f, 0.45f);
                    Fill((x, y) => Math.Min(Segment(x, y, 11.5f, 6, 11.5f, 26), Segment(x, y, 20.5f, 6, 20.5f, 26)) - 1.2f, wood);
                    foreach (float r in new[] { 9.5f, 14f, 18.5f, 23f })
                        Fill((x, y) => Segment(x, y, 11.5f, r, 20.5f, r) - 0.9f, wood);
                    break;
            }
        }

        /// Blends `c` over the icon where `shape` (a signed distance: below 0 inside) covers each pixel, edges smoothed.
        /// x and y are in the icon's own 32 x 32 grid, y down.
        static void Fill(Func<float, float, float> shape, Color c)
        {
            for (int row = 0; row < S; row++)
                for (int col = 0; col < S; col++)
                {
                    float a = Mathf.Clamp01(0.5f - shape(col + 0.5f, row + 0.5f));
                    if (a <= 0f)
                        continue;
                    int k = (S - 1 - row) * S + col;  // textures run bottom-up
                    var under = (Color)px[k];
                    var o = new Color(c.r * a + under.r * under.a * (1 - a), c.g * a + under.g * under.a * (1 - a), c.b * a + under.b * under.a * (1 - a), a + under.a * (1 - a));
                    if (o.a > 0f)
                        o = new Color(o.r / o.a, o.g / o.a, o.b / o.a, o.a);
                    px[k] = o;
                }
        }

        static float Circle(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        static float Box(float x, float y, float x0, float y0, float x1, float y1)
        {
            float dx = Mathf.Max(x0 - x, x - x1), dy = Mathf.Max(y0 - y, y - y1);
            if (dx <= 0f && dy <= 0f)
                return Mathf.Max(dx, dy);
            float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy);
        }

        static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay, wx = x - ax, wy = y - ay;
            float t = Mathf.Clamp01((wx * vx + wy * vy) / (vx * vx + vy * vy));
            float dx = wx - vx * t, dy = wy - vy * t;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// A convex polygon, its corners as x, y pairs (either way round): how far outside its furthest edge.
        static float Poly(float x, float y, params float[] p)
        {
            int n = p.Length / 2;
            // Which way round: the sign of the area.
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += p[i * 2] * p[j * 2 + 1] - p[j * 2] * p[i * 2 + 1];
            }
            float s = area > 0f ? 1f : -1f, d = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float ex = p[j * 2] - p[i * 2], ey = p[j * 2 + 1] - p[i * 2 + 1];
                float len = Mathf.Sqrt(ex * ex + ey * ey);
                // Distance outward from this edge (positive outside).
                float out_ = ((x - p[i * 2]) * ey - (y - p[i * 2 + 1]) * ex) / len * s;
                d = Mathf.Max(d, out_);
            }
            return d;
        }
    }
}
