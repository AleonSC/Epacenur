using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// An arrow in flight, from the bow to the target (just for show: the hit is already rolled).
    public sealed class Projectile : MonoBehaviour
    {
        const float Speed = 18f;  // world units per second

        Vector3 from;
        Npc target;
        Vector3 lastTargetPos;
        float t, duration;

        public static void Fire(Vector3 from, Npc target, string ammoItemId)
        {
            var go = new GameObject("Arrow");
            var p = go.AddComponent<Projectile>();
            p.from = from;
            p.target = target;
            p.lastTargetPos = Aim(target);
            p.duration = Mathf.Max(0.12f, (p.lastTargetPos - from).magnitude / Speed);
            var def = ContentDb.Current.Item(ammoItemId);
            var tip = def != null ? def.Colour : new Rgb(0.6f, 0.6f, 0.6f);
            ModelBuilder.Build(new Model()
                .Cylinder(0f, 0f, 0f, 0.025f, 0.3f, 0.025f, new Rgb(0.7f, 0.55f, 0.32f)).Turn(90f, 0f, 0f)
                .Cube(0f, 0f, 0.32f, 0.05f, 0.05f, 0.08f, tip)
                .Cube(0f, 0f, -0.28f, 0.01f, 0.07f, 0.1f, new Rgb(0.95f, 0.95f, 0.93f)), go.transform);
            go.transform.position = from;
        }

        static Vector3 Aim(Npc npc) => npc.transform.position + Vector3.up * npc.Def.Height * 0.55f;

        void Update()
        {
            if (target != null)
                lastTargetPos = Aim(target);
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / duration);
            // A slight arc.
            var pos = Vector3.Lerp(from, lastTargetPos, f) + Vector3.up * Mathf.Sin(f * Mathf.PI) * 0.25f;
            var dir = pos - transform.position;
            if (dir.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(dir);
            transform.position = pos;
            if (f >= 1f)
                Destroy(gameObject);
        }
    }
}
