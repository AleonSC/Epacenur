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
    }
}
