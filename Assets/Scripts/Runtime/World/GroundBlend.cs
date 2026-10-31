using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// How a chunk's ground is coloured, blended smoothly rather than tile by tile: at every corner of its tiles,
    /// the biome's grass tint (averaged over the tiles round the corner) and how much earth, rock and snow covers it
    /// (Overworld.GroundWeights). The Ground shader blends the four textures between the corners, letting each
    /// texture's own light and dark break up the edges (a rock's cracks show first).
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
    }
}
