using System;
using Epacenur.Content;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// A farming patch: a bed of soil, empty or with a crop growing in it (taller each stage). A town's patches are
    /// its farmer's (weedy) until you rent them from him. Use a seed on one of yours to plant it; once grown,
    /// Harvest picks the herbs or digs up the crop (with a spade), one at a time.
    public sealed class PatchObject : Interactable
    {
        public PatchKind Kind { get; private set; }
        Transform plants;
        int shownStage = -1;
        string shownSeed;
        bool shownYours = true;
        float nextCheck;

        static readonly Rgb Soil = new Rgb(0.32f, 0.22f, 0.13f), Edge = new Rgb(0.42f, 0.3f, 0.18f);

        PatchState State => Game.Instance.World.Patch(Tile);
        SeedDef Seed => ContentDb.Current.Seed(State.seed);
        bool Grown => Farming.IsGrown(State, Seed, DateTime.UtcNow);
        /// Yours to plant: rented from the farmer (the dev plot's are anyone's).
        bool Yours => Game.Instance.World.Map == MapId.DevPlot || State.rented;

        public override string Action => State.seed == null ? "Inspect" : Grown ? (Kind == PatchKind.Herb ? "Pick" : "Harvest") : "Inspect";
        public override string DisplayName => Kind == PatchKind.Herb ? "Herb patch" : "Allotment";
        public override string Examine
        {
            get
            {
                var s = Seed;
                if (!Yours)
                    return "One of the farmer's " + (Kind == PatchKind.Herb ? "herb patches" : "allotments") + ", overgrown with weeds. Rent it from the farmer ("
                        + Farming.RentCost(Kind) + " coins) to grow your own.";
                if (s == null)
                    return "Your empty " + DisplayName.ToLowerInvariant() + ". Use a seed on it to plant it.";
                if (Grown)
                    return "The " + s.Name.ToLowerInvariant() + " is ready to harvest (" + State.lives + " left).";
                double g = Farming.Growth(State, s, DateTime.UtcNow);
                int minutes = (int)Math.Ceiling(s.GrowMinutes * (1 - g));
                return "Some " + s.Name.ToLowerInvariant() + " is growing here: about " + minutes + " minute" + (minutes == 1 ? "" : "s") + " to go.";
            }
        }

        public static PatchObject Create(Tile tile, PatchKind kind, Transform parent)
        {
            var go = new GameObject(kind + " patch");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = World.Local(tile);
            var p = go.AddComponent<PatchObject>();
            p.Tile = tile;
            p.Kind = kind;
            ModelBuilder.Build(Bed, go.transform);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.25f, 0f);
            box.size = new Vector3(0.95f, 0.5f, 0.95f);
            return p;
        }

        static readonly Model Bed = new Model()
            .Cube(0f, 0.06f, 0f, 0.96f, 0.12f, 0.96f, Edge)
            .Cube(0f, 0.08f, 0f, 0.84f, 0.12f, 0.84f, Soil);

        public override void Interact()
        {
            var s = Seed;
            if (s == null || !Grown)
            {
                GameLog.Add(Examine);
                return;
            }
            Game.Instance.Player.StartHarvest(this);
        }

        /// A seed used on the patch: plants it if it can go here.
        public void Plant(int slot)
        {
            var g = Game.Instance;
            if (!Yours)
            {
                GameLog.Add("This patch isn't yours. Rent it from the farmer in the farmhouse first.");
                return;
            }
            var stack = g.Inventory[slot];
            var seed = ContentDb.Current.Seed(stack.ItemId);
            string problem = Farming.Problem(State, Kind, seed, g.Skills.GetLevel(Farming.Skill));
            if (problem != null)
            {
                GameLog.Add(problem);
                return;
            }
            g.Inventory.Use(stack.ItemId, 1);
            Farming.Plant(State, seed, DateTime.UtcNow, g.Rng);
            g.Skills.AddXp(Farming.Skill, seed.PlantXp);
            g.Player.Kneel(0.8f);
            GameLog.Add("You plant " + Making.Article(seed.Name.ToLowerInvariant() + " seed") + " in the " + DisplayName.ToLowerInvariant() + ".");
            Refresh();
        }

        /// One harvest. Returns whether there's more to take.
        public bool HarvestOne()
        {
            var g = Game.Instance;
            var s = Seed;
            if (s == null || !Grown)
                return false;
            if (!g.Inventory.HasRoomFor(s.Product))
            {
                GameLog.Add("You don't have room for any more.");
                return false;
            }
            if (Kind == PatchKind.Allotment && ToolBelt.Find(g.ToolBelt, g.Inventory, "spade", g.Skills) == null)
            {
                GameLog.Add("You need a spade to harvest this.");
                return false;
            }
            Farming.Harvest(State, s, DateTime.UtcNow);
            g.Inventory.Add(s.Product);
            g.Skills.AddXp(Farming.Skill, s.HarvestXp);
            GameLog.Add("You " + (Kind == PatchKind.Herb ? "pick" : "harvest") + " " + Making.Article(ContentDb.Current.Item(s.Product).Name.ToLowerInvariant()) + ".");
            Refresh();
            if (State.seed == null)
            {
                GameLog.Add("The " + DisplayName.ToLowerInvariant() + " is now empty.");
                return false;
            }
            return true;
        }

        void Update()
        {
            if (Time.time < nextCheck)
                return;
            nextCheck = Time.time + 2f;
            Refresh();
        }

        /// Rebuilds the plants when the stage (or what's planted) has changed.
        void Refresh()
        {
            var st = State;
            var s = Seed;
            int stage = s == null ? -1 : Farming.Stage(st, s, DateTime.UtcNow);
            bool yours = Yours;
            if (stage == shownStage && st.seed == shownSeed && yours == shownYours)
                return;
            shownStage = stage;
            shownSeed = st.seed;
            shownYours = yours;
            if (plants != null)
                Destroy(plants.gameObject);
            plants = null;
            if (s != null)
                plants = ModelBuilder.Build(Plants(s, stage, Kind), transform, "Plants");
            else if (!yours)
                plants = ModelBuilder.Build(Weeds, transform, "Weeds");
        }

        /// The farmer's patches you don't rent: tufts of weeds.
        static readonly Model Weeds = new Model()
            .Sphere(-0.25f, 0.18f, -0.2f, 0.22f, 0.14f, 0.2f, new Rgb(0.33f, 0.45f, 0.2f))
            .Sphere(0.2f, 0.17f, -0.25f, 0.18f, 0.12f, 0.2f, new Rgb(0.4f, 0.5f, 0.22f))
            .Sphere(0.05f, 0.2f, 0.1f, 0.26f, 0.16f, 0.24f, new Rgb(0.36f, 0.48f, 0.2f))
            .Sphere(-0.22f, 0.17f, 0.28f, 0.16f, 0.12f, 0.16f, new Rgb(0.42f, 0.52f, 0.25f))
            .Sphere(0.3f, 0.18f, 0.25f, 0.2f, 0.13f, 0.18f, new Rgb(0.34f, 0.44f, 0.2f))
            .Cylinder(0.1f, 0.3f, -0.05f, 0.03f, 0.14f, 0.03f, new Rgb(0.5f, 0.55f, 0.25f))
            .Sphere(0.1f, 0.45f, -0.05f, 0.07f, 0.07f, 0.07f, new Rgb(0.9f, 0.85f, 0.3f));  // a dandelion

        static readonly System.Collections.Generic.Dictionary<string, Model> models = new System.Collections.Generic.Dictionary<string, Model>();

        /// Rows of plants, taller each stage, showing the crop's colour once grown (one model per seed and stage).
        static Model Plants(SeedDef s, int stage, PatchKind kind)
        {
            string key = s.SeedId + stage;
            if (models.TryGetValue(key, out var m))
                return m;
            m = new Model();
            var green = new Rgb(0.3f, 0.6f, 0.22f);
            float h = 0.08f + 0.1f * Math.Min(stage, Farming.Stages);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    float x = -0.27f + i * 0.27f, z = -0.27f + j * 0.27f;
                    if (kind == PatchKind.Herb)
                        m.Sphere(x, 0.14f + h / 2f, z, 0.16f, h, 0.16f, stage >= Farming.Stages ? s.Colour : green);
                    else
                    {
                        m.Cylinder(x, 0.14f + h / 2f, z, 0.05f, h / 2f, 0.05f, green);
                        if (stage >= Farming.Stages)
                            m.Sphere(x, 0.16f + h, z, 0.14f, 0.12f, 0.14f, s.Colour);
                        else
                            m.Sphere(x, 0.16f + h, z, 0.12f, 0.06f, 0.12f, green * 1.1f);
                    }
                }
            return models[key] = m;
        }
    }
}
