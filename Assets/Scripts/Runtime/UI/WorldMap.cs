using System.Collections.Generic;
using System.Diagnostics;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The world map (M, or the button under the minimap): the overworld from above, north up. Drag to move
    /// round it, scroll to zoom. Places you haven't seen yet are shaded; towns and goblin camps you have are
    /// named. The wilderness is tinted red, deeper the more dangerous, so the safe rings round towns show. You, and your gravestone, are marked. On the dev plot it shows the dev plot.
    ///
    /// The map is drawn from the world's generator, a 64-tile region at a time, a few each frame (sharper
    /// close up, coarser zoomed out), and remembered.
    public sealed class WorldMap
    {
        static Game G => Game.Instance;
        const int RegionSize = Overworld.RegionSize;
        const float MinZoom = 0.25f, MaxZoom = 8f;  // HUD pixels per tile
        const double BuildBudgetMs = 6.0;            // per frame
        const int MaxCached = 600;

        public bool IsOpen { get; private set; }
        float zoom = 2f;
        Vector2 center;  // the map tile in the middle of the view (x, z)
        bool dragging;
        Vector2 dragFrom;
        // Region pictures, per level of detail (every tile, every 2nd, every 4th).
        readonly Dictionary<long, Texture2D>[] cache = { new Dictionary<long, Texture2D>(), new Dictionary<long, Texture2D>(), new Dictionary<long, Texture2D>() };
        int cachedSeed = int.MinValue;
        Texture2D devTexture;

        public void Toggle()
        {
            IsOpen = !IsOpen;
            if (IsOpen)
                CenterOnPlayer();
        }

        public void Close() => IsOpen = false;

        void CenterOnPlayer()
        {
            var p = G.Player.transform.position;
            center = new Vector2(p.x + World.Origin.X, p.z + World.Origin.Z);
        }

        public Rect RectFor(Hud hud)
        {
            float w = Mathf.Min(hud.W - 40f, 1100f), h = Mathf.Min(hud.H - 40f, 760f);
            return new Rect((hud.W - w) / 2f, (hud.H - h) / 2f, w, h);
        }

        public void Draw(Hud hud)
        {
            if (!IsOpen)
                return;
            var r = RectFor(hud);
            var e = Event.current;
            string title = G.World.Map == MapId.DevPlot ? "Dev plot" : "World map: " + G.WorldInfo.name;
            if (Gui.TitledPanel(r, title, true))
            {
                Close();
                return;
            }
            if (GUI.Button(new Rect(r.xMax - 146f, r.y + 6f, 112f, 20f), "Center on me"))
                CenterOnPlayer();

            var view = new Rect(r.x + 8f, r.y + Gui.TitleH + 4f, r.width - 16f, r.height - Gui.TitleH - 32f);
            Gui.Fill(new Rect(view.x - 1f, view.y - 1f, view.width + 2f, view.height + 2f), Gui.Edge);
            Gui.Fill(view, new Color(0.05f, 0.05f, 0.05f, 1f));
            GUI.BeginGroup(view);  // clips the map to the view
            var local = new Rect(0f, 0f, view.width, view.height);
            if (G.World.Map != MapId.Overworld)
                DrawDevPlot(local);
            else
                DrawOverworld(local);
            DrawMarkers(local);
            string hover = HoverText(local, e.mousePosition);
            HandleInput(local, e);
            GUI.EndGroup();

            Gui.Shadowed(new Rect(r.x + 10f, r.yMax - 24f, r.width - 20f, 20f),
                hover ?? "<color=" + Gui.MutedHex + ">Drag to move round the map, scroll to zoom. Shaded areas haven't been seen yet; red is wilderness, deeper the more dangerous.</color>", Gui.Small);
        }

        // ---- Overworld ----

        /// Map tile (x, z) to a point in the view; north is up.
        Vector2 ToView(Rect v, float x, float z) => new Vector2(v.width / 2f + (x - center.x) * zoom, v.height / 2f - (z - center.y) * zoom);

        Vector2 ToMap(Rect v, Vector2 p) => new Vector2(center.x + (p.x - v.width / 2f) / zoom, center.y - (p.y - v.height / 2f) / zoom);

        void DrawOverworld(Rect v)
        {
            var ow = G.World.Overworld;
            if (ow.Seed != cachedSeed)
                ClearCache(ow.Seed);
            int level = zoom >= 1f ? 0 : zoom >= 0.5f ? 1 : 2;
            var min = ToMap(v, new Vector2(0f, v.height));
            var max = ToMap(v, new Vector2(v.width, 0f));
            int rx0 = Overworld.RegionOf(Mathf.FloorToInt(min.x)), rx1 = Overworld.RegionOf(Mathf.CeilToInt(max.x));
            int rz0 = Overworld.RegionOf(Mathf.FloorToInt(min.y)), rz1 = Overworld.RegionOf(Mathf.CeilToInt(max.y));

            // Build the missing pictures nearest the middle first, within this frame's budget.
            var missing = new List<long>();
            for (int rx = rx0; rx <= rx1; rx++)
                for (int rz = rz0; rz <= rz1; rz++)
                    if (!cache[level].ContainsKey(ChunkedGrid.Key(rx, rz)))
                        missing.Add(ChunkedGrid.Key(rx, rz));
            if (missing.Count > 0 && Event.current.type == EventType.Repaint)
            {
                int crx = Overworld.RegionOf(Mathf.RoundToInt(center.x)), crz = Overworld.RegionOf(Mathf.RoundToInt(center.y));
                missing.Sort((a, b) => Dist(a, crx, crz).CompareTo(Dist(b, crx, crz)));
                var clock = Stopwatch.StartNew();
                foreach (var key in missing)
                {
                    if (clock.Elapsed.TotalMilliseconds > BuildBudgetMs)
                        break;
                    cache[level][key] = BuildRegion(ow, (int)(key >> 32), (int)(uint)key, 1 << level);
                }
                if (cache[level].Count > MaxCached)
                    ClearCache(ow.Seed);
            }

            if (Event.current.type != EventType.Repaint)
                return;
            var explored = G.World.Explored;
            for (int rx = rx0; rx <= rx1; rx++)
                for (int rz = rz0; rz <= rz1; rz++)
                {
                    long key = ChunkedGrid.Key(rx, rz);
                    var topLeft = ToView(v, rx * RegionSize - 0.5f, (rz + 1) * RegionSize - 0.5f);
                    var rect = new Rect(topLeft.x, topLeft.y, RegionSize * zoom, RegionSize * zoom);
                    if (cache[level].TryGetValue(key, out var tex))
                        GUI.DrawTexture(rect, tex);
                    else
                        Gui.Fill(rect, new Color(0.12f, 0.12f, 0.1f, 1f));
                    if (!explored.Contains(key))
                        Gui.Fill(rect, new Color(0.03f, 0.03f, 0.05f, 0.6f));
                }
            // Names of the towns, camps, shrines and quarries you've seen.
            for (int rx = rx0; rx <= rx1; rx++)
                for (int rz = rz0; rz <= rz1; rz++)
                {
                    if (!explored.Contains(ChunkedGrid.Key(rx, rz)))
                        continue;
                    var plan = ow.Plan(rx, rz);
                    string name = OverworldBuilder.RegionName(ow, plan);
                    if (name == null)
                        continue;
                    var p = ToView(v, plan.Center.X, plan.Center.Z);
                    string colour = plan.Kind == RegionKind.Town ? "#ffd966" : plan.Kind == RegionKind.Quarry ? "#d8d4c8" : plan.Kind == RegionKind.Course ? "#9fc8ff" : "#ff9a80";
                    if (plan.City)
                        name = "<size=17>" + name + "</size>";  // cities stand out
                    Gui.Shadowed(new Rect(p.x - 120f, p.y - 32f, 240f, 24f), "<b><color=" + colour + ">" + name + "</color></b>", Gui.Centered);
                }
        }

        static int Dist(long key, int crx, int crz) => Mathf.Max(Mathf.Abs((int)(key >> 32) - crx), Mathf.Abs((int)(uint)key - crz));

        static Texture2D BuildRegion(Overworld ow, int rx, int rz, int step)
        {
            int n = RegionSize / step;
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var t = new Tile(rx * RegionSize + x * step, rz * RegionSize + y * step);
                    pixels[y * n + x] = Wilderness(World.HillShade(World.MapColour(ow, t), ow, t, step), ow.Danger(t));
                }
            var plan = ow.Plan(rx, rz);
            foreach (var t in plan.Blocked)  // the bank booths, altars, shop keepers and tents themselves
            {
                int x = (t.X - rx * RegionSize) / step, y = (t.Z - rz * RegionSize) / step;
                if (x >= 0 && y >= 0 && x < n && y < n)
                    pixels[y * n + x] = new Color32(235, 205, 90, 255);
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// Tints the wilderness red, deeper the more dangerous, so the safe rings round the towns show.
        static Color32 Wilderness(Color32 c, int danger)
        {
            if (danger == 0)
                return c;
            float t = 0.08f + 0.4f * Mathf.Min(1f, danger / 45f);
            return new Color32((byte)(c.r + (170 - c.r) * t), (byte)(c.g + (40 - c.g) * t), (byte)(c.b + (32 - c.b) * t), 255);
        }

        void ClearCache(int seed)
        {
            foreach (var level in cache)
            {
                foreach (var t in level.Values)
                    Object.Destroy(t);
                level.Clear();
            }
            cachedSeed = seed;
        }

        // ---- Dev plot ----

        object devTextureFor;

        void DrawDevPlot(Rect v)
        {
            // The dev plot or the dungeon you're in (made again for each dungeon).
            object which = G.World.Map == MapId.Dungeon ? (object)G.World.CurrentDungeon : G.World.Map;
            if (devTexture == null || !Equals(devTextureFor, which))
            {
                devTextureFor = which;
                int w = G.World.FixedWidth, h = G.World.FixedHeight;
                var pixels = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        pixels[y * w + x] = G.World.TryMapColour(new Tile(x, y), out var c) ? c : new Color32(20, 20, 20, 255);
                devTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                devTexture.SetPixels32(pixels);
                devTexture.Apply();
            }
            if (Event.current.type != EventType.Repaint)
                return;
            var topLeft = ToView(v, -0.5f, G.World.FixedHeight - 0.5f);
            GUI.DrawTexture(new Rect(topLeft.x, topLeft.y, G.World.FixedWidth * zoom, G.World.FixedHeight * zoom), devTexture);
            foreach (var area in G.World.Areas)
            {
                var p = ToView(v, area.Plot.center.x - 0.5f, area.Plot.yMax);
                Gui.Shadowed(new Rect(p.x - 80f, p.y - 4f, 160f, 20f), "<b>" + area.Name + "</b>", Gui.Centered);
            }
        }

        // ---- You, your gravestone ----

        void DrawMarkers(Rect v)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var g = G.Grave;
            if (g != null && g.world == G.WorldInfo.id && g.map == (int)G.World.Map)
            {
                var gt = World.GroundTile(new Tile(g.x, g.z));
                var gp = ToView(v, gt.X, gt.Z);
                Gui.Fill(new Rect(gp.x - 4f, gp.y - 6f, 8f, 12f), new Color(0.1f, 0.1f, 0.1f));
                Gui.Fill(new Rect(gp.x - 3f, gp.y - 5f, 6f, 10f), new Color(0.85f, 0.85f, 0.9f));
                Gui.Shadowed(new Rect(gp.x - 60f, gp.y + 6f, 120f, 18f), "<color=#e0e0e8>Gravestone</color>", Gui.Centered);
            }
            // Bases: their floors.
            if (G.World.Map == MapId.Overworld)
            {
                float cell = Mathf.Max(2f, zoom);
                var floor = new Color(0.69f, 0.55f, 0.38f);
                foreach (var b in G.World.Bases.All)
                    foreach (var k in b.Foundations.Keys)
                    {
                        Structure.Unkey(k, out int i, out int j);
                        b.ToMap(i, j, out double bx, out double bz);
                        var bp = ToView(v, (float)bx, (float)bz);
                        Gui.Fill(new Rect(bp.x - cell / 2f, bp.y - cell / 2f, cell, cell), floor);
                    }
            }
            var pos = G.Player.transform.position;
            var pp = ToView(v, pos.x + World.Origin.X, pos.z + World.Origin.Z);
            Gui.Fill(new Rect(pp.x - 5f, pp.y - 5f, 10f, 10f), Color.black);
            Gui.Fill(new Rect(pp.x - 4f, pp.y - 4f, 8f, 8f), Color.white);
            Gui.Shadowed(new Rect(pp.x - 60f, pp.y + 6f, 120f, 18f), "<b>You</b>", Gui.Centered);
        }

        string HoverText(Rect v, Vector2 mouse)
        {
            if (!v.Contains(mouse))
                return null;
            var m = ToMap(v, mouse);
            var t = new Tile(Mathf.RoundToInt(m.x), Mathf.RoundToInt(m.y));
            string where = "Tile " + t.X + ", " + t.Z;
            if (G.World.Map == MapId.Overworld)
            {
                var ow = G.World.Overworld;
                var plan = ow.PlanAt(t);
                bool seen = G.World.Explored.Contains(ChunkedGrid.Key(plan.RX, plan.RZ));
                string name = seen ? OverworldBuilder.RegionName(ow, plan) : "Unexplored";
                if (name != null)
                    where = name + "  -  " + where;
                if (seen)
                    where = Hud.DangerText(ow.Danger(t)) + "  -  " + ow.BiomeAt(t) + "  -  " + where;
            }
            return where;
        }

        void HandleInput(Rect v, Event e)
        {
            if (e.type == EventType.MouseDown && e.button == 0 && v.Contains(e.mousePosition))
            {
                dragging = true;
                dragFrom = e.mousePosition;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && dragging)
            {
                var moved = e.mousePosition - dragFrom;
                dragFrom = e.mousePosition;
                center += new Vector2(-moved.x, moved.y) / zoom;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && dragging)
            {
                dragging = false;
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && v.Contains(e.mousePosition))
            {
                // Zoom about the mouse: the tile under it stays put.
                var before = ToMap(v, e.mousePosition);
                zoom = Mathf.Clamp(zoom * (1f - e.delta.y * 0.08f), MinZoom, MaxZoom);
                var after = ToMap(v, e.mousePosition);
                center += before - after;
                e.Use();
            }
        }
    }
}
