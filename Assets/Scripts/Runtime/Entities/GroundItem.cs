using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// An item lying on a tile: dropped by the player or by a monster. It doesn't block walking.
    /// Click it to walk onto its tile and pick it up ("Take"). It disappears after a few minutes.
    public sealed class GroundItem : MonoBehaviour
    {
        public const float LifeSeconds = 180f;

        public string ItemId { get; private set; }
        public int Count { get; private set; }
        public Tile Tile { get; private set; }
        public ItemDef Def => ContentDb.Current.Item(ItemId);
        public string Label => Def.Stackable && Count > 1 ? Def.Name + " (" + Experience.Format(Count) + ")" : Def.Name;

        float expires;

        public static GroundItem Create(string itemId, int count, Tile tile, Transform parent, System.Random rng)
        {
            var def = ContentDb.Current.Item(itemId);
            var go = new GameObject("Ground item " + def.Name);
            go.transform.SetParent(parent, false);
            // A little off the tile's centre and turned at random, so several items on a tile don't overlap.
            go.transform.localPosition = World.Local(tile) + new Vector3((float)rng.NextDouble() * 0.4f - 0.2f, 0.01f, (float)rng.NextDouble() * 0.4f - 0.2f);
            go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            var item = go.AddComponent<GroundItem>();
            item.ItemId = itemId;
            item.Count = count;
            item.Tile = tile;
            item.expires = Time.time + LifeSeconds;
            ModelBuilder.Build(def.GroundModel, go.transform);
            // A trigger: the mouse and the crosshair hit it, but you walk straight through.
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.15f, 0f);
            box.size = new Vector3(0.55f, 0.3f, 0.55f);
            return item;
        }

        /// More of a stackable item dropped on the same tile joins this pile.
        public void AddCount(int n)
        {
            Count += n;
            expires = Time.time + LifeSeconds;
        }

        void Update()
        {
            if (Time.time >= expires)
                Game.Instance.World.RemoveGroundItem(this);
        }
    }
}
