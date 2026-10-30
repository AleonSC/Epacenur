using Epacenur.Core;

namespace Epacenur.Content
{
    /// Smithing: smelt copper and tin into bronze bars at a furnace, then hammer bars into armour at an anvil.
    public sealed class SmithingModule : IContentModule
    {
        public const string Skill = "smithing", BronzeBar = "bronze_bar", Hammer = "hammer",
            BronzeDagger = "bronze_dagger", BronzeMace = "bronze_mace", BronzeScimitar = "bronze_scimitar", Bronze2h = "bronze_2h_sword",
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
            var helm = Armour(db, BronzeFullHelm, "Bronze full helm", "A full face helmet.", 44, EquipSlot.Head, new Bonuses { StabDefence = 4, SlashDefence = 5, CrushDefence = 3 });
            helm.Icon = new Icon().Rect(11f, 9f, 18f, 4f, c).Rect(9f, 13f, 22f, 18f, c).Rect(12f, 18f, 16f, 3f, Looks.Dark).Rect(19f, 21f, 2f, 9f, c * 0.75f);
            helm.GroundModel = new Model().Sphere(0f, 0.1f, 0f, 0.3f, 0.26f, 0.3f, c).Cube(0f, 0.08f, 0.13f, 0.2f, 0.16f, 0.04f, c);
            helm.Worn.Add(new WornPart { Bone = Bone.Body, Model = new Model()  // a dome over the head, face guard, eye slit and crest
                .Sphere(0f, 1.73f, -0.01f, 0.39f, 0.4f, 0.39f, c)
                .Cube(0f, 1.66f, 0.17f, 0.28f, 0.24f, 0.04f, c)
                .Cube(0f, 1.71f, 0.19f, 0.2f, 0.035f, 0.01f, Looks.Dark)
                .Cube(0f, 1.9f, 0f, 0.05f, 0.06f, 0.34f, c * 0.85f) });

            var body = Armour(db, BronzePlatebody, "Bronze platebody", "Provides excellent protection.", 160, EquipSlot.Body, new Bonuses { StabDefence = 15, SlashDefence = 14, CrushDefence = 9 });
            body.Icon = new Icon().Rect(7f, 9f, 26f, 7f, c).Rect(11f, 15f, 18f, 18f, c).Rect(19.5f, 15f, 1f, 18f, c * 0.75f)
                .Rect(7f, 16f, 3f, 10f, c * 0.85f).Rect(30f, 16f, 3f, 10f, c * 0.85f);
            body.GroundModel = new Model().Cube(0f, 0.05f, 0f, 0.4f, 0.1f, 0.46f, c).Cube(0f, 0.05f, 0.24f, 0.56f, 0.08f, 0.12f, c * 0.9f);
            body.Worn.Add(new WornPart { Bone = Bone.Body, Model = new Model()  // a bronze shell over the tunic, a ridge down the front
                .Capsule(0f, 1.21f, 0f, 0.54f, 0.39f, 0.36f, c)
                .Cube(0f, 1.24f, 0.175f, 0.04f, 0.34f, 0.03f, c * 0.8f)
                .Cylinder(0f, 0.95f, 0f, 0.52f, 0.06f, 0.36f, c * 0.85f) });

            var legs = Armour(db, BronzePlatelegs, "Bronze platelegs", "Big, bronze and heavy looking.", 80, EquipSlot.Legs, new Bonuses { StabDefence = 8, SlashDefence = 7, CrushDefence = 4 });
            legs.Icon = new Icon().Rect(11f, 7f, 18f, 6f, c).Rect(11f, 13f, 7f, 21f, c).Rect(22f, 13f, 7f, 21f, c).Rect(11f, 12f, 18f, 1f, c * 0.7f);
            legs.GroundModel = new Model().Cube(0f, 0.05f, 0.18f, 0.36f, 0.1f, 0.12f, c)
                .Cube(-0.1f, 0.05f, -0.06f, 0.13f, 0.09f, 0.42f, c).Cube(0.1f, 0.05f, -0.06f, 0.13f, 0.09f, 0.42f, c);
            var legPlates = new Model().Capsule(0f, -0.22f, 0f, 0.2f, 0.25f, 0.2f, c).Capsule(0f, -0.54f, 0f, 0.175f, 0.22f, 0.175f, c);
            legs.Worn.Add(new WornPart { Bone = Bone.Body, Model = new Model().Cube(0f, 0.88f, 0f, 0.43f, 0.18f, 0.27f, c) });
            legs.Worn.Add(new WornPart { Bone = Bone.LegLeft, Model = legPlates });
            legs.Worn.Add(new WornPart { Bone = Bone.LegRight, Model = legPlates });

            Weapons(db);

            // The furnace (its mouth glows and lights the ground) and the anvil.
            var stone = new Rgb(0.45f, 0.42f, 0.4f);
            var soot = new Rgb(0.28f, 0.26f, 0.25f);
            db.Add(new StationDef
            {
                Id = Furnace, Name = "Furnace", Action = "Smelt", Examine = "A red hot furnace.", Kind = StationKind.Make,
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
                Id = Anvil, Name = "Anvil", Action = "Smith", Examine = "Used for fashioning metal items.", Kind = StationKind.Make,
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
            Smith(db, "smith_bronze_dagger", BronzeDagger, 1, 1);
            Smith(db, "smith_bronze_mace", BronzeMace, 1, 2);
            Smith(db, "smith_bronze_sword", CombatModule.BronzeSword, 1, 4);
            Smith(db, "smith_bronze_scimitar", BronzeScimitar, 2, 5);
            Smith(db, "smith_bronze_full_helm", BronzeFullHelm, 2, 7);
            Smith(db, "smith_bronze_2h_sword", Bronze2h, 3, 14);
            Smith(db, "smith_bronze_platelegs", BronzePlatelegs, 3, 16);
            Smith(db, "smith_bronze_platebody", BronzePlatebody, 5, 18);
        }

        static ItemDef Armour(ContentDb db, string id, string name, string examine, int value, EquipSlot slot, Bonuses bonuses)
        {
            var item = db.Item(id, name, examine, value, Looks.Bronze);
            item.Slot = slot;
            item.Bonuses = bonuses;
            return item;
        }

        /// A weapon: held pointing +z from the grip, lying flat on the ground.
        static ItemDef Weapon(ContentDb db, string id, string name, string examine, int value, string kind, int ticks, Bonuses bonuses, Icon icon, Model held)
        {
            var item = db.Item(id, name, examine, value, Looks.Bronze);
            item.Slot = EquipSlot.Weapon;
            item.WeaponKind = kind;
            item.AttackTicks = ticks;
            item.Bonuses = bonuses;
            item.Icon = icon;
            item.HeldModel = held;
            item.GroundModel = Looks.OnGround(held);
            return item;
        }

        static void Weapons(ContentDb db)
        {
            var c = Looks.Bronze;
            var light = new Rgb(0.9f, 0.7f, 0.45f);
            var grip = new Rgb(0.35f, 0.22f, 0.12f);
            var guard = new Rgb(0.4f, 0.4f, 0.45f);
            // OSRS stats. Daggers are fast stabbers, maces crush, scimitars slash, a two-handed sword hits hard but slowly.
            Weapon(db, BronzeDagger, "Bronze dagger", "Short but pointy.", 10, CombatModule.Dagger, 4,
                new Bonuses { StabAttack = 4, SlashAttack = 2, CrushAttack = -4, SlashDefence = 1, Strength = 3 },
                new Icon().Rect(18f, 10f, 4f, 15f, c).Rect(19f, 10f, 1.5f, 15f, light).Rect(14f, 25f, 12f, 3f, guard).Rect(18f, 28f, 4f, 6f, grip),
                new Model().Cube(0f, 0f, 0.05f, 0.04f, 0.04f, 0.12f, grip).Cube(0f, 0f, 0.12f, 0.14f, 0.03f, 0.03f, guard)
                    .Cube(0f, 0f, 0.27f, 0.05f, 0.012f, 0.28f, c));
            Weapon(db, BronzeMace, "Bronze mace", "A spiky mace.", 18, CombatModule.Mace, 5,
                new Bonuses { StabAttack = 1, SlashAttack = -2, CrushAttack = 6, SlashDefence = 1, Strength = 5 },
                new Icon().Turned(18.5f, 12f, 3f, 24f, Rgb.Wood, -35f).TurnedAbout(14f, 6f, 10f, 10f, c, -35f, 20f, 20f)
                    .Diamond(13f, 9f, 3f, light),
                new Model().Cylinder(0f, 0f, 0.2f, 0.04f, 0.2f, 0.04f, Looks.Handle).Turn(90f, 0f, 0f)
                    .Sphere(0f, 0f, 0.45f, 0.14f, 0.14f, 0.16f, c)
                    .Cube(0f, 0.08f, 0.45f, 0.03f, 0.06f, 0.03f, c).Cube(0f, -0.08f, 0.45f, 0.03f, 0.06f, 0.03f, c)
                    .Cube(0.08f, 0f, 0.45f, 0.06f, 0.03f, 0.03f, c).Cube(-0.08f, 0f, 0.45f, 0.06f, 0.03f, 0.03f, c));
            Weapon(db, BronzeScimitar, "Bronze scimitar", "A vicious, curved sword.", 32, CombatModule.Scimitar, 4,
                new Bonuses { StabAttack = 1, SlashAttack = 7, CrushAttack = -2, SlashDefence = 1, Strength = 6 },
                new Icon().Turned(20f, 4f, 5f, 12f, c, 12f).Rect(18f, 14f, 5f, 12f, c).Rect(19f, 14f, 1.5f, 12f, light)
                    .Rect(12f, 26f, 17f, 3f, guard).Rect(18f, 29f, 5f, 6f, grip),
                new Model().Cube(0f, 0f, 0.05f, 0.04f, 0.04f, 0.14f, grip).Cube(0f, 0f, 0.13f, 0.18f, 0.04f, 0.04f, guard)
                    .Cube(0f, 0f, 0.33f, 0.07f, 0.015f, 0.36f, c)
                    .Cube(0f, 0.03f, 0.56f, 0.07f, 0.015f, 0.14f, c).Turn(-18f, 0f, 0f));
            var two = Weapon(db, Bronze2h, "Bronze 2h sword", "A two-handed sword.", 80, CombatModule.TwoHandedSword, 7,
                new Bonuses { StabAttack = -4, SlashAttack = 9, CrushAttack = 8, SlashDefence = 1, Strength = 11 },
                new Icon().Rect(18.5f, 2f, 5f, 25f, c).Rect(19.5f, 2f, 2f, 25f, light).Rect(10f, 27f, 21f, 3f, guard).Rect(18.5f, 30f, 5f, 8f, grip),
                new Model().Cube(0f, 0f, 0.08f, 0.045f, 0.045f, 0.22f, grip).Cube(0f, 0f, 0.2f, 0.28f, 0.05f, 0.05f, guard)
                    .Cube(0f, 0f, 0.62f, 0.08f, 0.018f, 0.82f, c));
            two.TwoHanded = true;
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
