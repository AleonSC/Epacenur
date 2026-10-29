using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A fire lit with a tinderbox. It blocks its tile while it burns, then dies down to ashes.
    /// "Cook" on it cooks the raw food you're carrying.
    public sealed class Fire : Interactable
    {
        public override string Action => "Cook";
        public override string DisplayName => "Fire";
        public override string Examine => "A hot fire.";
        public override void Interact() => Game.Instance.Player.CookOn(this);

        Transform[] flames;
        Light glow;
        float expires, seed;

        public static Fire Create(Tile tile, float burnSeconds, Transform parent)
        {
            var go = new GameObject("Fire " + tile);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.TileCenter(tile);
            var fire = go.AddComponent<Fire>();
            fire.Tile = tile;
            fire.expires = Time.time + burnSeconds;
            fire.seed = Random.value * 10f;
            fire.Build();
            return fire;
        }

        void Build()
        {
            var t = transform;
            // Logs in a little cross, then three flames that flicker.
            var bark = new Color(0.35f, 0.22f, 0.12f);
            for (int i = 0; i < 3; i++)
            {
                var log = Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.07f, 0f), new Vector3(0.11f, 0.28f, 0.11f), bark);
                log.transform.localRotation = Quaternion.Euler(90f, i * 60f, 0f);
            }
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.02f, 0f), new Vector3(0.5f, 0.01f, 0.5f), new Color(0.15f, 0.12f, 0.1f));
            flames = new Transform[3];
            var colours = new[] { new Color(1f, 0.35f, 0.05f), new Color(1f, 0.6f, 0.1f), new Color(1f, 0.9f, 0.4f) };
            for (int i = 0; i < 3; i++)
            {
                flames[i] = Visuals.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.2f + i * 0.08f, 0f), Vector3.one * (0.4f - i * 0.1f), colours[i]).transform;
                var r = flames[i].GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // Glowing: flames light themselves.
                var m = new Material(r.sharedMaterial);
                if (m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", colours[i] * 1.6f);
                }
                r.sharedMaterial = m;
            }

            glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(t, false);
            glow.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.6f, 0.25f);
            glow.range = 5f;
            glow.intensity = 1.5f;

            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;  // clickable (Examine), and tile mode can't path through it
            box.center = new Vector3(0f, 0.3f, 0f);
            box.size = new Vector3(0.7f, 0.6f, 0.7f);
        }

        void Update()
        {
            float time = Time.time + seed;
            for (int i = 0; i < flames.Length; i++)
            {
                float f = 1f + Mathf.Sin(time * (9f + i * 3f)) * 0.12f + Mathf.Sin(time * (23f + i * 5f)) * 0.06f;
                float size = 0.4f - i * 0.1f;
                flames[i].localScale = new Vector3(size * (2f - f), size * 1.6f * f, size * (2f - f));
            }
            glow.intensity = 1.4f + Mathf.Sin(time * 13f) * 0.25f;
            if (Time.time >= expires)
                Game.Instance.World.RemoveFire(this, true);
        }
    }
}
