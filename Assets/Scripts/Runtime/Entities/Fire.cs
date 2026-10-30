using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A fire lit with a tinderbox: the "fire" station (cook on it). It blocks its tile while it burns,
    /// then dies down to ashes.
    public sealed class Fire : StationObject
    {
        static readonly Model Flames = MakeFlames();

        static Model MakeFlames()
        {
            var flames = new Model();
            var colours = new[] { new Rgb(1f, 0.35f, 0.05f), new Rgb(1f, 0.6f, 0.1f), new Rgb(1f, 0.9f, 0.4f) };
            for (int i = 0; i < 3; i++)
                flames.Sphere(0f, 0.2f + i * 0.08f, 0f, 0.4f - i * 0.1f, 0.4f - i * 0.1f, 0.4f - i * 0.1f, colours[i]).Glowing();
            return flames;
        }

        Transform[] flameParts;
        float expires, flicker;

        public static Fire Create(Tile tile, float burnSeconds, Transform parent)
        {
            var go = new GameObject("Fire " + tile);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.Local(tile);
            var fire = go.AddComponent<Fire>();
            fire.Init(ContentDb.Current.Station(FiremakingModule.Fire), tile);
            fire.expires = Time.time + burnSeconds;
            fire.flicker = Random.value * 10f;
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
            var built = ModelBuilder.Build(Flames, t, "Flames");
            flameParts = new Transform[built.childCount];
            for (int i = 0; i < flameParts.Length; i++)
                flameParts[i] = built.GetChild(i);
            AddLight(new Vector3(0f, 0.6f, 0f), new Color(1f, 0.6f, 0.25f), 5f, 1.5f);

            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;  // clickable, and tile mode can't path through it
            box.center = new Vector3(0f, 0.3f, 0f);
            box.size = new Vector3(0.7f, 0.6f, 0.7f);
        }

        protected override void Update()
        {
            base.Update();
            float time = Time.time + flicker;
            for (int i = 0; i < flameParts.Length; i++)
            {
                float f = 1f + Mathf.Sin(time * (9f + i * 3f)) * 0.12f + Mathf.Sin(time * (23f + i * 5f)) * 0.06f;
                float size = 0.4f - i * 0.1f;
                flameParts[i].localScale = new Vector3(size * (2f - f), size * 1.6f * f, size * (2f - f));
            }
            if (Time.time >= expires)
                Game.Instance.World.RemoveFire(this, true);
        }
    }
}
