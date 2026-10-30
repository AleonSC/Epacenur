using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Grass, flowers and bushes over a chunk's open ground, by biome: tufts of grass blades everywhere grass
    /// grows (tall reeds in marshes, dry and sparse in the wasteland), flowers in clumps (most in meadows), and the
    /// odd bush (most in woodland). All of a chunk's grass and flowers are one mesh, its bushes another; you walk
    /// through them. They're drawn only fairly near (World.DetailLayer), and the grass casts no shadows.
    public static class Foliage
    {
        static readonly Color[] FlowerColours =
        {
            new Color(0.98f, 0.85f, 0.2f), new Color(0.95f, 0.95f, 0.92f), new Color(0.7f, 0.4f, 0.9f),
            new Color(0.95f, 0.3f, 0.3f), new Color(0.4f, 0.55f, 0.95f), new Color(1f, 0.6f, 0.75f),
        };

        sealed class Builder
        {
            public readonly List<Vector3> Verts = new List<Vector3>();
            public readonly List<Vector2> Free = new List<Vector2>();  // y: how freely each vertex sways (0 rooted, 1 a tip)
            public readonly Dictionary<Color, List<int>> Tris = new Dictionary<Color, List<int>>();

            /// A thin two-sided triangle from a base (with a half-width across `side`) up to a tip.
            public void Blade(Vector3 a, Vector3 side, Vector3 tip, Color c, float footFree = 0f)
            {
                if (!Tris.TryGetValue(c, out var t))
                    Tris[c] = t = new List<int>();
                int i = Verts.Count;
                Verts.Add(a - side);
                Verts.Add(a + side);
                Verts.Add(tip);
                Free.Add(new Vector2(0f, footFree));
                Free.Add(new Vector2(1f, footFree));
                Free.Add(new Vector2(0.5f, 1f));
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                t.Add(i + 1); t.Add(i); t.Add(i + 2);
            }

            /// A little four-pointed flower head facing up, two-sided.
            public void Head(Vector3 at, float r, Color c)
            {
                Blade(at + new Vector3(0f, 0f, -r), new Vector3(r, 0f, 0f), at + new Vector3(0f, 0.01f, r), c, 1f);
                Blade(at + new Vector3(-r, 0f, 0f), new Vector3(0f, 0f, r), at + new Vector3(r, 0.01f, 0f), c, 1f);
            }
        }

        const float GrassSway = 0.2f, BushSway = 0.04f;  // how far the wind bends them (tiles, in a full gale)

        public static void Build(Overworld ow, Terrain[,] terrain, int x0, int z0, float[] heights, Transform root, System.Random rng)
        {
            int size = terrain.GetLength(0), n = size + 1;
            var grass = new Builder();
            var bushes = new Dictionary<Color, List<CombineInstance>>();
            var sphere = SphereMesh();
            for (int x = 0; x < size; x++)
                for (int z = 0; z < size; z++)
                {
                    var t = new Tile(x0 + x, z0 + z);
                    if (terrain[x, z] != Terrain.Grass)
                        continue;
                    var plan = ow.PlanAt(t);
                    if (plan.Blocked.Contains(t))
                        continue;
                    int k = x * n + z;
                    var ground = ow.GroundAt(t, heights[k], heights[k + n], heights[k + 1], heights[k + n + 1]);
                    if (ground == Core.Ground.Rock || ground == Core.Ground.Snow)
                        continue;
                    var biome = ow.BiomeAt(t);
                    bool town = plan.Kind == RegionKind.Town, levelled = plan.KeepsClear(t);
                    var baseColour = World.GroundColour(biome, World.DangerBand(ow.Danger(t)));
                    Color light = baseColour * 1.35f, dark = baseColour * 0.9f;
                    if (biome == Biome.Wasteland)
                        light = new Color(0.62f, 0.55f, 0.36f);

                    int tufts = ground == Core.Ground.Earth ? rng.Next(2) : biome == Biome.Meadow ? 3 : biome == Biome.Marsh ? 4
                        : biome == Biome.Wasteland ? 1 : 2;
                    float tall = biome == Biome.Marsh ? 0.7f : biome == Biome.Meadow ? 0.38f : biome == Biome.Woodland ? 0.32f : 0.24f;
                    if (town)
                        tall *= 0.7f;  // kept short in town
                    for (int i = 0; i < tufts; i++)
                    {
                        float px = t.X + (float)rng.NextDouble() - 0.5f, pz = t.Z + (float)rng.NextDouble() - 0.5f;
                        var at = new Vector3(px, HeightAt(heights, n, x0, z0, px, pz), pz);
                        int blades = 3 + rng.Next(3);
                        for (int b = 0; b < blades; b++)
                        {
                            float ang = (float)rng.NextDouble() * Mathf.PI * 2f, off = (float)rng.NextDouble() * 0.09f;
                            var foot = at + new Vector3(Mathf.Cos(ang) * off, -0.02f, Mathf.Sin(ang) * off);
                            float h = tall * (0.6f + (float)rng.NextDouble() * 0.6f);
                            var lean = new Vector3(Mathf.Cos(ang) * h * 0.35f, h, Mathf.Sin(ang) * h * 0.35f);
                            var side = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * (biome == Biome.Marsh ? 0.025f : 0.035f);
                            grass.Blade(foot, side, foot + lean, rng.NextDouble() < 0.5 ? light : dark);
                        }
                    }

                    // Flowers, in clumps of one colour.
                    double flowerChance = biome == Biome.Meadow ? 0.3 : biome == Biome.Highlands ? 0.12 : biome == Biome.Marsh ? 0.1
                        : biome == Biome.Woodland ? 0.08 : 0;
                    if (ground == Core.Ground.Grass && rng.NextDouble() < flowerChance)
                    {
                        var colour = biome == Biome.Highlands ? FlowerColours[2] : biome == Biome.Marsh ? FlowerColours[rng.Next(2)]
                            : FlowerColours[rng.Next(FlowerColours.Length)];
                        int count = 3 + rng.Next(3);
                        for (int i = 0; i < count; i++)
                        {
                            float px = t.X + (float)rng.NextDouble() * 0.8f - 0.4f, pz = t.Z + (float)rng.NextDouble() * 0.8f - 0.4f;
                            var foot = new Vector3(px, HeightAt(heights, n, x0, z0, px, pz) - 0.02f, pz);
                            float h = 0.22f + (float)rng.NextDouble() * 0.18f;
                            var head = foot + new Vector3(0f, h, 0f);
                            grass.Blade(foot, new Vector3(0.012f, 0f, 0f), head, dark);
                            grass.Head(head, 0.055f, colour);
                        }
                    }

                    // The odd bush, out in the country: a dome of leafy clumps, darker underneath and lighter on top;
                    // some in berry or in flower.
                    double bushChance = biome == Biome.Woodland ? 0.07 : biome == Biome.Highlands ? 0.04 : biome == Biome.Meadow || biome == Biome.Marsh ? 0.03 : 0.02;
                    if (!town && !levelled && rng.NextDouble() < bushChance)
                    {
                        var green = biome == Biome.Wasteland ? new Color(0.4f, 0.36f, 0.22f) : baseColour * 0.75f;
                        var shades = new[] { green * 0.78f, green, green * 1.18f + new Color(0.03f, 0.03f, 0f) };
                        float px = t.X + (float)rng.NextDouble() * 0.4f - 0.2f, pz = t.Z + (float)rng.NextDouble() * 0.4f - 0.2f;
                        float y = HeightAt(heights, n, x0, z0, px, pz);
                        float s = 0.7f + (float)rng.NextDouble() * 0.5f;
                        // Three tiers: a ring of low clumps, a smaller ring above, a crown.
                        int low = 4 + rng.Next(2), mid = 3 + rng.Next(2);
                        float turn = (float)rng.NextDouble() * 6.28f;
                        for (int tier = 0; tier < 3; tier++)
                        {
                            int count = tier == 0 ? low : tier == 1 ? mid : 1;
                            float ring = tier == 0 ? 0.3f : tier == 1 ? 0.17f : 0f, up = tier == 0 ? 0.22f : tier == 1 ? 0.42f : 0.56f;
                            float lump = tier == 0 ? 0.42f : tier == 1 ? 0.4f : 0.36f;
                            for (int i = 0; i < count; i++)
                            {
                                float ang = turn + i * 6.28f / count + tier * 0.5f + (float)rng.NextDouble() * 0.4f;
                                float w = s * lump * (0.85f + (float)rng.NextDouble() * 0.3f);
                                AddLump(bushes, sphere, shades[tier], new Vector3(px + Mathf.Cos(ang) * ring * s, y + up * s, pz + Mathf.Sin(ang) * ring * s),
                                    new Vector3(w, w * (0.75f + (float)rng.NextDouble() * 0.15f), w));
                            }
                        }
                        // In berry (red, purple or blue) or in flower (white, yellow or pink), now and then.
                        if (biome != Biome.Wasteland && rng.NextDouble() < 0.35)
                        {
                            var dot = BushDots[rng.Next(BushDots.Length)];
                            int dots = 7 + rng.Next(6);
                            for (int i = 0; i < dots; i++)
                            {
                                float ang = (float)rng.NextDouble() * 6.28f, rise = 0.15f + (float)rng.NextDouble() * 0.85f;  // up the dome
                                float r = Mathf.Cos(rise * 1.2f) * 0.52f * s, h = (0.2f + Mathf.Sin(rise * 1.2f) * 0.5f) * s;
                                float d = 0.07f + (float)rng.NextDouble() * 0.03f;
                                AddLump(bushes, sphere, dot, new Vector3(px + Mathf.Cos(ang) * r, y + h, pz + Mathf.Sin(ang) * r), new Vector3(d, d, d));
                            }
                        }
                    }
                }
            if (grass.Verts.Count > 0)
                MakeGrass(grass, root);
            if (bushes.Count > 0)
                MakeBushes(bushes, root);
        }

        /// The ground's height at a point in the chunk, from its corner heights.
        static float HeightAt(float[] heights, int n, int x0, int z0, float px, float pz)
        {
            float u = px - (x0 - 0.5f), v = pz - (z0 - 0.5f);
            int i = Mathf.Clamp(Mathf.FloorToInt(u), 0, n - 2), j = Mathf.Clamp(Mathf.FloorToInt(v), 0, n - 2);
            float fu = Mathf.Clamp01(u - i), fv = Mathf.Clamp01(v - j);
            float a = heights[i * n + j], b = heights[(i + 1) * n + j], c = heights[i * n + j + 1], d = heights[(i + 1) * n + j + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, fu), Mathf.Lerp(c, d, fu), fv);
        }

        static void MakeGrass(Builder b, Transform root)
        {
            var mesh = new Mesh { name = "Grass", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = b.Verts.ToArray();
            mesh.uv = b.Free.ToArray();
            // Lit as if facing up, like the ground they grow from (both sides alike).
            var normals = new Vector3[b.Verts.Count];
            for (int i = 0; i < normals.Length; i++)
                normals[i] = Vector3.up;
            mesh.normals = normals;
            mesh.subMeshCount = b.Tris.Count;
            var mats = new Material[b.Tris.Count];
            int s = 0;
            foreach (var kv in b.Tris)
            {
                mesh.SetTriangles(kv.Value, s);
                mats[s++] = Visuals.SwayMat(kv.Key, GrassSway, true);
            }
            mesh.RecalculateBounds();
            var go = new GameObject("Grass");
            go.layer = World.DetailLayer;
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<OwnedMesh>().Mesh = mesh;
        }

        static readonly Color[] BushDots =
        {
            new Color(0.8f, 0.12f, 0.12f), new Color(0.45f, 0.15f, 0.5f), new Color(0.25f, 0.3f, 0.75f),
            new Color(0.95f, 0.95f, 0.9f), new Color(0.95f, 0.85f, 0.25f), new Color(0.95f, 0.55f, 0.7f),
        };

        /// One round piece of a bush (a clump of leaves, a berry or a flower), in its colour's batch.
        static void AddLump(Dictionary<Color, List<CombineInstance>> bushes, Mesh sphere, Color c, Vector3 at, Vector3 size)
        {
            if (!bushes.TryGetValue(c, out var list))
                bushes[c] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = sphere, transform = Matrix4x4.TRS(at, Quaternion.identity, size) });
        }

        static void MakeBushes(Dictionary<Color, List<CombineInstance>> bushes, Transform root)
        {
            var parts = new CombineInstance[bushes.Count];
            var mats = new Material[bushes.Count];
            int i = 0;
            foreach (var kv in bushes)
            {
                var one = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                one.CombineMeshes(kv.Value.ToArray(), true, true);
                parts[i] = new CombineInstance { mesh = one, transform = Matrix4x4.identity };
                mats[i++] = Visuals.SwayMat(kv.Key, BushSway, false);
            }
            var mesh = new Mesh { name = "Bushes", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(parts, false, false);
            mesh.RecalculateBounds();
            foreach (var p in parts)
                Object.Destroy(p.mesh);
            var go = new GameObject("Bushes");
            go.layer = World.DetailLayer;
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            go.AddComponent<OwnedMesh>().Mesh = mesh;
        }

        static Mesh sphere;

        static Mesh SphereMesh()
        {
            if (sphere != null)
                return sphere;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return sphere;
        }
    }
}
