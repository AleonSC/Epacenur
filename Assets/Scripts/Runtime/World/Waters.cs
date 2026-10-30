using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The surface of ponds and rivers in a chunk. It ends exactly where the ground, as the ground mesh draws it
    /// (a corner at each tile corner, two triangles a tile), rises out of the water, so it never hangs over the
    /// ground or stops short of it: the shore is where the land meets the water, curving wherever the land
    /// does. Only round the water tiles (bridges too: the water runs under them), so a hollow elsewhere stays
    /// dry. Traced a quarter of a tile at a time (marching squares on the water's depth over the ground).
    ///
    /// Each corner carries how deep the water is there: its colour's red (0 to 1 of the overworld's water depth),
    /// and its first uv (x: 0 on the shore, 1 a few centimetres deep; y: 0 on the shore, 1 well out), so the
    /// water shader fades it in at the shore, tints the shallows and lays the foam; the second uv is the
    /// river's flow. (So the shader needs no depth texture.)
    public static class Waters
    {
        const int Sub = 4;  // samples a tile

        static bool IsWater(Terrain t) => t == Terrain.Water || t == Terrain.Bridge;

        public static void Build(Overworld ow, int x0, int z0, int size, Transform root, Color colour)
        {
            // Water tiles, for the chunk's tiles and one round it.
            int span = size + 2;
            var wet = new bool[span, span];
            bool any = false;
            for (int i = 0; i < span; i++)
                for (int j = 0; j < span; j++)
                    any |= wet[i, j] = IsWater(ow.At(new Tile(x0 - 1 + i, z0 - 1 + j)));
            if (!any)
                return;

            // The ground's corners (the ground mesh's), and the water's height and flow at each sample point.
            var corner = new float[size + 1, size + 1];
            for (int i = 0; i <= size; i++)
                for (int j = 0; j <= size; j++)
                    corner[i, j] = (float)ow.Elevation(x0 - 0.5 + i, z0 - 0.5 + j);
            int n = size * Sub + 1;
            var depth = new float[n, n];  // the water's surface less the ground: over 0 where there's water
            var height = new float[n, n];
            var flows = new Vector2[n, n];
            for (int a = 0; a < n; a++)
                for (int b = 0; b < n; b++)
                {
                    // Only on or next to the water tiles (a sample on a tile's edge or corner is on each tile it touches).
                    bool near = false;
                    for (int i = (a - 1) / Sub - 1; i <= a / Sub + 1 && !near; i++)
                        for (int j = (b - 1) / Sub - 1; j <= b / Sub + 1 && !near; j++)
                            if (i >= -1 && j >= -1 && i <= size && j <= size && wet[i + 1, j + 1])
                                near = true;
                    float x = x0 - 0.5f + a / (float)Sub, z = z0 - 0.5f + b / (float)Sub;
                    height[a, b] = (float)ow.WaterSurface(x, z);
                    depth[a, b] = near ? height[a, b] - Ground(corner, a, b) : -1f;
                    if (near)
                    {
                        ow.RiverFlow(x, z, out double fx, out double fz);
                        flows[a, b] = new Vector2((float)fx, (float)fz);
                    }
                }

            var verts = new List<Vector3>();
            var shore = new List<Vector2>();
            var flow = new List<Vector2>();
            var deep = new List<Color>();
            var tris = new List<int>();
            var poly = new List<Vector3>();  // a, the depth, b (in samples)
            for (int a = 0; a < n - 1; a++)
                for (int b = 0; b < n - 1; b++)
                {
                    float d00 = depth[a, b], d10 = depth[a + 1, b], d11 = depth[a + 1, b + 1], d01 = depth[a, b + 1];
                    if (d00 <= 0f && d10 <= 0f && d11 <= 0f && d01 <= 0f)
                        continue;
                    // The cell's corners under water, and where each side reaches the shore.
                    poly.Clear();
                    Corner(poly, a, b, d00, a + 1, b, d10);
                    Corner(poly, a + 1, b, d10, a + 1, b + 1, d11);
                    Corner(poly, a + 1, b + 1, d11, a, b + 1, d01);
                    Corner(poly, a, b + 1, d01, a, b, d00);
                    int start = verts.Count;
                    foreach (var p in poly)
                    {
                        float x = x0 - 0.5f + p.x / Sub, z = z0 - 0.5f + p.z / Sub;
                        // Between the cell's corners (a point on a side, from the two corners at its ends).
                        float u = p.x - a, v = p.z - b;
                        float h = Mathf.Lerp(Mathf.Lerp(height[a, b], height[a + 1, b], u), Mathf.Lerp(height[a, b + 1], height[a + 1, b + 1], u), v);
                        var fl = Vector2.Lerp(Vector2.Lerp(flows[a, b], flows[a + 1, b], u), Vector2.Lerp(flows[a, b + 1], flows[a + 1, b + 1], u), v);
                        float d = Mathf.Max(p.y, 0f);
                        verts.Add(new Vector3(x, h, z));
                        shore.Add(new Vector2(Mathf.Clamp01(d / 0.04f), Mathf.Clamp01(d / 0.25f)));
                        flow.Add(fl);
                        deep.Add(new Color(Mathf.Clamp01(d / (float)Overworld.WaterDepth), 0f, 0f, 1f));
                    }
                    // Facing up: the corners go round anticlockwise seen from above, so wind them the other way.
                    for (int k = 1; k + 1 < poly.Count; k++)
                    {
                        tris.Add(start);
                        tris.Add(start + k + 1);
                        tris.Add(start + k);
                    }
                }
            if (tris.Count == 0)
                return;
            var mesh = new Mesh { name = "Water" };
            if (verts.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts.ToArray();
            mesh.uv = shore.ToArray();
            mesh.uv2 = flow.ToArray();
            mesh.colors = deep.ToArray();
            mesh.triangles = tris.ToArray();
            var up = new Vector3[verts.Count];
            for (int k = 0; k < up.Length; k++)
                up[k] = Vector3.up;
            mesh.normals = up;
            mesh.RecalculateBounds();
            var go = new GameObject("Water");
            go.layer = World.FarLayer;
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Visuals.WaterMat(colour);
            go.AddComponent<OwnedMesh>().Mesh = mesh;
        }

        /// The ground's height at sample (a, b), as the ground mesh draws it: each tile is two triangles, split
        /// along the diagonal from its (-x, +z) corner to its (+x, -z) corner.
        static float Ground(float[,] corner, int a, int b)
        {
            int i = Mathf.Min(a / Sub, corner.GetLength(0) - 2), j = Mathf.Min(b / Sub, corner.GetLength(1) - 2);
            float u = (a - i * Sub) / (float)Sub, v = (b - j * Sub) / (float)Sub;
            float c00 = corner[i, j], c01 = corner[i, j + 1], c10 = corner[i + 1, j], c11 = corner[i + 1, j + 1];
            return u + v <= 1f ? c00 + (c10 - c00) * u + (c01 - c00) * v : c11 + (c01 - c11) * (1f - u) + (c10 - c11) * (1f - v);
        }

        /// One corner of a cell, if it's under water, and where the side from it to the next reaches the shore (if
        /// it does): in sample units, with the depth in y.
        static void Corner(List<Vector3> poly, int a, int b, float d, int a2, int b2, float d2)
        {
            if (d > 0f)
                poly.Add(new Vector3(a, d, b));
            if ((d > 0f) != (d2 > 0f))
            {
                float t = d / (d - d2);
                poly.Add(new Vector3(a + (a2 - a) * t, 0f, b + (b2 - b) * t));
            }
        }
    }
}
