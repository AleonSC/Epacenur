using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// The surface of ponds and rivers in a chunk, with a rounded, natural outline rather than a square per
    /// tile. Each water tile (bridges too: the water runs under them) counts 1 and each dry one 0; blurred
    /// (a Gaussian over the tiles round) and wobbled a little by noise, that gives a smooth field, and the
    /// water is where it's over a half: the shore runs along the edges of the water tiles where they're
    /// straight, and curves across their steps and corners. Traced a quarter of a tile at a time (marching
    /// squares). Each corner's first uv says how far inside the edge it is (x: 0 on it, 1 just inside; y: 0 on
    /// it, 1 about a tile in), so the water shader fades it out there and tints the shallows; the second uv is
    /// the river's flow.
    public static class Waters
    {
        const int Sub = 4;        // samples a tile
        const float Wobble = 0.2f;
        const float Blur = 0.55f; // the Gaussian's spread, in tiles
        const int Reach = 2;      // tiles round a point the blur looks at

        static bool IsWater(Terrain t) => t == Terrain.Water || t == Terrain.Bridge;

        public static void Build(Overworld ow, int x0, int z0, int size, Transform root, Color colour)
        {
            // Wet or not, for the chunk's tiles and Reach round it.
            int span = size + Reach * 2;
            var wet = new float[span, span];
            bool any = false;
            for (int i = 0; i < span; i++)
                for (int j = 0; j < span; j++)
                    if (IsWater(ow.At(new Tile(x0 - Reach + i, z0 - Reach + j))))
                    {
                        wet[i, j] = 1f;
                        any |= i >= Reach - 1 && j >= Reach - 1 && i <= size + Reach && j <= size + Reach;
                    }
            if (!any)
                return;

            // The field at every sample point (the first is the chunk's corner, x0 - 0.5, z0 - 0.5): the blur is a
            // Gaussian, so it's done along x then along z. A sample's offset from the tile centres repeats every
            // tile, so the weights are worked out once for each of the Sub offsets.
            int n = size * Sub + 1;
            // Sample a (or b) lies a % Sub quarters past the corner of chunk tile a / Sub; the tiles it looks at are
            // chunk tiles a / Sub + k - Reach - 1 (k from 0), which are wet[a / Sub + k - 1].
            var weight = new float[Sub, Reach * 2 + 2];  // [offset in a tile, k]
            for (int o = 0; o < Sub; o++)
                for (int k = 0; k < Reach * 2 + 2; k++)
                {
                    float d = -0.5f + o / (float)Sub - (k - Reach - 1);
                    weight[o, k] = Mathf.Exp(-d * d / (2f * Blur * Blur));
                }
            // Along x: for each sample column, each tile row.
            var across = new float[n, span];
            var norm = new float[n];
            for (int a = 0; a < n; a++)
            {
                int o = a % Sub, i0 = a / Sub;
                for (int k = 0; k < Reach * 2 + 2; k++)
                {
                    int i = i0 + k - 1;
                    if (i < 0 || i >= span)
                        continue;
                    norm[a] += weight[o, k];
                    for (int j = 0; j < span; j++)
                        across[a, j] += wet[i, j] * weight[o, k];
                }
            }
            var field = new float[n, n];
            for (int a = 0; a < n; a++)
                for (int b = 0; b < n; b++)
                {
                    int o = b % Sub, j0 = b / Sub;
                    float sum = 0f, weights = 0f;
                    for (int k = 0; k < Reach * 2 + 2; k++)
                    {
                        int j = j0 + k - 1;
                        if (j < 0 || j >= span)
                            continue;
                        sum += across[a, j] * weight[o, k];
                        weights += weight[o, k];
                    }
                    float x = x0 - 0.5f + a / (float)Sub, z = z0 - 0.5f + b / (float)Sub;
                    field[a, b] = sum / (weights * norm[a]) + (DirtPaths.Noise(x * 1.3f + 71.3f, z * 1.3f - 23.9f) - 0.5f) * Wobble;
                }
            // The water's height and flow at each sample point, worked out the first time a cell needs them.
            var height = new float[n, n];
            var flows = new Vector2[n, n];
            var known = new bool[n, n];

            var verts = new List<Vector3>();
            var edge = new List<Vector2>();
            var flow = new List<Vector2>();
            var tris = new List<int>();
            var poly = new List<Vector3>();  // x, z, and the field
            for (int a = 0; a < n - 1; a++)
                for (int b = 0; b < n - 1; b++)
                {
                    float f00 = field[a, b], f10 = field[a + 1, b], f11 = field[a + 1, b + 1], f01 = field[a, b + 1];
                    if (f00 <= 0.5f && f10 <= 0.5f && f11 <= 0.5f && f01 <= 0.5f)
                        continue;
                    // Round the cell's corners, keeping the corners in the water and where each side crosses the edge.
                    poly.Clear();
                    Corner(poly, a, b, f00, a + 1, b, f10);
                    Corner(poly, a + 1, b, f10, a + 1, b + 1, f11);
                    Corner(poly, a + 1, b + 1, f11, a, b + 1, f01);
                    Corner(poly, a, b + 1, f01, a, b, f00);
                    for (int c = 0; c < 4; c++)
                    {
                        int ca = a + (c == 1 || c == 2 ? 1 : 0), cb = b + (c >= 2 ? 1 : 0);
                        if (known[ca, cb])
                            continue;
                        known[ca, cb] = true;
                        float cx = x0 - 0.5f + ca / (float)Sub, cz = z0 - 0.5f + cb / (float)Sub;
                        height[ca, cb] = (float)ow.WaterSurface(cx, cz);
                        ow.RiverFlow(cx, cz, out double fx, out double fz);
                        flows[ca, cb] = new Vector2((float)fx, (float)fz);
                    }
                    int start = verts.Count;
                    foreach (var p in poly)
                    {
                        float x = x0 - 0.5f + p.x / Sub, z = z0 - 0.5f + p.z / Sub;
                        // Between the cell's corners (a point on a side, from the two corners at its ends).
                        float u = p.x - a, v = p.z - b;
                        float h = Mathf.Lerp(Mathf.Lerp(height[a, b], height[a + 1, b], u), Mathf.Lerp(height[a, b + 1], height[a + 1, b + 1], u), v);
                        var fl = Vector2.Lerp(Vector2.Lerp(flows[a, b], flows[a + 1, b], u), Vector2.Lerp(flows[a, b + 1], flows[a + 1, b + 1], u), v);
                        verts.Add(new Vector3(x, h, z));
                        edge.Add(new Vector2(Mathf.Clamp01((p.y - 0.5f) * 5f), Mathf.Clamp01((p.y - 0.5f) * 2.2f)));
                        flow.Add(fl);
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
            mesh.uv = edge.ToArray();
            mesh.uv2 = flow.ToArray();
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

        /// One corner of a cell, and where the side from it to the next crosses the edge (if it does): in sample
        /// units, with the field in y.
        static void Corner(List<Vector3> poly, int a, int b, float f, int a2, int b2, float f2)
        {
            if (f > 0.5f)
                poly.Add(new Vector3(a, f, b));
            if ((f > 0.5f) != (f2 > 0.5f))
            {
                float t = (0.5f - f) / (f2 - f);
                poly.Add(new Vector3(a + (a2 - a) * t, 0.5f, b + (b2 - b) * t));
            }
        }
    }
}
