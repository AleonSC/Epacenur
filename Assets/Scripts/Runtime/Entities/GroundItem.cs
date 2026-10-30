using System;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A pile of items lying on a tile, as the world keeps it: it stays until someone picks it up,
    /// even while its part of the world isn't loaded, and it's saved with the world.
    [Serializable]
    public sealed class GroundPile
    {
        public string item;
        public int count, x, z, map;
        public long cave;  // in a dungeon: its cave (a region key)
        public Tile Tile => new Tile(x, z);
    }

    /// An item lying on a tile (the visible side of a GroundPile): dropped by the player or by a monster.
    /// It doesn't block walking. Click it to walk onto its tile and pick it up ("Take").
    public sealed class GroundItem : MonoBehaviour
    {
        public GroundPile Pile { get; private set; }
        public string ItemId => Pile.item;
        public int Count => Pile.count;
        public Tile Tile => Pile.Tile;
        public ItemDef Def => ContentDb.Current.Item(ItemId);
        public string Label => Def.Stackable && Count > 1 ? Def.Name + " (" + Experience.Format(Count) + ")" : Def.Name;

        public static GroundItem Create(GroundPile pile, Transform parent, System.Random rng)
        {
            var def = ContentDb.Current.Item(pile.item);
            var go = new GameObject("Ground item " + def.Name);
            go.transform.SetParent(parent, false);
            // A little off the tile's centre and turned at random, so several items on a tile don't overlap.
            go.transform.localPosition = World.Local(pile.Tile) + new Vector3((float)rng.NextDouble() * 0.4f - 0.2f, 0.01f, (float)rng.NextDouble() * 0.4f - 0.2f);
            go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            var item = go.AddComponent<GroundItem>();
            item.Pile = pile;
            ModelBuilder.Build(def.GroundModel, go.transform);
            // A trigger: the mouse and the crosshair hit it, but you walk straight through.
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.15f, 0f);
            box.size = new Vector3(0.55f, 0.3f, 0.55f);
            return item;
        }
    }
}
