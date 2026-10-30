using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// How a chunk's ground is coloured, blended smoothly rather than tile by tile: at every corner of its tiles,
    /// the biome's grass tint (averaged over the tiles round the corner) and how much earth, rock and snow covers it
    /// (Overworld.GroundWeights). The Ground shader blends the four textures between the corners, letting each
    /// texture's own light and dark break up the edges (a rock's cracks show first). ColourAt works out the very
    /// same colour on the CPU, so DirtPaths' painted skin meets the ground without a seam.
    public sealed class GroundBlend
    {
        public const float Detail = 0.5f, Edge = 0.2f;  // how much each texture's brightness pushes it through; how soft the edges

        public readonly int X0, Z0, Size, N;
        public readonly float[] Heights;
        public readonly Color[] Tint;         // per corner: the grass's colour there
        public readonly Vector2[] EarthRock;  // per corner: how much earth (x) and rock (y)
        public readonly Vector2[] Snow;       // per corner: how much snow (x)

        public static readonly Color EarthTint = World.GroundTint(Core.Ground.Earth, Biome.Meadow, 0);
        public static readonly Color RockTint = World.GroundTint(Core.Ground.Rock, Biome.Meadow, 0);
        public static readonly Color SnowTint = World.GroundTint(Core.Ground.Snow, Biome.Meadow, 0);

        public GroundBlend(Overworld ow, int x0, int z0, int size, Vector3[] verts)
        {
            X0 = x0;
            Z0 = z0;
            Size = size;
            N = size + 1;
            Heights = new float[N * N];
            Tint = new Color[N * N];
            EarthRock = new Vector2[N * N];
            Snow = new Vector2[N * N];
            // The grass colour of the tiles round the chunk's corners (one tile out all round).
            var tiles = new Color[size + 2, size + 2];
            for (int a = 0; a < size + 2; a++)
                for (int b = 0; b < size + 2; b++)
                {
                    var t = new Tile(x0 - 1 + a, z0 - 1 + b);
                    tiles[a, b] = World.GroundTint(Core.Ground.Grass, ow.BiomeAt(t), World.DangerBand(ow.Danger(t)));
                }
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N; j++)
                {
                    int k = i * N + j;
                    var v = verts[k];
                    Heights[k] = v.y;
                    Tint[k] = (tiles[i, j] + tiles[i + 1, j] + tiles[i, j + 1] + tiles[i + 1, j + 1]) * 0.25f;
                    ow.GroundWeights(v.x, v.z, out double earth, out double rock, out double snow);
                    EarthRock[k] = new Vector2((float)earth, (float)rock);
                    Snow[k] = new Vector2((float)snow, 0f);
                }
        }

        /// The ground's colour at a pixel of a texture laid over the chunk at `px` pixels a tile (pixel 0, 0 at the
        /// chunk's first corner), as the Ground shader draws it. The ground textures repeat every TexSize texels.
        public Color ColourAt(int pxx, int py, int px)
        {
            float u = (pxx + 0.5f) / px, v = (py + 0.5f) / px;
            int i = Mathf.Min((int)u, Size - 1), j = Mathf.Min((int)v, Size - 1);
            float fu = u - i, fv = v - j;
            int a = i * N + j, b = a + 1, c = a + N, d = c + 1;
            // Across the tile's two triangles (a, b, c) and (c, b, d), as the graphics card blends between corners.
            Color tint;
            Vector2 er, sn;
            if (fu + fv <= 1f)
            {
                tint = Tint[a] + (Tint[c] - Tint[a]) * fu + (Tint[b] - Tint[a]) * fv;
                er = EarthRock[a] + (EarthRock[c] - EarthRock[a]) * fu + (EarthRock[b] - EarthRock[a]) * fv;
                sn = Snow[a] + (Snow[c] - Snow[a]) * fu + (Snow[b] - Snow[a]) * fv;
            }
            else
            {
                tint = Tint[d] + (Tint[b] - Tint[d]) * (1f - fu) + (Tint[c] - Tint[d]) * (1f - fv);
                er = EarthRock[d] + (EarthRock[b] - EarthRock[d]) * (1f - fu) + (EarthRock[c] - EarthRock[d]) * (1f - fv);
                sn = Snow[d] + (Snow[b] - Snow[d]) * (1f - fu) + (Snow[c] - Snow[d]) * (1f - fv);
            }
            const int T = DirtPaths.TexSize;
            int texel = (py % T) * T + pxx % T;
            return Blend(tint, er.x, er.y, sn.x, Texels(0)[texel], Texels(1)[texel], Texels(2)[texel], Texels(3)[texel]);
        }

        /// The four textures blended (the Ground shader does the same): each texture's weight lifted by its own
        /// brightness, and only those near the top kept, so edges follow the textures' detail.
        public static Color Blend(Color tint, float earth, float rock, float snow, Color32 g, Color32 e, Color32 r, Color32 s)
        {
            float grass = Mathf.Clamp01(1f - earth - rock - snow);
            float hg = grass + g.r / 255f * Detail, he = earth + e.r / 255f * Detail, hr = rock + r.r / 255f * Detail, hs = snow + s.r / 255f * Detail;
            float top = Mathf.Max(Mathf.Max(hg, he), Mathf.Max(hr, hs)) - Edge;
            float bg = Mathf.Max(hg - top, 0f), be = Mathf.Max(he - top, 0f), br = Mathf.Max(hr - top, 0f), bs = Mathf.Max(hs - top, 0f);
            float sum = bg + be + br + bs;
            bg /= sum; be /= sum; br /= sum; bs /= sum;
            return new Color(
                bg * g.r / 255f * tint.r + be * e.r / 255f * EarthTint.r + br * r.r / 255f * RockTint.r + bs * s.r / 255f * SnowTint.r,
                bg * g.g / 255f * tint.g + be * e.g / 255f * EarthTint.g + br * r.g / 255f * RockTint.g + bs * s.g / 255f * SnowTint.g,
                bg * g.b / 255f * tint.b + be * e.b / 255f * EarthTint.b + br * r.b / 255f * RockTint.b + bs * s.b / 255f * SnowTint.b);
        }

        static readonly Color32[][] texels = new Color32[4][];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            for (int k = 0; k < texels.Length; k++)
                texels[k] = null;
        }

        /// The ground textures' texels: grass, earth, rock, snow.
        static Color32[] Texels(int k)
        {
            if (texels[k] == null)
            {
                var tex = k == 0 ? Visuals.GrassTexture() : k == 1 ? Visuals.EarthTexture() : k == 2 ? Visuals.RockTexture() : Visuals.SnowTexture();
                texels[k] = tex.GetPixels32();
            }
            return texels[k];
        }
    }
}
