using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// An agility obstacle: a log, wall, rope, pipe or stepping stones across the tiles it covers. Click it and
    /// you walk to its start, then cross to its end (or fall part way and land back at the start).
    public sealed class ObstacleObject : Interactable
    {
        public CourseDef Course { get; private set; }
        public ObstacleDef Def { get; private set; }
        public ObstaclePlacement Placement { get; private set; }

        public override string Action => Def.Action;
        public override string DisplayName => Def.Name;
        public override string Examine => Def.Examine;
        public override Tile? ApproachTile => Placement.Start;
        public override void Interact() => Game.Instance.Player.Cross(this);

        public static ObstacleObject Create(CourseDef course, ObstaclePlacement p, Transform parent)
        {
            var def = course.Obstacles[p.Index];
            var go = new GameObject(def.Name);
            go.transform.SetParent(parent, false);
            // Centred between its start and end, pointing along the crossing (+z).
            Vector3 a = World.Local(p.Start), b = World.Local(p.End);
            go.transform.localPosition = (a + b) / 2f;
            var dir = b - a;
            dir.y = 0f;
            go.transform.localRotation = Quaternion.LookRotation(dir);
            var o = go.AddComponent<ObstacleObject>();
            o.Course = course;
            o.Def = def;
            o.Placement = p;
            o.Tile = p.Covered[0];
            float len = p.Covered.Count;
            ModelBuilder.Build(Look(def.Look, len), go.transform);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = new Vector3(1f, 1f, len);
            return o;
        }

        /// How high you are over the ground while crossing (balancing on a log or rope).
        public float Lift => Def.Look == ObstacleLook.Log ? 0.45f : Def.Look == ObstacleLook.Rope ? 0.3f : 0f;

        static readonly Rgb Wood = new Rgb(0.5f, 0.34f, 0.2f), Bark = new Rgb(0.42f, 0.3f, 0.18f), Stone = new Rgb(0.55f, 0.53f, 0.5f),
            WaterBlue = new Rgb(0.2f, 0.42f, 0.7f), RopeColour = new Rgb(0.75f, 0.65f, 0.45f);

        /// The obstacle's model, `len` tiles long along z, centred on the origin.
        static readonly System.Collections.Generic.Dictionary<int, Model> looks = new System.Collections.Generic.Dictionary<int, Model>();

        /// One model per look and length, shared.
        static Model Look(ObstacleLook look, float len)
        {
            int key = (int)look * 100 + (int)len;
            if (!looks.TryGetValue(key, out var m))
                looks[key] = m = MakeLook(look, len);
            return m;
        }

        static Model MakeLook(ObstacleLook look, float len)
        {
            var m = new Model();
            float half = len / 2f;
            switch (look)
            {
                case ObstacleLook.Log:
                    m.Cube(0f, 0.01f, 0f, 1f, 0.02f, len, WaterBlue);  // over a ditch of water
                    m.Cylinder(0f, 0.3f, 0f, 0.4f, half + 0.5f, 0.4f, Bark).Turn(90f, 0f, 0f);
                    break;
                case ObstacleLook.Wall:
                    m.Cube(0f, 0.05f, 0f, 1f, 0.1f, len, Stone * 0.8f);
                    m.Cube(0f, 0.6f, 0f, 1.4f, 1.2f, 0.5f, Stone);
                    m.Cube(0f, 0.25f, -0.6f, 1f, 0.5f, 0.7f, Wood).Turn(-25f, 0f, 0f);
                    m.Cube(0f, 0.25f, 0.6f, 1f, 0.5f, 0.7f, Wood).Turn(25f, 0f, 0f);
                    break;
                case ObstacleLook.Rope:
                    m.Cube(0f, 0.01f, 0f, 1f, 0.02f, len, WaterBlue);
                    m.Cylinder(0f, 0.25f, 0f, 0.05f, half + 0.5f, 0.05f, RopeColour).Turn(90f, 0f, 0f);
                    foreach (float z in new[] { -half - 0.4f, half + 0.4f })
                        m.Cube(-0.35f, 0.7f, z, 0.1f, 1.4f, 0.1f, Wood).Cube(0.35f, 0.7f, z, 0.1f, 1.4f, 0.1f, Wood)
                         .Cube(0f, 1.35f, z, 0.8f, 0.08f, 0.08f, Wood);
                    m.Cylinder(-0.35f, 1.1f, 0f, 0.03f, half + 0.4f, 0.03f, RopeColour).Turn(90f, 0f, 0f);
                    m.Cylinder(0.35f, 1.1f, 0f, 0.03f, half + 0.4f, 0.03f, RopeColour).Turn(90f, 0f, 0f);
                    break;
                case ObstacleLook.Pipe:
                    m.Cylinder(0f, 0.5f, 0f, 1f, half + 0.3f, 1f, Stone * 0.9f).Turn(90f, 0f, 0f);
                    m.Cylinder(0f, 0.5f, -half - 0.3f, 0.75f, 0.02f, 0.75f, Looks.Dark).Turn(90f, 0f, 0f);
                    m.Cylinder(0f, 0.5f, half + 0.3f, 0.75f, 0.02f, 0.75f, Looks.Dark).Turn(90f, 0f, 0f);
                    break;
                case ObstacleLook.Stones:
                    m.Cube(0f, 0.01f, 0f, 1.2f, 0.02f, len + 0.4f, WaterBlue);
                    for (int i = 0; i < (int)len; i++)
                        m.Cylinder((i % 2 == 0 ? -0.15f : 0.15f), 0.06f, -half + 0.5f + i, 0.55f, 0.06f, 0.5f, Stone);
                    break;
                default:
                    m.Cube(0f, 0.5f, 0f, 1f, 1f, len, Wood);
                    break;
            }
            return m;
        }
    }
}
