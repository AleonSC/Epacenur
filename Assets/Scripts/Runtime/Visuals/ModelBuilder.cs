using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Builds the models content describes (Core: Model) out of Unity primitives.
    public static class ModelBuilder
    {
        static readonly Dictionary<Color, Material> glowCache = new Dictionary<Color, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => glowCache.Clear();

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
            foreach (var p in model.Parts)
            {
                var go = Visuals.Prim(ToPrimitive(p.Shape), root, ToVector(p.Position), ToVector(p.Scale), ToColor(p.Colour));
                go.transform.localRotation = Quaternion.Euler(ToVector(p.Rotation));
                if (p.Glow)
                {
                    var r = go.GetComponent<Renderer>();
                    r.sharedMaterial = GlowMaterial(ToColor(p.Colour));
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    go.name = "Glow";
                }
            }
            foreach (var c in model.Children)
            {
                var child = new GameObject(c.Name ?? "Part").transform;
                child.SetParent(root, false);
                child.localPosition = ToVector(c.Position);
                child.localRotation = Quaternion.Euler(ToVector(c.Rotation));
                child.localScale = Vector3.one * (c.Scale == 0f ? 1f : c.Scale);
                Fill(c.Model, child);
            }
        }

        /// A self-lit material (flames, a furnace's mouth).
        static Material GlowMaterial(Color c)
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
