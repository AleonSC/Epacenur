using System;
using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Where your lost items wait after you die: in one world, at one spot, for 15 minutes of play in that
    /// world. Then it collapses and they're left lying on the ground there (where they stay; see GroundPile).
    [Serializable]
    public sealed class Grave
    {
        public string world;
        public int map, x, z;
        public float secondsLeft;
        public List<string> items = new List<string>();
        public List<int> counts = new List<int>();
        public Tile Tile => new Tile(x, z);
        public bool IsEmpty => items.Count == 0;
    }

    /// The gravestone in the world. "Reclaim" takes back as much as fits in your inventory.
    public sealed class Gravestone : Interactable
    {
        static Game G => Game.Instance;

        public override string Action => "Reclaim items";
        public override string DisplayName => "Gravestone (" + Time(G.Grave) + ")";
        public override string Examine => "Your gravestone. It will collapse in " + Time(G.Grave) + ", leaving your items on the ground.";
        public override void Interact() => G.ReclaimGrave();

        public static string Time(Grave grave)
        {
            int s = grave == null ? 0 : Mathf.CeilToInt(grave.secondsLeft);
            return s / 60 + ":" + (s % 60).ToString("00");
        }

        public static Gravestone Create(Grave grave, Transform parent)
        {
            var go = new GameObject("Gravestone");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.Local(grave.Tile);
            var stone = go.AddComponent<Gravestone>();
            stone.Tile = grave.Tile;
            var grey = new Rgb(0.6f, 0.6f, 0.62f);
            var model = new Model()
                .Cube(0f, 0.05f, 0f, 0.8f, 0.1f, 0.5f, grey * 0.8f)
                .Cube(0f, 0.5f, 0f, 0.5f, 0.8f, 0.14f, grey)
                .Sphere(0f, 0.9f, 0f, 0.5f, 0.2f, 0.14f, grey)
                .Cube(0f, 0.6f, -0.075f, 0.05f, 0.34f, 0.01f, grey * 0.6f)
                .Cube(0f, 0.68f, -0.075f, 0.22f, 0.05f, 0.01f, grey * 0.6f);
            ModelBuilder.Build(model, go.transform);
            var box = ModelBuilder.AddBox(go, model);
            box.isTrigger = true;  // you can stand next to it and walk round it
            return stone;
        }
    }
}
