using UnityEngine;
using Epacenur.Core;

namespace Epacenur.Runtime
{
    /// An anvil: "Smith" opens the Smithing window, to hammer bronze bars into armour (needs a hammer).
    public sealed class Anvil : Interactable
    {
        public override string Action => "Smith";
        public override string DisplayName => "Anvil";
        public override string Examine => "Used for fashioning metal items.";
        public override void Interact() => Game.Instance.Hud.OpenSmithing(this);

        public static Anvil Create(Tile tile, Transform parent)
        {
            var go = new GameObject("Anvil " + tile);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.TileCenter(tile);
            var anvil = go.AddComponent<Anvil>();
            anvil.Tile = tile;
            var t = go.transform;
            var iron = new Color(0.25f, 0.26f, 0.28f);
            var wood = new Color(0.4f, 0.27f, 0.15f);
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.2f, 0f), new Vector3(0.6f, 0.2f, 0.6f), wood);    // stump
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.5f, 0f), new Vector3(0.22f, 0.22f, 0.3f), iron);      // waist
            Visuals.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.68f, 0f), new Vector3(0.7f, 0.16f, 0.32f), iron);     // face
            Visuals.Prim(PrimitiveType.Cylinder, t, new Vector3(0.42f, 0.68f, 0f), new Vector3(0.12f, 0.1f, 0.12f), iron)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);                                               // horn
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.4f, 0f);
            box.size = new Vector3(0.9f, 0.8f, 0.7f);
            return anvil;
        }
    }
}
