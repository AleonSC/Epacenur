using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A furnace: "Smelt" turns copper and tin ore into bronze bars, as many as you can.
    public sealed class Furnace : Interactable
    {
        public override string Action => "Smelt";
        public override string DisplayName => "Furnace";
        public override string Examine => "A red hot furnace.";
        public override void Interact() => Game.Instance.Player.StartProduction(SmithingRules.BronzeBar, int.MaxValue, this);

        Light glow;

        public static Furnace Create(Tile tile, Transform parent)
        {
            var go = new GameObject("Furnace " + tile);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.TileCenter(tile);
            var furnace = go.AddComponent<Furnace>();
            furnace.Tile = tile;
            furnace.Build();
            return furnace;
        }

        void Build()
        {
            var t = transform;
            var stone = new Color(0.45f, 0.42f, 0.4f);
            var dark = new Color(0.28f, 0.26f, 0.25f);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.6f, 0f), new Vector3(0.95f, 1.2f, 0.9f), stone);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.25f, 0f), new Vector3(0.8f, 0.12f, 0.75f), dark);
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0.18f, 1.6f, 0.15f), new Vector3(0.3f, 0.35f, 0.3f), dark);  // chimney
            // The mouth, glowing (it faces -z).
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.45f, -0.44f), new Vector3(0.5f, 0.42f, 0.04f), new Color(0.12f, 0.08f, 0.06f));
            var fire = Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.38f, -0.43f), new Vector3(0.4f, 0.22f, 0.05f), new Color(1f, 0.45f, 0.1f));
            var r = fire.GetComponent<Renderer>();
            var m = new Material(r.sharedMaterial);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.1f) * 2f);
            }
            r.sharedMaterial = m;
            glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(t, false);
            glow.transform.localPosition = new Vector3(0f, 0.5f, -0.8f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.55f, 0.2f);
            glow.range = 3.5f;
            glow.intensity = 1.2f;
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.8f, 0f);
            box.size = new Vector3(0.95f, 1.6f, 0.9f);
        }

        void Update() => glow.intensity = 1.1f + Mathf.Sin(Time.time * 7f) * 0.15f;
    }
}
