using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Dirt roads, town squares and the trampled ground of camps and cave mouths, drawn as a skin over the
    /// ground with a texture painted for each chunk: mottled brown earth with pebbles, its edge ragged and
    /// fading into the grass round it (the skin's outer tiles are painted the ground's own colour, so it
    /// meets the ground without a seam). It shares the ground's vertices and normals, so it's lit the same.
    /// The texture is painted a few rows at a time over the next frames (DirtPainter), so streaming chunks
    /// in doesn't stutter; the skin shows once it's done.
    public static class DirtPaths
    {
        internal const int Px = 12;         // texture pixels per tile
        internal const int Reach = 2;       // tiles round the dirt the skin covers (the fade is well inside this)
        const float Lift = 0.012f; // above the ground

        internal static readonly Color DirtDark = new Color(0.42f, 0.32f, 0.21f), DirtLight = new Color(0.6f, 0.49f, 0.34f),
            Pebble = new Color(0.6f, 0.57f, 0.51f), PebbleDark = new Color(0.36f, 0.32f, 0.27f);

        static bool IsDirt(Terrain t) => t == Terrain.Path || t == Terrain.Camp;

        /// The dirt skin for a chunk, over its ground mesh (null when there's no dirt near it).
        public static void Build(World world, Mesh ground, int x0, int z0, int size, Transform root)
        {
            var ow = world.Overworld;
            // Which tiles round the chunk are dirt (and which kind), out to Reach beyond its edges.
            int span = size + Reach * 2;
            var dirt = new byte[span, span];  // 0: none, 1: road, 2: camp
            bool any = false;
            for (int i = 0; i < span; i++)
                for (int j = 0; j < span; j++)
                {
                    var t = ow.At(new Tile(x0 - Reach + i, z0 - Reach + j));
                    if (IsDirt(t))
                    {
                        dirt[i, j] = (byte)(t == Terrain.Camp ? 2 : 1);
                        any = true;
                    }
                }
            if (!any)
                return;

            // The chunk's tiles the skin covers: those within Reach of some dirt.
            var covered = new bool[size, size];
            bool anyCovered = false;
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                    for (int a = 0; a <= Reach * 2 && !covered[i, j]; a++)
                        for (int b = 0; b <= Reach * 2; b++)
                            if (dirt[i + a, j + b] != 0)
                            {
                                covered[i, j] = anyCovered = true;
                                break;
                            }
            if (!anyCovered)
                return;

            // Each tile's own ground colour, as the ground draws it (its material's colour times its texture's).
            var verts = ground.vertices;
            int n = size + 1;
            var groundColour = new Color[size, size];
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                {
                    var tile = new Tile(x0 + i, z0 + j);
                    int v = i * n + j;
                    var bare = ow.GroundAt(tile, verts[v].y, verts[v + n].y, verts[v + 1].y, verts[v + n + 1].y);
                    var c = bare != Core.Ground.Grass ? World.BareColour(bare) : World.GroundColour(ow.BiomeAt(tile), World.DangerBand(ow.Danger(tile)));
                    groundColour[i, j] = c * (1.07f * 0.93f);
                }

            // The skin: the ground's own grid of vertices, lifted a little, with the covered tiles' triangles.
            var lifted = new Vector3[verts.Length];
            for (int k = 0; k < verts.Length; k++)
                lifted[k] = verts[k] + new Vector3(0f, Lift, 0f);
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                {
                    if (!covered[i, j])
                        continue;
                    int a = i * n + j, b = a + 1, c = a + n, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(c); tris.Add(b); tris.Add(d);
                }
            var mesh = new Mesh { name = "Dirt" };
            mesh.vertices = lifted;
            mesh.normals = ground.normals;
            mesh.uv = ground.uv;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            var mat = Visuals.NewMat(Color.white);
            var go = new GameObject("Dirt");
            go.layer = World.FarLayer;
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.enabled = false;  // until its texture's painted
            var owned = go.AddComponent<OwnedMesh>();
            owned.Mesh = mesh;
            owned.Material = mat;
            go.AddComponent<DirtPainter>().Begin(dirt, groundColour, covered, x0, z0, size, renderer, owned);
        }

        internal static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        // Value noise on world coordinates (the same across chunk edges).
        internal static float Hash(int x, int z)
        {
            uint h = (uint)(x * 374761393 + z * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xffffff) / 16777216f;
        }

        internal static float Noise(float x, float z)
        {
            int xi = Mathf.FloorToInt(x), zi = Mathf.FloorToInt(z);
            float fx = x - xi, fz = z - zi;
            fx = fx * fx * (3f - 2f * fx);
            fz = fz * fz * (3f - 2f * fz);
            float a = Hash(xi, zi), b = Hash(xi + 1, zi), c = Hash(xi, zi + 1), d = Hash(xi + 1, zi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
        }

        /// Two octaves of value noise.
        internal static float Fbm2(float x, float z) => (Noise(x, z) * 2f + Noise(x * 2.03f + 17.1f, z * 2.03f - 9.7f)) / 3f;
    }

    /// Paints a chunk's dirt texture (DirtPaths) a few rows a frame, sharing a small budget a frame with every
    /// other chunk's, then shows the skin and goes.
    public sealed class DirtPainter : MonoBehaviour
    {
        const double BudgetMillis = 3.0;  // a frame, for all the chunks' painting together
        static int budgetFrame = -1;
        static double spent;

        byte[,] dirt;
        Color[,] groundColour;
        bool[,] covered;
        System.Collections.Generic.List<int>[,] near;
        int x0, z0, size, w, row;
        Color32[] px;
        MeshRenderer target;
        OwnedMesh owned;

        public void Begin(byte[,] dirt, Color[,] groundColour, bool[,] covered, int x0, int z0, int size, MeshRenderer target, OwnedMesh owned)
        {
            this.dirt = dirt;
            this.groundColour = groundColour;
            this.covered = covered;
            this.x0 = x0;
            this.z0 = z0;
            this.size = size;
            this.target = target;
            this.owned = owned;
            const int Px = DirtPaths.Px, Reach = DirtPaths.Reach;
            w = size * Px;
            px = new Color32[w * w];
            // For each tile, the dirt tiles near it (offsets), so each pixel only looks at those.
            near = new System.Collections.Generic.List<int>[size, size];
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                {
                    if (!covered[i, j])
                        continue;
                    var list = near[i, j] = new System.Collections.Generic.List<int>();
                    for (int a = -Reach; a <= Reach; a++)
                        for (int b = -Reach; b <= Reach; b++)
                            if (dirt[i + Reach + a, j + Reach + b] != 0)
                                list.Add((a + Reach) * 8 + (b + Reach));
                }
        }

        void Update()
        {
            if (Time.frameCount != budgetFrame)
            {
                budgetFrame = Time.frameCount;
                spent = 0;
            }
            if (spent >= BudgetMillis)
                return;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (row < w && spent + clock.Elapsed.TotalMilliseconds < BudgetMillis)
                PaintRow(row++);
            spent += clock.Elapsed.TotalMilliseconds;
            if (row < w)
                return;
            var tex = new Texture2D(w, w, TextureFormat.RGBA32, true);
            tex.SetPixels32(px);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 4;
            tex.Apply();
            owned.Texture = tex;
            target.sharedMaterial.mainTexture = tex;
            target.enabled = true;
            Destroy(this);
        }

        /// Paints the whole texture at once (for tests and previews).
        public Color32[] PaintAll()
        {
            while (row < w)
                PaintRow(row++);
            return px;
        }

        void PaintRow(int py)
        {
            const int Px = DirtPaths.Px, Reach = DirtPaths.Reach;
            for (int pxx = 0; pxx < w; pxx++)
            {
                int i = pxx / Px, j = py / Px;
                if (!covered[i, j])
                    continue;  // never drawn
                // Where this pixel is on the map (tile centres are whole numbers; a tile runs from -0.5 to +0.5).
                float x = x0 - 0.5f + (pxx + 0.5f) / Px, z = z0 - 0.5f + (py + 0.5f) / Px;

                // How far it is from the nearest dirt tile's edge, and what kind of dirt that is.
                int kind = dirt[i + Reach, j + Reach];
                float dist = 0f;
                if (kind == 0)
                {
                    float best = 99f;
                    foreach (int o in near[i, j])
                    {
                        int a = o / 8 - Reach, b = o % 8 - Reach;
                        float dx = Mathf.Max(Mathf.Abs(x - (x0 + i + a)) - 0.5f, 0f), dz = Mathf.Max(Mathf.Abs(z - (z0 + j + b)) - 0.5f, 0f);
                        float d = dx * dx + dz * dz;
                        if (d < best)
                        {
                            best = d;
                            kind = dirt[i + Reach + a, j + Reach + b];
                        }
                    }
                    dist = Mathf.Sqrt(best);
                }

                var c = groundColour[i, j];
                if (dist < 1.9f)
                {
                    // The edge wanders in and out of the grass (noise), and frays finely right at it.
                    float e = dist + (DirtPaths.Fbm2(x * 0.9f, z * 0.9f) - 0.5f) * 0.9f;
                    // Worn grass: paler and yellower, out to about a tile and a half.
                    float wear = 1f - DirtPaths.Smooth(-0.2f, 1.5f, e);
                    c = Color.Lerp(c, new Color(c.r * 0.9f + 0.1f, c.g * 0.82f + 0.08f, c.b * 0.7f + 0.03f), wear * 0.75f);
                    float amount = 1f - DirtPaths.Smooth(-0.15f, 0.75f, e);
                    if (amount > 0f)
                    {
                        float grain = DirtPaths.Noise(x * 9f, z * 9f) - 0.5f;
                        amount = Mathf.Clamp01(amount + grain * 1.4f * amount * (1f - amount) * 4f);
                        // The earth: light and dark patches, a few pebbles, darker where it's thin (trodden into the grass).
                        var earth = Color.Lerp(DirtPaths.DirtDark, DirtPaths.DirtLight, Mathf.Clamp01(DirtPaths.Noise(x * 2.2f + 31.7f, z * 2.2f - 12.3f) * 1.4f - 0.2f));
                        earth *= 0.92f + (DirtPaths.Hash(Mathf.FloorToInt(x * 16f), Mathf.FloorToInt(z * 16f)) - 0.5f) * 0.12f;
                        if (kind == 2)
                            earth *= 0.88f;  // trampled camp ground: a little darker
                        float pebble = DirtPaths.Hash(Mathf.FloorToInt(x * 8f) + 911, Mathf.FloorToInt(z * 8f) - 377);
                        if (pebble > 0.972f && amount > 0.7f)
                            earth = pebble > 0.99f ? DirtPaths.Pebble : DirtPaths.PebbleDark;
                        earth = Color.Lerp(earth * 0.85f, earth, DirtPaths.Smooth(0.3f, 0.85f, amount));
                        c = Color.Lerp(c, earth, amount);
                    }
                }
                // The ground's texture: a speckle, and a faint line round each tile (fainter on the dirt).
                c *= 1f + (DirtPaths.Hash(pxx * 7 + 3, py * 13 + 5) - 0.5f) * 0.05f;
                if (pxx % Px == 0 || py % Px == 0)
                    c *= kind != 0 && dist < 0.3f ? 0.95f : 0.8f / 0.93f;  // (the ground texture's line against its fill)
                px[py * w + pxx] = c;
            }
        }
    }
}
