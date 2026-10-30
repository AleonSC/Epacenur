using System.Collections.Generic;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// There are no art assets: everything is built from Unity's primitive shapes and flat colours.
    public static class Visuals
    {
        static Material baseMaterial;
        static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            baseMaterial = null;
            cache.Clear();
        }

        /// A matte material of one colour (shared by everything of that colour).
        public static Material Mat(Color c)
        {
            if (cache.TryGetValue(c, out var m) && m != null)
                return m;
            // Instanced: the many identical trees, walls and so on draw in a few batches.
            m = new Material(BaseMaterial()) { color = c, enableInstancing = true };
            if (m.HasProperty("_Glossiness"))
                m.SetFloat("_Glossiness", 0.08f);
            cache[c] = m;
            return m;
        }

        /// A fresh material (not shared), e.g. for a textured ground.
        public static Material NewMat(Color c)
        {
            var m = new Material(BaseMaterial()) { color = c };
            if (m.HasProperty("_Glossiness"))
                m.SetFloat("_Glossiness", 0.05f);
            return m;
        }

        // Copies the default material primitives use, so this works in any render pipeline the
        // project is set to, and the shader is always included in builds.
        static Material BaseMaterial()
        {
            if (baseMaterial == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseMaterial = new Material(probe.GetComponent<Renderer>().sharedMaterial);
                Object.Destroy(probe);
            }
            return baseMaterial;
        }

        /// A primitive shape without a collider (unless asked for), parented and coloured.
        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color c, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!keepCollider)
                Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(c);
            return go;
        }

        public static Color Rgb(float r, float g, float b) => new Color(r, g, b);

        /// A grass texture with a faint line round each tile, so the grid is visible.
        public static Texture2D TileTexture(Color fill, Color line)
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color[size * size];
            var rng = new System.Random(7);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool edge = x == 0 || y == 0;
                    float n = (float)rng.NextDouble() * 0.05f - 0.025f;
                    var c = edge ? line : fill;
                    px[y * size + x] = new Color(c.r + n, c.g + n, c.b + n, 1f);
                }
            tex.SetPixels(px);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 4;
            tex.Apply();
            return tex;
        }

        /// The textures the overworld's ground wears (pale, so each material's colour shows through), tiling
        /// seamlessly, each covering TextureTiles tiles. Made once.
        public const float TextureTiles = 4f;
        static Texture2D grassTex, earthTex, rockTex, snowTex;

        public static Texture2D GrassTexture() => grassTex ?? (grassTex = GroundTexture(11, 0.82f, 0.16f, 900, 0.72f, 1.12f, 2, 5));
        public static Texture2D EarthTexture() => earthTex ?? (earthTex = GroundTexture(12, 0.85f, 0.14f, 500, 0.7f, 1.12f, 1, 2));
        public static Texture2D RockTexture() => rockTex ?? (rockTex = GroundTexture(13, 0.8f, 0.22f, 220, 0.62f, 1.1f, 1, 7));
        public static Texture2D SnowTexture() => snowTex ?? (snowTex = GroundTexture(14, 0.95f, 0.05f, 120, 0.9f, 1.04f, 1, 2));

        /// A seamless ground texture: soft tileable blotches of light and shade (`bright` +- `blotch`), scattered
        /// with `strokes` short strokes (blades of grass, grit, cracks) of `dark` and `light` shades, `minLen`-`maxLen` long.
        static Texture2D GroundTexture(int seed, float bright, float blotch, int strokes, float dark, float light, int minLen, int maxLen)
        {
            const int size = 128, cells = 8;
            var rng = new System.Random(seed);
            var lattice = new float[cells, cells];
            for (int i = 0; i < cells; i++)
                for (int j = 0; j < cells; j++)
                    lattice[i, j] = (float)rng.NextDouble();
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Value noise on a lattice that wraps round, so the texture tiles.
                    float gx = x * cells / (float)size, gy = y * cells / (float)size;
                    int x0 = (int)gx, y0 = (int)gy;
                    float fx = gx - x0, fy = gy - y0;
                    fx = fx * fx * (3 - 2 * fx);
                    fy = fy * fy * (3 - 2 * fy);
                    float a = lattice[x0 % cells, y0 % cells], b = lattice[(x0 + 1) % cells, y0 % cells];
                    float c = lattice[x0 % cells, (y0 + 1) % cells], d = lattice[(x0 + 1) % cells, (y0 + 1) % cells];
                    float n = Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
                    float v = bright + (n - 0.5f) * 2f * blotch + ((float)rng.NextDouble() - 0.5f) * 0.05f;
                    px[y * size + x] = new Color(v, v, v, 1f);
                }
            for (int s = 0; s < strokes; s++)
            {
                int x = rng.Next(size), y = rng.Next(size), len = minLen + rng.Next(maxLen - minLen + 1);
                int dx = rng.Next(3) - 1;
                float shade = rng.NextDouble() < 0.6 ? dark : light;
                for (int k = 0; k < len; k++)
                {
                    int sx = ((x + (k * dx) / 2) % size + size) % size, sy = (y + k) % size;
                    var p = px[sy * size + sx];
                    float v = p.r * shade;
                    px[sy * size + sx] = new Color(v, v, v, 1f);
                }
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.SetPixels(px);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 4;
            tex.Apply();
            return tex;
        }
    }
}
