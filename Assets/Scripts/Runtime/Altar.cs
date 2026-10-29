using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A stone altar. "Pray-at" recharges your Prayer points to full.
    public sealed class Altar : Interactable
    {
        public override string Action => "Pray-at";
        public override string DisplayName => "Altar";
        public override string Examine => "An altar to the gods.";

        public override void Interact()
        {
            var g = Game.Instance;
            g.Player.Kneel(1.2f);
            if (g.Prayer.Points >= g.Prayer.Max)
            {
                GameLog.Add("You already have full Prayer points.");
                return;
            }
            g.Prayer.Recharge();
            GameLog.Add("You pray at the altar... You recharge your Prayer points.");
        }

        Transform[] flames;

        public static Altar Create(Tile tile, Transform parent)
        {
            var go = new GameObject("Altar " + tile);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.TileCenter(tile);
            var altar = go.AddComponent<Altar>();
            altar.Tile = tile;
            altar.BuildModel();
            return altar;
        }

        void BuildModel()
        {
            var t = transform;
            var stone = new Color(0.72f, 0.7f, 0.66f);
            var dark = new Color(0.55f, 0.53f, 0.5f);
            var cloth = new Color(0.92f, 0.9f, 0.85f);
            var gold = new Color(0.9f, 0.74f, 0.25f);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.08f, 0f), new Vector3(0.98f, 0.16f, 0.9f), dark);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.45f, 0f), new Vector3(0.82f, 0.6f, 0.72f), stone);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.8f, 0f), new Vector3(0.95f, 0.1f, 0.85f), dark);
            // Altar cloth draped over the top and down the front, with a gold stripe and symbol.
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.86f, 0f), new Vector3(0.55f, 0.02f, 0.87f), cloth);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.62f, -0.44f), new Vector3(0.55f, 0.48f, 0.02f), cloth);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.62f, -0.452f), new Vector3(0.08f, 0.44f, 0.01f), gold);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.7f, -0.452f), new Vector3(0.26f, 0.06f, 0.01f), gold);
            // Two candles.
            flames = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.33f : 0.33f;
                Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(x, 0.93f, 0.1f), new Vector3(0.07f, 0.08f, 0.07f), new Color(0.96f, 0.93f, 0.8f));
                flames[i] = Visuals.Prim(PrimitiveType.Sphere, t, new Vector3(x, 1.04f, 0.1f), new Vector3(0.04f, 0.07f, 0.04f), new Color(1f, 0.75f, 0.2f)).transform;
            }
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = new Vector3(0.96f, 1f, 0.9f);
        }

        void Update()
        {
            for (int i = 0; i < flames.Length; i++)
                flames[i].localScale = new Vector3(0.04f, 0.07f * (1f + Mathf.Sin(Time.time * (11f + i * 4f)) * 0.15f), 0.04f);
        }
    }
}
