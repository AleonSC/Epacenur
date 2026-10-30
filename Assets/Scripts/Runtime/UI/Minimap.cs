using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The minimap (top right): the ground round you from above, turned with the camera like RuneScape's,
    /// with dots for monsters (yellow), items on the ground (red), banks, shops and altars, fishing spots and
    /// your gravestone. North is marked on the rim. Click it to walk there (tile mode).
    public sealed class Minimap
    {
        public const float Size = 150f;           // on screen, HUD units
        const int Pixels = 128;                   // texture size
        const float TilesAcross = 60f;            // how much ground it shows
        const float TilesPerPixel = TilesAcross / Pixels;
        const int CacheSize = 68;                 // tiles round you kept ready (covers the circle at any angle)

        static Game G => Game.Instance;
        Texture2D tex;
        readonly Color32[] pixels = new Color32[Pixels * Pixels];
        readonly Color32[] cache = new Color32[CacheSize * CacheSize];
        Tile cacheOrigin;
        Tile cacheFor = new Tile(int.MinValue, 0);
        float cacheTime;
        float px, pz, yaw;  // the player in map space, and the camera's facing (radians)

        static readonly Color32 Clear = new Color32(0, 0, 0, 0), Unknown = new Color32(18, 16, 12, 255), Rim = new Color32(168, 135, 79, 255),
            RimEdge = new Color32(8, 6, 5, 255);

        public static Rect RectFor(Hud hud) => new Rect(hud.W - Size - 10f, 8f, Size, Size);

        public void Draw(Hud hud)
        {
            var r = RectFor(hud);
            var e = Event.current;
            if (e.type == EventType.Repaint)
            {
                Render();
                GUI.DrawTexture(r, tex);
                DrawCompass(r);
            }
            else if (e.type == EventType.MouseDown && e.button == 0 && Inside(r, e.mousePosition) && G.Player.Mode == ViewMode.Tile)
            {
                // Back from the screen to the ground: the click's offset, turned with the camera.
                float dx = (e.mousePosition.x - r.center.x) / r.width * TilesAcross;
                float dy = -(e.mousePosition.y - r.center.y) / r.height * TilesAcross;
                ToWorld(dx, dy, out float wx, out float wz);
                G.Player.WalkToTile(new Tile(Mathf.RoundToInt(wx), Mathf.RoundToInt(wz)));
                e.Use();
            }
        }

        static bool Inside(Rect r, Vector2 m)
        {
            float dx = m.x - r.center.x, dy = m.y - r.center.y, rad = r.width / 2f;
            return dx * dx + dy * dy <= rad * rad;
        }

        void ToWorld(float dx, float dy, out float wx, out float wz)
        {
            float s = Mathf.Sin(yaw), c = Mathf.Cos(yaw);
            // Screen right = (cos, -sin), screen up = the camera's forward = (sin, cos).
            wx = px + c * dx + s * dy;
            wz = pz - s * dx + c * dy;
        }

        void Render()
        {
            if (tex == null)
                tex = new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var p = G.Player.transform.position;
            px = p.x + World.Origin.X;
            pz = p.z + World.Origin.Z;
            yaw = G.Rig.Yaw * Mathf.Deg2Rad;
            var here = G.Player.CurrentTile;
            if (here != cacheFor || Time.time - cacheTime > 1f)
                FillCache(here);

            float s = Mathf.Sin(yaw), c = Mathf.Cos(yaw);
            const float half = Pixels / 2f, outer = half - 0.5f, edge = half - 1.5f, rim = half - 3f, inside = half - 4f;
            for (int y = 0; y < Pixels; y++)
                for (int x = 0; x < Pixels; x++)
                {
                    float ox = x + 0.5f - half, oy = y + 0.5f - half, d2 = ox * ox + oy * oy;
                    int i = y * Pixels + x;
                    if (d2 > outer * outer) { pixels[i] = Clear; continue; }
                    if (d2 > edge * edge) { pixels[i] = RimEdge; continue; }  // a dark edge, a gold ring, a dark line inside it
                    if (d2 > rim * rim) { pixels[i] = Rim; continue; }
                    if (d2 > inside * inside) { pixels[i] = RimEdge; continue; }
                    float dx = ox * TilesPerPixel, dy = oy * TilesPerPixel;
                    int tx = Mathf.RoundToInt(px + c * dx + s * dy) - cacheOrigin.X;
                    int tz = Mathf.RoundToInt(pz - s * dx + c * dy) - cacheOrigin.Z;
                    pixels[i] = tx >= 0 && tz >= 0 && tx < CacheSize && tz < CacheSize ? cache[tx * CacheSize + tz] : Unknown;
                }

            // Dots.
            var w = G.World;
            foreach (var g in w.GroundItems)
                if (g != null) Dot(g.transform.position, 1, new Color32(230, 40, 30, 255));
            foreach (var st in w.Stations)
                if (st != null && !(st is Fire)) Dot(st.transform.position, 2, StationColour(st.Def.Kind));
            foreach (var k in w.ShopKeepers)
                if (k != null) Dot(k.transform.position, 2, new Color32(250, 150, 40, 255));
            foreach (var n in w.Nodes)
                if (n != null && n.Def.Look == NodeLook.FishingSpot) Dot(n.transform.position, 1, new Color32(120, 230, 255, 255));
            foreach (var npc in w.Npcs)
                if (npc != null && !npc.IsDead) Dot(npc.transform.position, 1, new Color32(250, 240, 40, 255));
            var grave = G.Grave;
            if (grave != null && grave.world == G.WorldInfo.id && grave.map == (int)w.Map)
                Dot(World.TileCenter(grave.Tile), 2, new Color32(235, 235, 240, 255));
            // You: a white dot with a short line the way you face.
            Dot(G.Player.transform.position, 1, new Color32(255, 255, 255, 255));
            Dot(G.Player.transform.position + G.Player.transform.forward * 1.2f, 0, new Color32(255, 255, 255, 255));
            tex.SetPixels32(pixels);
            tex.Apply();
        }

        static Color32 StationColour(StationKind kind) =>
            kind == StationKind.Bank ? new Color32(255, 215, 60, 255) : kind == StationKind.Altar ? new Color32(250, 250, 250, 255) : new Color32(150, 200, 255, 255);

        /// A square dot `radius` pixels out from a world position's spot, if it's inside the circle.
        void Dot(Vector3 world, int radius, Color32 colour)
        {
            float dx = world.x + World.Origin.X - px, dz = world.z + World.Origin.Z - pz;
            float s = Mathf.Sin(yaw), c = Mathf.Cos(yaw);
            float sx = (c * dx - s * dz) / TilesPerPixel, sy = (s * dx + c * dz) / TilesPerPixel;
            const float half = Pixels / 2f, inner = half - 4f;
            if (sx * sx + sy * sy > inner * inner)
                return;
            int cx = Mathf.RoundToInt(half + sx - 0.5f), cy = Mathf.RoundToInt(half + sy - 0.5f);
            for (int y = cy - radius; y <= cy + radius; y++)
                for (int x = cx - radius; x <= cx + radius; x++)
                    if (x >= 0 && y >= 0 && x < Pixels && y < Pixels)
                        pixels[y * Pixels + x] = colour;
        }

        void FillCache(Tile center)
        {
            cacheFor = center;
            cacheTime = Time.time;
            cacheOrigin = new Tile(center.X - CacheSize / 2, center.Z - CacheSize / 2);
            var w = G.World;
            for (int x = 0; x < CacheSize; x++)
                for (int z = 0; z < CacheSize; z++)
                    cache[x * CacheSize + z] = w.TryMapColour(new Tile(cacheOrigin.X + x, cacheOrigin.Z + z), out var c) ? c : Unknown;
        }

        /// "N" on the rim, where north is.
        void DrawCompass(Rect r)
        {
            // North (0, 1) on screen: right = (cos, -sin) gives x = -sin; up = (sin, cos) gives y = cos.
            float nx = -Mathf.Sin(yaw), ny = Mathf.Cos(yaw);
            float rad = r.width / 2f - 9f;
            var at = new Rect(r.center.x + nx * rad - 8f, r.center.y - ny * rad - 9f, 16f, 18f);
            var badge = new Rect(at.x + 1f, at.y + 2f, 14f, 14f);
            Gui.Fill(badge, Gui.Edge);
            Gui.Fill(new Rect(badge.x + 1f, badge.y + 1f, badge.width - 2f, badge.height - 2f), Gui.HeaderBg);
            Gui.Outline(new Rect(badge.x + 1f, badge.y + 1f, badge.width - 2f, badge.height - 2f), Gui.TrimDim);
            Gui.Shadowed(at, "<b><color=#ff5a48>N</color></b>", Gui.Centered);
        }
    }
}
