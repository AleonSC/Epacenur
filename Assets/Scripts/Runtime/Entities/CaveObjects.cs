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

    /// A mine shaft: a winch frame over a square hole, a rope hanging down into it. Climb down to the mine below,
    /// whose tier (and so its ore) you only find out once you're down there.
    public sealed class MineShaft : Interactable
    {
        public RegionPlan Mine { get; private set; }

        public override string Action => "Climb-down";
        public override string DisplayName => "Mine shaft";
        public override string Examine => "A rope leads down into a mine. There's no telling what ore is down there from up here.";
        public override Tile? ApproachTile => Overworld.CaveApproach(Mine);

        public override void Interact()
        {
            GameLog.Add("You climb down the rope into the mine...");
            Game.Instance.World.GoUnderground(Mine);
            var d = Game.Instance.World.CurrentDungeon;
            foreach (var t in Mines.Tiers)
                if (t.Tier == d.Tier)
                    GameLog.Add("<color=#f0d48a>It's a tier " + t.Tier + " mine: you can see " + t.Name + " in the walls.</color>");
        }

        public static MineShaft Create(RegionPlan mine, Transform parent)
        {
            var go = new GameObject("Mine shaft");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.Local(mine.CaveMouth);
            var m = go.AddComponent<MineShaft>();
            m.Mine = mine;
            m.Tile = mine.CaveMouth;
            ModelBuilder.Build(Look, go.transform);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.3f, 0f);
            box.size = new Vector3(1.6f, 2.6f, 1.4f);
            return m;
        }

        static readonly Rgb Wood = new Rgb(0.4f, 0.36f, 0.3f), Grey = new Rgb(0.5f, 0.47f, 0.42f), Rope = new Rgb(0.72f, 0.78f, 0.8f),
            Dark = new Rgb(0.02f, 0.02f, 0.025f), Earth = new Rgb(0.25f, 0.2f, 0.16f);

        /// Like a well's winch: a square frame of beams round a black hole, two posts (one braced) and a beam across
        /// the top, with the rope looped over it and hanging down the middle into the dark.
        static readonly Model Look = new Model()
            .Cube(0f, 0.02f, 0f, 1.05f, 0.02f, 1.05f, Dark)
            .Cube(0f, -0.15f, 0.5f, 1.05f, 0.3f, 0.04f, Earth).Cube(0f, -0.15f, -0.5f, 1.05f, 0.3f, 0.04f, Earth)  // the shaft's sides, just in
            .Cube(0f, 0.09f, -0.62f, 1.45f, 0.18f, 0.2f, Wood).Cube(0f, 0.09f, 0.62f, 1.45f, 0.18f, 0.2f, Wood)
            .Cube(-0.62f, 0.1f, 0f, 0.2f, 0.18f, 1.45f, Wood * 0.92f).Cube(0.62f, 0.1f, 0f, 0.2f, 0.18f, 1.45f, Wood * 0.92f)
            .Cube(-0.66f, 0.2f, -0.66f, 0.22f, 0.12f, 0.22f, Grey).Cube(0.66f, 0.2f, -0.66f, 0.22f, 0.12f, 0.22f, Grey)   // corner pegs
            .Cube(-0.66f, 0.2f, 0.66f, 0.22f, 0.12f, 0.22f, Grey).Cube(0.66f, 0.2f, 0.66f, 0.22f, 0.12f, 0.22f, Grey)
            .Cube(-0.62f, 1.3f, 0.45f, 0.14f, 2.4f, 0.14f, Wood)                       // the posts
            .Cube(0.62f, 1.3f, 0.45f, 0.14f, 2.4f, 0.14f, Wood)
            .Cube(0.78f, 1.15f, 0.1f, 0.12f, 2.3f, 0.12f, Wood * 0.9f).Turn(-18f, 0f, -12f)  // a brace
            .Cube(0f, 2.5f, 0.45f, 1.6f, 0.18f, 0.2f, Wood * 1.05f)                    // the beam across
            .Cube(0f, 2.5f, 0.45f, 0.2f, 0.24f, 0.26f, Rope)                           // the rope looped over it
            .Cube(0f, 2.38f, 0.22f, 0.06f, 0.06f, 0.46f, Rope)                         // and out over the hole
            .Cylinder(0f, 1.05f, 0f, 0.045f, 1.35f, 0.045f, Rope)                     // hanging down into the shaft
            .Cylinder(-0.62f, 1.6f, 0.45f, 0.17f, 0.08f, 0.17f, Rope * 0.9f);         // a bound joint

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
