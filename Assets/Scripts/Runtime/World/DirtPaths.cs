using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Dirt roads, town squares and the trampled ground of camps and cave mouths, drawn as a skin over the
    /// ground with a texture painted for each chunk: mottled brown earth with pebbles, its edge ragged and
    /// fading through worn grass into the ground round it. Away from the dirt the skin is painted with the
    /// ground's own texture, texel for texel (at the same resolution and tint), so it meets the ground
    /// without a seam; its normals are worked out as the ground's are, so it's lit the same.
    /// The texture is painted a few rows at a time over the next frames (DirtPainter), so streaming chunks
    /// in doesn't stutter; the skin shows once it's done.
    public static class DirtPaths
    {
        internal const int Px = 32;         // texture pixels per tile (as the ground's textures have)
        internal const int Reach = 2;       // tiles round the dirt the skin covers (the fade is well inside this)
        const float Lift = 0.012f; // above the ground

        internal static readonly Color DirtDark = new Color(0.42f, 0.32f, 0.21f), DirtLight = new Color(0.6f, 0.49f, 0.34f),
            Pebble = new Color(0.6f, 0.57f, 0.51f), PebbleDark = new Color(0.36f, 0.32f, 0.27f);

        static bool IsDirt(Terrain t) => t == Terrain.Path || t == Terrain.Camp;

        /// The dirt skin for a chunk, over its ground (`heights`: the ground's corner heights, as Ground makes them).
        public static void Build(World world, float[] heights, int x0, int z0, int size, Transform root)
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

            // What the ground is on each tile, as the ground draws it: its material's tint, and its texture.
            int n = size + 1;
            var tint = new Color[size, size];
            var texture = new Color32[size, size][];
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                {
                    var tile = new Tile(x0 + i, z0 + j);
                    int v = i * n + j;
                    var bare = ow.GroundAt(tile, heights[v], heights[v + n], heights[v + 1], heights[v + n + 1]);
                    tint[i, j] = World.GroundTint(bare, ow.BiomeAt(tile), World.DangerBand(ow.Danger(tile)));
                    texture[i, j] = Texels(bare);
                }

            // The skin: the ground's own grid of corners, lifted a little, with the covered tiles' triangles. Its
            // normals come from the whole grid first (as the ground's do), then it keeps only those triangles.
            var verts = new Vector3[n * n];
            var uv = new Vector2[n * n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    verts[i * n + j] = new Vector3(x0 - 0.5f + i, heights[i * n + j] + Lift, z0 - 0.5f + j);
                    uv[i * n + j] = new Vector2(i / (float)size, j / (float)size);
                }
            var all = new System.Collections.Generic.List<int>();
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < size; i++)
                for (int j = 0; j < size; j++)
                {
                    int a = i * n + j, b = a + 1, c = a + n, d = c + 1;
                    all.Add(a); all.Add(b); all.Add(c);
                    all.Add(c); all.Add(b); all.Add(d);
                    if (!covered[i, j])
                        continue;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(c); tris.Add(b); tris.Add(d);
                }
            var mesh = new Mesh { name = "Dirt" };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.SetTriangles(all, 0);
            mesh.RecalculateNormals();
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
            go.AddComponent<DirtPainter>().Begin(dirt, tint, texture, Texels(Core.Ground.Earth), covered, x0, z0, size, renderer, owned);
        }

        /// The texels of the texture the ground wears for this kind of ground (made by script, so readable).
        static Color32[] Texels(Core.Ground ground)
        {
            int k = (int)ground;
            if (texels[k] == null)
            {
                var tex = ground == Core.Ground.Grass ? Visuals.GrassTexture() : ground == Core.Ground.Earth ? Visuals.EarthTexture()
                    : ground == Core.Ground.Rock ? Visuals.RockTexture() : Visuals.SnowTexture();
                texels[k] = tex.GetPixels32();
            }
            return texels[k];
        }

        static readonly Color32[][] texels = new Color32[4][];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            for (int k = 0; k < texels.Length; k++)
                texels[k] = null;
        }

        /// The ground textures' size in texels (each repeats every Visuals.TextureTiles tiles).
        internal const int TexSize = 128;

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
        const double BudgetMillis = 4.0;  // a frame, for all the chunks' painting together
        static int budgetFrame = -1;
        static double spent;

        byte[,] dirt;
        Color[,] tint;
        Color32[,][] texture;
        Color32[] earth;
        bool[,] covered;
        int x0, z0, size, w, row;
        Color32[] px;
        MeshRenderer target;
        OwnedMesh owned;

        public void Begin(byte[,] dirt, Color[,] tint, Color32[,][] texture, Color32[] earth, bool[,] covered, int x0, int z0, int size,
            MeshRenderer target, OwnedMesh owned)
        {
            this.dirt = dirt;
            this.tint = tint;
            this.texture = texture;
            this.earth = earth;
            this.covered = covered;
            this.x0 = x0;
            this.z0 = z0;
            this.size = size;
            this.target = target;
            this.owned = owned;
            w = size * DirtPaths.Px;
            px = new Color32[w * w];
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

        // The smooth parts (how far into the grass, and the earth's light and dark patches) are worked out on a
        // grid of nodes every Step pixels and blended between, row by row as the painting reaches them; only the
        // fine grain is worked out for every pixel.
        const int Step = 4;
        float[] edge, mottle;   // per node: the edge value (distance into the grass, with the wander), the patches
        byte[] nodeKind;        // per node: the kind of dirt nearest
        int nodes, nodeRows;    // nodes along a row; how many rows are done

        void NodeRow(int r)
        {
            const int Px = DirtPaths.Px, Reach = DirtPaths.Reach;
            if (edge == null)
            {
                nodes = w / Step + 1;
                edge = new float[nodes * nodes];
                mottle = new float[nodes * nodes];
                nodeKind = new byte[nodes * nodes];
            }
            float z = z0 - 0.5f + r * Step / (float)Px;
            int j = Mathf.Min(r * Step / Px, size - 1);
            for (int q = 0; q < nodes; q++)
            {
                float x = x0 - 0.5f + q * Step / (float)Px;
                int i = Mathf.Min(q * Step / Px, size - 1);
                int k = r * nodes + q;
                if (!covered[i, j] && !covered[Mathf.Max(i - 1, 0), j] && !covered[i, Mathf.Max(j - 1, 0)] && !covered[Mathf.Max(i - 1, 0), Mathf.Max(j - 1, 0)])
                {
                    edge[k] = 9f;
                    continue;
                }
                // How far it is from the nearest dirt tile's edge (round its tile, and the tiles a node on a corner touches).
                float best = 99f;
                byte kind = 1;
                for (int a = -Reach - 1; a <= Reach; a++)
                    for (int b = -Reach - 1; b <= Reach; b++)
                    {
                        int di = i + Reach + a, dj = j + Reach + b;
                        if (di < 0 || dj < 0 || di >= size + Reach * 2 || dj >= size + Reach * 2 || dirt[di, dj] == 0)
                            continue;
                        float dx = Mathf.Max(Mathf.Abs(x - (x0 + i + a)) - 0.5f, 0f), dz = Mathf.Max(Mathf.Abs(z - (z0 + j + b)) - 0.5f, 0f);
                        float d = dx * dx + dz * dz;
                        if (d < best)
                        {
                            best = d;
                            kind = dirt[di, dj];
                        }
                    }
                // The edge wanders in and out of the grass (noise).
                edge[k] = Mathf.Sqrt(best) + (DirtPaths.Fbm2(x * 0.9f, z * 0.9f) - 0.5f) * 0.9f;
                mottle[k] = DirtPaths.Noise(x * 2.2f + 31.7f, z * 2.2f - 12.3f);
                nodeKind[k] = kind;
            }
        }

        void PaintRow(int py)
        {
            const int Px = DirtPaths.Px, T = DirtPaths.TexSize;
            int r0 = py / Step;
            while (nodeRows <= r0 + 1)
                NodeRow(nodeRows++);
            float fz = (py % Step) / (float)Step;
            int ty = py % T;  // the ground's texture repeats every T texels, lined up with the chunk (16 tiles is 4 repeats)
            for (int pxx = 0; pxx < w; pxx++)
            {
                int i = pxx / Px, j = py / Px;
                if (!covered[i, j])
                    continue;  // never drawn

                // The ground here, as the ground draws it.
                int texel = ty * T + pxx % T;
                var g = texture[i, j][texel];
                var tn = tint[i, j];
                var c = new Color(tn.r * g.r / 255f, tn.g * g.g / 255f, tn.b * g.b / 255f);

                int q0 = pxx / Step, k = r0 * nodes + q0;
                float fx = (pxx % Step) / (float)Step;
                float e = Mathf.Lerp(Mathf.Lerp(edge[k], edge[k + 1], fx), Mathf.Lerp(edge[k + nodes], edge[k + nodes + 1], fx), fz);
                if (e < 1.5f)
                {
                    // Worn grass: paler and yellower, out to about a tile and a half.
                    float wear = 1f - DirtPaths.Smooth(-0.2f, 1.5f, e);
                    c = Color.Lerp(c, new Color(c.r * 0.9f + 0.1f, c.g * 0.82f + 0.08f, c.b * 0.7f + 0.03f), wear * 0.75f);
                    float amount = 1f - DirtPaths.Smooth(-0.15f, 0.75f, e);
                    if (amount > 0f)
                    {
                        // Where this pixel is on the map (tile centres are whole numbers; a tile runs from -0.5 to +0.5).
                        int wx = (x0 * Px + pxx), wz = (z0 * Px + py);
                        // Frayed finely right at the edge.
                        float grain = DirtPaths.Hash(wx >> 1, wz >> 1) - 0.5f;
                        amount = Mathf.Clamp01(amount + grain * 1.4f * amount * (1f - amount) * 4f);
                        // The earth: light and dark patches, a few pebbles, darker where it's thin (trodden into the grass).
                        float m = Mathf.Lerp(Mathf.Lerp(mottle[k], mottle[k + 1], fx), Mathf.Lerp(mottle[k + nodes], mottle[k + nodes + 1], fx), fz);
                        var soil = Color.Lerp(DirtPaths.DirtDark, DirtPaths.DirtLight, Mathf.Clamp01(m * 1.4f - 0.2f));
                        soil *= earth[texel].r / 255f * 1.15f;  // the grain of the ground's earth texture
                        if (nodeKind[k] == 2)
                            soil *= 0.88f;  // trampled camp ground: a little darker
                        float pebble = DirtPaths.Hash((wx / 3) + 911, (wz / 3) - 377);
                        if (pebble > 0.975f && amount > 0.7f)
                            soil = pebble > 0.99f ? DirtPaths.Pebble : DirtPaths.PebbleDark;
                        soil = Color.Lerp(soil * 0.85f, soil, DirtPaths.Smooth(0.3f, 0.85f, amount));
                        c = Color.Lerp(c, soil, amount);
                    }
                }
                px[py * w + pxx] = c;
            }
        }
    }
}
