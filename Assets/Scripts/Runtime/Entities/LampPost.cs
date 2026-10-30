using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A lamp post in a town: an iron post with a lantern that lights up at dusk and goes out at dawn.
    public sealed class LampPost : MonoBehaviour
    {
        const float Range = 8f, Brightness = 1.5f;
        static readonly Rgb Iron = new Rgb(0.18f, 0.18f, 0.2f), Flame = new Rgb(1f, 0.82f, 0.45f);

        static readonly Model Look = new Model()
                .Cylinder(0f, 0.08f, 0f, 0.3f, 0.08f, 0.3f, Iron)
                .Cylinder(0f, 1.3f, 0f, 0.09f, 1.25f, 0.09f, Iron)
                .Cube(0f, 2.55f, 0f, 0.36f, 0.05f, 0.36f, Iron)
                .Cube(0f, 2.82f, 0f, 0.42f, 0.06f, 0.42f, Iron)
                .Cylinder(0f, 2.92f, 0f, 0.12f, 0.06f, 0.12f, Iron)
                .Cube(0f, 2.69f, 0f, 0.28f, 0.24f, 0.28f, Flame).Glowing();

        Light glow;
        Renderer[] flames;
        float lit;   // 0 out .. 1 fully lit (it fades)
        float seed;

        public static LampPost Create(Tile tile, Transform parent)
        {
            var go = new GameObject("Lamp post");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.Local(tile);
            ModelBuilder.Build(Look, go.transform);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.5f, 0f);
            box.size = new Vector3(0.4f, 3f, 0.4f);
            var lamp = go.AddComponent<LampPost>();
            lamp.Init();
            return lamp;
        }

        void Init()
        {
            seed = Random.value * 10f;
            glow = new GameObject("Light").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.8f, 0.5f);
            glow.range = Range;
            glow.shadows = LightShadows.None;
            flames = System.Array.FindAll(GetComponentsInChildren<Renderer>(), r => r.gameObject.name == "Glow");
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            var clock = Game.Instance != null ? Game.Instance.Clock : null;
            bool night = clock != null && clock.LampsLit;
            lit = Mathf.MoveTowards(lit, night ? 1f : 0f, Time.deltaTime / 2f);
            glow.enabled = lit > 0.01f;
            glow.intensity = Brightness * lit * (0.95f + Mathf.Sin((Time.time + seed) * 6f) * 0.05f);
            foreach (var f in flames)
                f.enabled = lit > 0.5f;
        }
    }
}
