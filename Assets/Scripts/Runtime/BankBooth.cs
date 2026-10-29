using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A bank booth on one tile. Clicking it (tile mode: walk next to it first; first person: within reach)
    /// opens the bank window.
    public sealed class BankBooth : Interactable
    {
        public override string Action => "Bank";
        public override string DisplayName => "Bank booth";
        public override string Examine => "The bank teller will serve you here.";
        public override void Interact() => Game.Instance.Hud.OpenBank(this);

        public static BankBooth Create(Tile tile, Transform parent)
        {
            var go = new GameObject("Bank booth " + tile);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.TileCenter(tile);
            var booth = go.AddComponent<BankBooth>();
            booth.Tile = tile;
            booth.BuildModel();
            return booth;
        }

        void BuildModel()
        {
            var wood = new Color(0.45f, 0.3f, 0.18f);
            var darkWood = new Color(0.32f, 0.2f, 0.12f);
            var stone = new Color(0.62f, 0.6f, 0.56f);
            var gold = new Color(0.95f, 0.78f, 0.25f);
            var t = transform;
            // Counter, with the teller's window above it, under a little roof.
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.5f, 0f), new Vector3(0.96f, 1f, 0.96f), wood);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.03f, -0.06f), new Vector3(1f, 0.06f, 1.08f), darkWood);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(-0.45f, 1.55f, 0f), new Vector3(0.08f, 1f, 0.9f), stone);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0.45f, 1.55f, 0f), new Vector3(0.08f, 1f, 0.9f), stone);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.55f, 0.42f), new Vector3(0.9f, 1f, 0.06f), stone);
            for (int i = -1; i <= 1; i++)
                Visuals.Prim(PrimitiveType.Cube, t, new Vector3(i * 0.2f, 1.55f, -0.4f), new Vector3(0.025f, 1f, 0.025f), gold);
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 2.1f, -0.04f), new Vector3(1.1f, 0.12f, 1.12f), darkWood);
            // A gold coin sign on the front.
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.55f, -0.49f), new Vector3(0.4f, 0.02f, 0.4f), gold)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.1f, 0f);
            box.size = new Vector3(0.96f, 2.2f, 0.96f);
        }
    }
}
