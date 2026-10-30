using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// An arrow or a spell in flight, to a monster or to you (just for show: the hit is already rolled).
    public sealed class Projectile : MonoBehaviour
    {
        const float Speed = 18f;  // world units per second

        Vector3 from;
        Transform target;
        float aimHeight, arc;
        Vector3 lastTargetPos;
        float t, duration;

        /// An arrow from a bow at a monster.
        public static void Fire(Vector3 from, Npc target, string ammoItemId)
        {
            var def = ContentDb.Current.Item(ammoItemId);
            var tip = def != null ? def.Colour : new Rgb(0.6f, 0.6f, 0.6f);
            Launch("Arrow", from, target.transform, target.Def.Height * 0.55f, 0.25f, new Model()
                .Cylinder(0f, 0f, 0f, 0.025f, 0.3f, 0.025f, new Rgb(0.7f, 0.55f, 0.32f)).Turn(90f, 0f, 0f)
                .Cube(0f, 0f, 0.32f, 0.05f, 0.05f, 0.08f, tip)
                .Cube(0f, 0f, -0.28f, 0.01f, 0.07f, 0.1f, new Rgb(0.95f, 0.95f, 0.93f)));
        }

        /// A glowing ball of a spell's colour, flying straight at `target` (aimed `aimHeight` above its feet).
        public static void Spell(Vector3 from, Transform target, float aimHeight, Rgb colour)
        {
            Launch("Spell", from, target, aimHeight, 0f, new Model()
                .Sphere(0f, 0f, 0f, 0.28f, 0.28f, 0.28f, colour).Glowing()
                .Sphere(0f, 0f, -0.22f, 0.16f, 0.16f, 0.22f, colour * 0.8f).Glowing());
        }

        static void Launch(string name, Vector3 from, Transform target, float aimHeight, float arc, Model model)
        {
            var go = new GameObject(name);
            var p = go.AddComponent<Projectile>();
            p.from = from;
            p.target = target;
            p.aimHeight = aimHeight;
            p.arc = arc;
            p.lastTargetPos = p.Aim();
            p.duration = Mathf.Max(0.12f, (p.lastTargetPos - from).magnitude / Speed);
            ModelBuilder.Build(model, go.transform);
            go.transform.position = from;
        }

        Vector3 Aim() => target.position + Vector3.up * aimHeight;

        void Update()
        {
            if (target != null)
                lastTargetPos = Aim();
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / duration);
            // Arrows fly in a slight arc.
            var pos = Vector3.Lerp(from, lastTargetPos, f) + Vector3.up * Mathf.Sin(f * Mathf.PI) * arc;
            var dir = pos - transform.position;
            if (dir.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(dir);
            transform.position = pos;
            if (f >= 1f)
                Destroy(gameObject);
        }
    }
}
