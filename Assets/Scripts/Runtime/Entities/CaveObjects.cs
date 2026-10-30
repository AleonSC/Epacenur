using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A cave mouth in the mountains: a dark opening in a heap of rocks. Enter it to go down into its dungeon.
    public sealed class CaveMouth : Interactable
    {
        public RegionPlan Cave { get; private set; }
        public string CaveName;

        public override string Action => "Enter";
        public override string DisplayName => CaveName ?? "Cave entrance";
        public override string Examine => "A dark cave mouth. Who knows what lives down there? (Wilderness level " + Game.Instance.World.Overworld.Danger(Cave.CaveMouth) + ")";
        public override Tile? ApproachTile => Overworld.CaveApproach(Cave);

        public override void Interact()
        {
            GameLog.Add("You climb down into the darkness...");
            Game.Instance.World.GoUnderground(Cave);
        }

        public static CaveMouth Create(RegionPlan cave, string name, Transform parent)
        {
            var go = new GameObject("Cave mouth");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.Local(cave.CaveMouth);
            var m = go.AddComponent<CaveMouth>();
            m.Cave = cave;
            m.CaveName = name;
            m.Tile = cave.CaveMouth;
            ModelBuilder.Build(Look, go.transform);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1f, 0.3f);
            box.size = new Vector3(2.8f, 2f, 1.8f);
            return m;
        }

        static readonly Rgb Rock = new Rgb(0.45f, 0.43f, 0.4f), Dark = new Rgb(0.03f, 0.03f, 0.04f);

        /// Boulders heaped round a black opening facing south (-z).
        static readonly Model Look = new Model()
            .Sphere(-1.1f, 0.9f, 0.4f, 1.3f, 1.9f, 1.6f, Rock).Sphere(1.1f, 0.9f, 0.4f, 1.3f, 1.9f, 1.6f, Rock * 0.95f)
            .Sphere(0f, 1.9f, 0.6f, 2.6f, 1.2f, 1.8f, Rock * 0.9f).Sphere(0f, 0.6f, 1.3f, 2.4f, 1.4f, 1.2f, Rock * 0.85f)
            .Cube(0f, 0.75f, -0.35f, 1.1f, 1.5f, 0.1f, Dark).Sphere(0f, 1.45f, -0.36f, 1.1f, 0.5f, 0.1f, Dark)
            .Cube(0f, 0.02f, -0.6f, 1.2f, 0.04f, 0.6f, Rock * 0.7f);
    }

    /// The ladder back up out of a dungeon.
    public sealed class LadderObject : Interactable
    {
        public override string Action => "Climb-up";
        public override string DisplayName => "Ladder";
        public override string Examine => "It leads back up to daylight.";

        public override void Interact()
        {
            GameLog.Add("You climb back up the ladder.");
            Game.Instance.World.GoUp();
        }

        public static LadderObject Create(Tile tile, Transform parent)
        {
            var go = new GameObject("Ladder");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.Local(tile);
            var l = go.AddComponent<LadderObject>();
            l.Tile = tile;
            ModelBuilder.Build(Look, go.transform);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.5f, 0f);
            box.size = new Vector3(0.8f, 3f, 0.4f);
            return l;
        }

        static readonly Rgb Wood = new Rgb(0.5f, 0.35f, 0.2f);
        static readonly Model Look = MakeLook();

        static Model MakeLook()
        {
            var m = new Model().Cube(-0.3f, 1.6f, 0f, 0.08f, 3.2f, 0.08f, Wood).Cube(0.3f, 1.6f, 0f, 0.08f, 3.2f, 0.08f, Wood);
            for (int i = 0; i < 8; i++)
                m.Cube(0f, 0.25f + i * 0.4f, 0f, 0.6f, 0.06f, 0.06f, Wood * 0.9f);
            // A patch of daylight from the hole above.
            m.Cylinder(0f, 0.01f, 0f, 1.2f, 0.005f, 1.2f, new Rgb(0.55f, 0.55f, 0.45f));
            return m;
        }
    }
}
