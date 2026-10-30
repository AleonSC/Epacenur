using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A town well: fill your empty vials with water from it.
    public sealed class WellObject : Interactable
    {
        public override string Action => "Fill vials";
        public override string DisplayName => "Well";
        public override string Examine => "A stone well. The water looks clean enough.";

        public static WellObject Create(Tile tile, Transform parent)
        {
            var go = new GameObject("Well " + tile);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.Local(tile);
            ModelBuilder.Build(Landmarks.WellLook, go.transform);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.2f, 0f);
            box.size = new Vector3(1.1f, 2.4f, 1.1f);
            var well = go.AddComponent<WellObject>();
            well.Tile = tile;
            return well;
        }

        public override void Interact()
        {
            var inv = Game.Instance.Inventory;
            int n = inv.Remove(HerbloreModule.Vial, Inventory.Size);
            if (n == 0)
            {
                GameLog.Add("You have no empty vials to fill.");
                return;
            }
            inv.Add(HerbloreModule.VialOfWater, n);
            GameLog.Add(n == 1 ? "You fill the vial from the well." : "You fill " + n + " vials from the well.");
            Sfx.Play(Sound.Click, 0.6f);
        }
    }
}
