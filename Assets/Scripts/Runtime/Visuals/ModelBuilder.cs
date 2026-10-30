using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Builds the models content describes (Core: Model) out of Unity's primitive shapes. Each model's solid
    /// parts are merged into one mesh (a sub-mesh per colour), worked out once per model and shared by everything
    /// built from it: one renderer instead of one per part, which is far cheaper with thousands of trees about.
    /// Glowing parts (flames: they flicker) and named children (a tree's crown: it shakes) stay objects of their own.
    public static class ModelBuilder
    {
        static readonly Dictionary<Color, Material> glowCache = new Dictionary<Color, Material>();
        static readonly Dictionary<Model, Baked> baked = new Dictionary<Model, Baked>();
        static readonly Dictionary<PrimitiveType, Mesh> shapes = new Dictionary<PrimitiveType, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            glowCache.Clear();
            baked.Clear();
            shapes.Clear();
        }

        /// A model's solid parts merged: the mesh and a material for each of its sub-meshes (null mesh: none).
        sealed class Baked
        {
            public Mesh Mesh;
            public Material[] Materials;
        }

        public static Color ToColor(Rgb c) => new Color(c.R, c.G, c.B);
        public static Vector3 ToVector(Vec3 v) => new Vector3(v.X, v.Y, v.Z);

        /// Builds `model` as a new child of `parent` (or at the root when parent is null) and returns it.
        /// Nothing gets a collider: the caller adds one that suits it.
        public static Transform Build(Model model, Transform parent, string name = "Model")
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            if (model != null)
                Fill(model, root);
            return root;
        }

        static void Fill(Model model, Transform root)
        {
            // The solid parts (with those of unnamed children): one shared, merged mesh.
            var b = Bake(model);
            if (b.Mesh != null)
            {
                root.gameObject.AddComponent<MeshFilter>().sharedMesh = b.Mesh;
                root.gameObject.AddComponent<MeshRenderer>().sharedMaterials = b.Materials;
            }
            AddGlows(model, root);
            // Named children stay objects of their own (they're found by name and moved).
            foreach (var c in model.Children)
            {
                if (c.Name == null)
                    continue;
                Fill(c.Model, ChildTransform(c, root));
            }
        }

        static Transform ChildTransform(ModelChild c, Transform parent)
        {
            var child = new GameObject(c.Name ?? "Part").transform;
            child.SetParent(parent, false);
            child.localPosition = ToVector(c.Position);
            child.localRotation = Quaternion.Euler(ToVector(c.Rotation));
            child.localScale = Vector3.one * (c.Scale == 0f ? 1f : c.Scale);
            return child;
        }

        /// The glowing parts of a model and its unnamed children, each its own object named "Glow" (flames flicker).
        static void AddGlows(Model model, Transform root)
        {
            foreach (var p in model.Parts)
            {
                if (!p.Glow)
                    continue;
                var go = Visuals.Prim(ToPrimitive(p.Shape), root, ToVector(p.Position), ToVector(p.Scale), ToColor(p.Colour));
                go.transform.localRotation = Quaternion.Euler(ToVector(p.Rotation));
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = GlowMaterial(ToColor(p.Colour));
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.name = "Glow";
            }
            foreach (var c in model.Children)
                if (c.Name == null && HasGlow(c.Model))
                    AddGlows(c.Model, ChildTransform(c, root));
        }

        static bool HasGlow(Model m)
        {
            foreach (var p in m.Parts)
                if (p.Glow)
                    return true;
            foreach (var c in m.Children)
                if (c.Name == null && HasGlow(c.Model))
                    return true;
            return false;
        }

        /// Frees the merged mesh made for a model that won't be built again (a dungeon's walls, once you leave).
        public static void Forget(Model model)
        {
            if (baked.TryGetValue(model, out var b))
            {
                if (b.Mesh != null)
                    Object.Destroy(b.Mesh);
                baked.Remove(model);
            }
        }

        /// The model's solid parts, and its unnamed children's, merged into one mesh with a sub-mesh per colour.
        static Baked Bake(Model model)
        {
            if (baked.TryGetValue(model, out var b))
                return b;
            var byColour = new Dictionary<Color, List<CombineInstance>>();
            Collect(model, Matrix4x4.identity, byColour);
            b = new Baked();
            if (byColour.Count > 0)
            {
                var parts = new CombineInstance[byColour.Count];
                b.Materials = new Material[byColour.Count];
                int i = 0;
                foreach (var kv in byColour)
                {
                    var one = new Mesh();
                    one.CombineMeshes(kv.Value.ToArray(), true, true);
                    parts[i] = new CombineInstance { mesh = one, transform = Matrix4x4.identity };
                    b.Materials[i] = Visuals.Mat(kv.Key);
                    i++;
                }
                b.Mesh = new Mesh { name = "Model" };
                b.Mesh.CombineMeshes(parts, false, false);
                b.Mesh.RecalculateBounds();
                foreach (var p in parts)
                    Object.Destroy(p.mesh);
            }
            baked[model] = b;
            return b;
        }

        static void Collect(Model model, Matrix4x4 m, Dictionary<Color, List<CombineInstance>> byColour)
        {
            foreach (var p in model.Parts)
            {
                if (p.Glow)
                    continue;
                var c = ToColor(p.Colour);
                if (!byColour.TryGetValue(c, out var list))
                    byColour[c] = list = new List<CombineInstance>();
                list.Add(new CombineInstance
                {
                    mesh = PrimitiveMesh(ToPrimitive(p.Shape)),
                    transform = m * Matrix4x4.TRS(ToVector(p.Position), Quaternion.Euler(ToVector(p.Rotation)), ToVector(p.Scale)),
                });
            }
            foreach (var ch in model.Children)
            {
                if (ch.Name != null)
                    continue;
                float s = ch.Scale == 0f ? 1f : ch.Scale;
                Collect(ch.Model, m * Matrix4x4.TRS(ToVector(ch.Position), Quaternion.Euler(ToVector(ch.Rotation)), Vector3.one * s), byColour);
            }
        }

        /// Unity's mesh for a primitive shape (taken once from a throwaway primitive).
        static Mesh PrimitiveMesh(PrimitiveType type)
        {
            if (shapes.TryGetValue(type, out var mesh) && mesh != null)
                return mesh;
            var go = GameObject.CreatePrimitive(type);
            mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return shapes[type] = mesh;
        }

        /// A self-lit material (flames, a furnace's mouth).
        public static Material GlowMaterial(Color c)
        {
            if (glowCache.TryGetValue(c, out var m) && m != null)
                return m;
            m = new Material(Visuals.Mat(c));
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 1.8f);
            }
            glowCache[c] = m;
            return m;
        }

        static PrimitiveType ToPrimitive(Shape s)
        {
            switch (s)
            {
                case Shape.Sphere: return PrimitiveType.Sphere;
                case Shape.Cylinder: return PrimitiveType.Cylinder;
                case Shape.Capsule: return PrimitiveType.Capsule;
                default: return PrimitiveType.Cube;
            }
        }

        /// A child (at any depth) with this name, or null.
        public static Transform Find(Transform root, string name)
        {
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// The box a model fills, in its own space (roughly: turned parts count at their largest).
        public static void Bounds(Model model, out Vector3 min, out Vector3 max)
        {
            min = Vector3.one * float.MaxValue;
            max = Vector3.one * float.MinValue;
            Grow(model, Vector3.zero, 1f, ref min, ref max);
            if (min.x > max.x)
                min = max = Vector3.zero;
        }

        static void Grow(Model model, Vector3 offset, float scale, ref Vector3 min, ref Vector3 max)
        {
            if (model == null)
                return;
            foreach (var p in model.Parts)
            {
                // Unity's cylinder and capsule are 2 units tall at scale 1.
                Vector3 size = ToVector(p.Scale);
                if (p.Shape == Shape.Cylinder || p.Shape == Shape.Capsule)
                    size.y *= 2f;
                if (p.Rotation.X != 0f || p.Rotation.Y != 0f || p.Rotation.Z != 0f)
                    size = Vector3.one * Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                Vector3 c = offset + ToVector(p.Position) * scale;
                min = Vector3.Min(min, c - size * scale / 2f);
                max = Vector3.Max(max, c + size * scale / 2f);
            }
            foreach (var ch in model.Children)
                Grow(ch.Model, offset + ToVector(ch.Position) * scale, scale * (ch.Scale == 0f ? 1f : ch.Scale), ref min, ref max);
        }

        /// A box collider round a model (the caller decides whether it's a trigger).
        public static BoxCollider AddBox(GameObject go, Model model)
        {
            Bounds(model, out var min, out var max);
            var box = go.AddComponent<BoxCollider>();
            box.center = (min + max) / 2f;
            box.size = Vector3.Max(max - min, Vector3.one * 0.1f);
            return box;
        }
    }
}
