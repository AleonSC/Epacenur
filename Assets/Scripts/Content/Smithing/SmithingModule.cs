using Epacenur.Core;

namespace Epacenur.Content
{
    /// Smithing: smelt copper and tin into bronze bars at a furnace, then hammer bars into armour at an anvil.
    public sealed class SmithingModule : IContentModule
    {
        public const string Skill = "smithing", BronzeBar = "bronze_bar", Hammer = "hammer",
            BronzeFullHelm = "bronze_full_helm", BronzePlatebody = "bronze_platebody", BronzePlatelegs = "bronze_platelegs",
            Furnace = "furnace", Anvil = "anvil";

        public void Register(ContentDb db)
        {
            db.Skill(Skill, "Smithing", new Rgb(0.85f, 0.6f, 0.3f), new Icon(22f)  // an anvil
                .Rect(2f, 6f, 15f, 4f, Looks.Iron).Rect(17f, 7f, 4f, 2f, Looks.Iron).Rect(7f, 10f, 6f, 5f, Looks.Iron)
                .Rect(4f, 15f, 12f, 4f, Rgb.Wood));

            var c = Looks.Bronze;
            var bar = db.Item(BronzeBar, "Bronze bar", "It's a bar of bronze.", 8, c);
            bar.Icon = new Icon().Rect(8f, 22f, 24f, 8f, c * 0.8f).Rect(11f, 15f, 18f, 7f, c).Rect(12f, 16f, 16f, 2f, c * 1.3f);
            bar.GroundModel = new Model().Cube(0f, 0.05f, 0f, 0.32f, 0.1f, 0.14f, c);

            var grey = new Rgb(0.55f, 0.56f, 0.6f);
            var hammer = db.Item(Hammer, "Hammer", "Good for hitting things.", 1, grey);
            hammer.ToolType = "hammer";
            hammer.ToolTier = 1;
            hammer.Icon = new Icon().Turned(18.5f, 10f, 3f, 24f, Rgb.Wood, -35f).Turned(12f, 6f, 16f, 7f, grey, -35f);
            hammer.HeldModel = new Model()
                .Cube(0f, 0f, 0.18f, 0.04f, 0.04f, 0.42f, Looks.Handle)
                .Cube(0f, 0f, 0.4f, 0.08f, 0.2f, 0.08f, new Rgb(0.5f, 0.52f, 0.56f));
            hammer.GroundModel = Looks.OnGround(hammer.HeldModel);

            // Bronze armour. Worn parts are placed on the player's body (see Bone).
            var helm = Armour(db, BronzeFullHelm, "Bronze full helm", "A full face helmet.", 44, EquipSlot.Head, 4);
            helm.Icon = new Icon().Rect(11f, 9f, 18f, 4f, c).Rect(9f, 13f, 22f, 18f, c).Rect(12f, 18f, 16f, 3f, Looks.Dark).Rect(19f, 21f, 2f, 9f, c * 0.75f);
            helm.GroundModel = new Model().Sphere(0f, 0.1f, 0f, 0.3f, 0.26f, 0.3f, c).Cube(0f, 0.08f, 0.13f, 0.2f, 0.16f, 0.04f, c);
            helm.Worn.Add(new WornPart { Bone = Bone.Body, Model = new Model()  // a dome over the head, face guard, eye slit and crest
                .Sphere(0f, 1.73f, -0.01f, 0.39f, 0.4f, 0.39f, c)
                .Cube(0f, 1.66f, 0.17f, 0.28f, 0.24f, 0.04f, c)
                .Cube(0f, 1.71f, 0.19f, 0.2f, 0.035f, 0.01f, Looks.Dark)
                .Cube(0f, 1.9f, 0f, 0.05f, 0.06f, 0.34f, c * 0.85f) });

            var body = Armour(db, BronzePlatebody, "Bronze platebody", "Provides excellent protection.", 160, EquipSlot.Body, 15);
            body.Icon = new Icon().Rect(7f, 9f, 26f, 7f, c).Rect(11f, 15f, 18f, 18f, c).Rect(19.5f, 15f, 1f, 18f, c * 0.75f)
                .Rect(7f, 16f, 3f, 10f, c * 0.85f).Rect(30f, 16f, 3f, 10f, c * 0.85f);
            body.GroundModel = new Model().Cube(0f, 0.05f, 0f, 0.4f, 0.1f, 0.46f, c).Cube(0f, 0.05f, 0.24f, 0.56f, 0.08f, 0.12f, c * 0.9f);
            body.Worn.Add(new WornPart { Bone = Bone.Body, Model = new Model()  // a bronze shell over the tunic, a ridge down the front
                .Capsule(0f, 1.21f, 0f, 0.54f, 0.39f, 0.36f, c)
                .Cube(0f, 1.24f, 0.175f, 0.04f, 0.34f, 0.03f, c * 0.8f)
                .Cylinder(0f, 0.95f, 0f, 0.52f, 0.06f, 0.36f, c * 0.85f) });

            var legs = Armour(db, BronzePlatelegs, "Bronze platelegs", "Big, bronze and heavy looking.", 80, EquipSlot.Legs, 8);
            legs.Icon = new Icon().Rect(11f, 7f, 18f, 6f, c).Rect(11f, 13f, 7f, 21f, c).Rect(22f, 13f, 7f, 21f, c).Rect(11f, 12f, 18f, 1f, c * 0.7f);
            legs.GroundModel = new Model().Cube(0f, 0.05f, 0.18f, 0.36f, 0.1f, 0.12f, c)
                .Cube(-0.1f, 0.05f, -0.06f, 0.13f, 0.09f, 0.42f, c).Cube(0.1f, 0.05f, -0.06f, 0.13f, 0.09f, 0.42f, c);
            var legPlates = new Model().Capsule(0f, -0.22f, 0f, 0.2f, 0.25f, 0.2f, c).Capsule(0f, -0.54f, 0f, 0.175f, 0.22f, 0.175f, c);
            legs.Worn.Add(new WornPart { Bone = Bone.Body, Model = new Model().Cube(0f, 0.88f, 0f, 0.43f, 0.18f, 0.27f, c) });
            legs.Worn.Add(new WornPart { Bone = Bone.LegLeft, Model = legPlates });
            legs.Worn.Add(new WornPart { Bone = Bone.LegRight, Model = legPlates });

            // The furnace (its mouth glows and lights the ground) and the anvil.
            var stone = new Rgb(0.45f, 0.42f, 0.4f);
            var soot = new Rgb(0.28f, 0.26f, 0.25f);
            db.Add(new StationDef
            {
                Id = Furnace, Name = "Furnace", Action = "Smelt", Examine = "A red hot furnace.", Kind = StationKind.MakeAll,
                HasLight = true, LightColour = new Rgb(1f, 0.55f, 0.2f), Label = "Furnace", LabelHeight = 2.3f,
                Model = new Model()
                    .Cube(0f, 0.6f, 0f, 0.95f, 1.2f, 0.9f, stone)
                    .Cube(0f, 1.25f, 0f, 0.8f, 0.12f, 0.75f, soot)
                    .Cylinder(0.18f, 1.6f, 0.15f, 0.3f, 0.35f, 0.3f, soot)
                    .Cube(0f, 0.45f, -0.44f, 0.5f, 0.42f, 0.04f, new Rgb(0.12f, 0.08f, 0.06f))
                    .Cube(0f, 0.38f, -0.43f, 0.4f, 0.22f, 0.05f, new Rgb(1f, 0.45f, 0.1f)).Glowing(),
            });
            var iron = new Rgb(0.25f, 0.26f, 0.28f);
            db.Add(new StationDef
            {
                Id = Anvil, Name = "Anvil", Action = "Smith", Examine = "Used for fashioning metal items.", Kind = StationKind.MakeWindow,
                Label = "Anvil", LabelHeight = 1.3f,
                Help = "Smelt bronze bars at the furnace (1 copper ore + 1 tin ore each). Each bar smithed gives 12.5 Smithing xp.",
                Model = new Model()
                    .Cylinder(0f, 0.2f, 0f, 0.6f, 0.2f, 0.6f, new Rgb(0.4f, 0.27f, 0.15f))
                    .Cube(0f, 0.5f, 0f, 0.22f, 0.22f, 0.3f, iron)
                    .Cube(0f, 0.68f, 0f, 0.7f, 0.16f, 0.32f, iron)
                    .Cylinder(0.42f, 0.68f, 0f, 0.12f, 0.1f, 0.12f, iron).Turn(0f, 0f, 90f),
            });

            // Smelting: 1 copper ore + 1 tin ore = a bronze bar (6.2 xp). Smithing: 12.5 xp per bar used.
            db.Add(new RecipeDef
            {
                Id = "smelt_bronze_bar", Name = "Bronze bar", Station = Furnace, Skill = Skill, Level = 1, Xp = 6.2,
                Inputs = new[] { new Ingredient(MiningModule.CopperOre, 1), new Ingredient(MiningModule.TinOre, 1) }, Product = BronzeBar,
                SuccessMessage = "You smelt the copper and tin together in the furnace. You retrieve a bar of bronze.",
            });
            Smith(db, "smith_bronze_full_helm", BronzeFullHelm, 2, 7);
            Smith(db, "smith_bronze_platelegs", BronzePlatelegs, 3, 16);
            Smith(db, "smith_bronze_platebody", BronzePlatebody, 5, 18);
        }

        static ItemDef Armour(ContentDb db, string id, string name, string examine, int value, EquipSlot slot, int defence)
        {
            var item = db.Item(id, name, examine, value, Looks.Bronze);
            item.Slot = slot;
            item.DefenceBonus = defence;
            return item;
        }

        static void Smith(ContentDb db, string id, string product, int bars, int level)
        {
            string name = db.Item(product).Name;
            db.Add(new RecipeDef
            {
                Id = id, Name = name, Station = Anvil, Skill = Skill, Level = level, Xp = 12.5 * bars, ToolType = "hammer",
                Inputs = new[] { new Ingredient(BronzeBar, bars) }, Product = product,
                SuccessMessage = "You hammer the bronze and make " + (name.EndsWith("s") ? "some " : "a ") + name.ToLowerInvariant() + ".",
            });
        }
    }
}
