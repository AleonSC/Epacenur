using Epacenur.Core;

namespace Epacenur.Content
{
    /// Crafting: pick flax and spin it into bowstrings at a spinning wheel; tan cowhides into leather at a
    /// tanning rack; sew leather armour with a needle and thread (OSRS levels and xp). Every town has a
    /// workshop with a spinning wheel and a tanning rack.
    public sealed class CraftingModule : IContentModule
    {
        public const string Skill = "crafting", Flax = "flax", FlaxPlant = "flax_plant", Leather = "leather", Needle = "needle", Thread = "thread",
            SpinningWheel = "spinning_wheel", TanningRack = "tanning_rack",
            LeatherGloves = "leather_gloves", LeatherBoots = "leather_boots", LeatherCowl = "leather_cowl", LeatherVambraces = "leather_vambraces",
            LeatherBody = "leather_body", LeatherChaps = "leather_chaps";

        static readonly Rgb LeatherBrown = new Rgb(0.55f, 0.38f, 0.22f);

        public void Register(ContentDb db)
        {
            var steel = Rgb.Steel;
            db.Skill(Skill, "Crafting", new Rgb(0.75f, 0.55f, 0.35f), new Icon(22f)  // a needle and thread
                .Turned(9.5f, 1f, 2f, 18f, steel, 40f).Rect(3f, 13f, 5f, 1.5f, new Rgb(0.85f, 0.2f, 0.2f)).Rect(3f, 14f, 1.5f, 5f, new Rgb(0.85f, 0.2f, 0.2f))
                .Rect(3f, 18f, 8f, 1.5f, new Rgb(0.85f, 0.2f, 0.2f)));

            // Flax: picked from plants in the fields (no tool, no xp), spun into bowstrings.
            var flaxBlue = new Rgb(0.45f, 0.55f, 0.85f);
            var stalk = new Rgb(0.55f, 0.6f, 0.3f);
            var flax = db.Item(Flax, "Flax", "I should use this with a spinning wheel.", 5, stalk);
            flax.Icon = new Icon().Turned(17f, 8f, 2f, 26f, stalk, 15f).Turned(21f, 8f, 2f, 26f, stalk, -10f).Turned(14f, 10f, 2f, 22f, stalk, 30f)
                .Diamond(15f, 9f, 3f, flaxBlue).Diamond(23f, 8f, 3f, flaxBlue).Diamond(11f, 13f, 2.5f, flaxBlue);
            flax.GroundModel = new Model().Cylinder(0f, 0.03f, 0f, 0.03f, 0.2f, 0.03f, stalk).Turn(90f, 10f, 0f)
                .Cylinder(0.05f, 0.03f, 0.02f, 0.03f, 0.2f, 0.03f, stalk).Turn(90f, -15f, 0f).Sphere(0.02f, 0.04f, 0.22f, 0.07f, 0.05f, 0.07f, flaxBlue);
            var plant = new Model();
            var picked = new Model();
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.26f, x = (float)System.Math.Cos(a) * 0.18f, z = (float)System.Math.Sin(a) * 0.18f;
                plant.Cylinder(x, 0.35f, z, 0.04f, 0.35f, 0.04f, stalk).Sphere(x, 0.72f, z, 0.12f, 0.08f, 0.12f, flaxBlue);
                picked.Cylinder(x, 0.08f, z, 0.04f, 0.08f, 0.04f, stalk * 0.85f);
            }
            var flaxNode = db.Add(new NodeDef
            {
                Id = FlaxPlant, Name = "Flax", Action = "Pick", Examine = "A flax plant.",
                EmptyName = "Picked flax", EmptyExamine = "It has all been picked.",
                Skill = Skill,
                Yields = new[] { new NodeYield { ItemId = Flax, Level = 1, Xp = 0, LowChance = 256, HighChance = 256, Message = "You pick some flax." } },
                AttemptTicks = 3, FirstPersonTicks = 2, MinLives = 1, MaxLives = 4, RespawnSeconds = 15f, Look = NodeLook.Plant,
                Model = plant, EmptyModel = picked,
                StartMessage = "You start picking flax.",
                FullMessage = "You can't carry any more flax.",
                LevelMessage = "You need a Crafting level of {0} to pick this.",
            });
            flaxNode.Spawns.Add(new SpawnRule { On = Terrain.Flax });

            // Leather, and what you sew it with.
            var leather = db.Item(Leather, "Leather", "It's a piece of leather.", 10, LeatherBrown);
            leather.Icon = new Icon().Rect(9f, 10f, 22f, 20f, LeatherBrown).Rect(7f, 8f, 4f, 4f, LeatherBrown).Rect(29f, 28f, 4f, 4f, LeatherBrown)
                .Rect(12f, 13f, 16f, 1f, LeatherBrown * 0.8f);
            leather.GroundModel = new Model().Cube(0f, 0.015f, 0f, 0.45f, 0.03f, 0.36f, LeatherBrown);
            var needle = db.Item(Needle, "Needle", "Used with a thread to make clothes.", 1, steel);
            needle.ToolType = "needle";
            needle.ToolTier = 1;
            needle.Icon = new Icon().Turned(19f, 5f, 2f, 30f, steel, 35f);
            needle.GroundModel = new Model().Cylinder(0f, 0.01f, 0f, 0.012f, 0.12f, 0.012f, steel).Turn(90f, 30f, 0f);
            var threadWhite = new Rgb(0.92f, 0.9f, 0.85f);
            var thread = db.Item(Thread, "Thread", "Use with a needle to make clothes.", 1, threadWhite);
            thread.Stackable = true;
            thread.Icon = new Icon().Rect(13f, 12f, 14f, 16f, threadWhite).Rect(11f, 10f, 18f, 3f, Rgb.Wood).Rect(11f, 27f, 18f, 3f, Rgb.Wood);
            thread.GroundModel = new Model().Cylinder(0f, 0.06f, 0f, 0.1f, 0.06f, 0.1f, threadWhite);

            // The workshop's spinning wheel and tanning rack.
            var wood = new Rgb(0.5f, 0.34f, 0.2f);
            db.Add(new StationDef
            {
                Id = SpinningWheel, Name = "Spinning wheel", Action = "Spin", Examine = "Used for spinning thread.", Kind = StationKind.Make,
                Label = "Spinning wheel", LabelHeight = 1.6f,
                Model = new Model()
                    .Cube(0f, 0.2f, 0f, 0.6f, 0.08f, 0.3f, wood)
                    .Cube(-0.25f, 0.1f, 0f, 0.06f, 0.2f, 0.25f, wood).Cube(0.25f, 0.1f, 0f, 0.06f, 0.2f, 0.25f, wood)
                    .Cube(0.15f, 0.55f, 0f, 0.05f, 0.7f, 0.05f, wood)
                    .Cylinder(0.15f, 0.75f, 0f, 0.6f, 0.02f, 0.6f, wood * 1.1f).Turn(90f, 0f, 0f)
                    .Cylinder(0.15f, 0.75f, 0f, 0.48f, 0.025f, 0.48f, wood * 0.7f).Turn(90f, 0f, 0f)
                    .Cylinder(-0.18f, 0.4f, 0f, 0.08f, 0.12f, 0.08f, new Rgb(0.85f, 0.82f, 0.75f)),
            });
            var hide = new Rgb(0.82f, 0.78f, 0.68f);
            db.Add(new StationDef
            {
                Id = TanningRack, Name = "Tanning rack", Action = "Tan", Examine = "Hides are stretched and tanned here, for a coin each.",
                Kind = StationKind.Make, Label = "Tanning rack", LabelHeight = 1.9f,
                Help = "Tanning a cowhide into leather costs 1 coin.",
                Model = new Model()
                    .Cylinder(-0.42f, 0.75f, 0f, 0.06f, 0.75f, 0.06f, wood).Cylinder(0.42f, 0.75f, 0f, 0.06f, 0.75f, 0.06f, wood)
                    .Cube(0f, 1.42f, 0f, 0.95f, 0.07f, 0.07f, wood).Cube(0f, 0.25f, 0f, 0.95f, 0.07f, 0.07f, wood)
                    .Cube(0f, 0.85f, 0f, 0.72f, 1.05f, 0.03f, hide)
                    .Cube(0.12f, 1f, -0.02f, 0.22f, 0.18f, 0.01f, Looks.Dark).Cube(-0.15f, 0.65f, -0.02f, 0.2f, 0.16f, 0.01f, Looks.Dark),
            });

            db.Add(new RecipeDef
            {
                Id = "spin_bowstring", Name = "Bowstring", Station = SpinningWheel, Skill = Skill, Level = 10, Xp = 15, Ticks = 3,
                Inputs = new[] { new Ingredient(Flax, 1) }, Product = FletchingModule.Bowstring,
                SuccessMessage = "You spin the flax into a bowstring.",
            });
            db.Add(new RecipeDef
            {
                Id = "tan_leather", Name = "Leather", Station = TanningRack, Skill = Skill, Level = 1, Xp = 0, Ticks = 1, Batch = 28,
                Inputs = new[] { new Ingredient(CombatModule.Cowhide, 1), new Ingredient(ItemIds.Coins, 1) }, Product = Leather,
                SuccessMessage = "You tan the hides into leather.",
            });

            // Leather armour (OSRS levels, xp and bonuses): good for rangers, some ranged accuracy and defence.
            var c = LeatherBrown;
            var gloves = Armour(db, LeatherGloves, "Leather gloves", "These will keep my hands warm!", 6, EquipSlot.Hands, 1, 13.8,
                new Bonuses { StabDefence = 1, SlashDefence = 1, CrushDefence = 1, RangedDefence = 1 },
                new Icon().Rect(10f, 12f, 9f, 16f, c).Rect(21f, 12f, 9f, 16f, c).Rect(8f, 10f, 3f, 6f, c).Rect(29f, 10f, 3f, 6f, c));
            var glove = new Model().Sphere(0f, -0.58f, 0.01f, 0.13f, 0.13f, 0.13f, c);
            gloves.Worn.Add(new WornPart { Bone = Bone.ArmLeft, Model = glove });
            gloves.Worn.Add(new WornPart { Bone = Bone.ArmRight, Model = glove });

            var boots = Armour(db, LeatherBoots, "Leather boots", "Comfortable leather boots.", 6, EquipSlot.Feet, 7, 16.25,
                new Bonuses { StabDefence = 1, SlashDefence = 1, CrushDefence = 1, RangedDefence = 1 },
                new Icon().Rect(9f, 10f, 8f, 18f, c).Rect(9f, 26f, 13f, 6f, c).Rect(23f, 10f, 8f, 18f, c * 0.9f).Rect(23f, 26f, 13f, 6f, c * 0.9f));
            var boot = new Model().Cube(0f, -0.78f, 0.04f, 0.18f, 0.16f, 0.31f, c).Cylinder(0f, -0.66f, 0f, 0.2f, 0.06f, 0.2f, c * 0.85f);
            boots.Worn.Add(new WornPart { Bone = Bone.LegLeft, Model = boot });
            boots.Worn.Add(new WornPart { Bone = Bone.LegRight, Model = boot });

            var cowl = Armour(db, LeatherCowl, "Coif", "Made from leather. Protects the head.", 24, EquipSlot.Head, 9, 18.5,
                new Bonuses { StabDefence = 2, SlashDefence = 3, CrushDefence = 4, RangedDefence = 3, RangedAttack = 2 },
                new Icon().Rect(11f, 8f, 18f, 6f, c).Rect(9f, 13f, 22f, 18f, c).Rect(14f, 17f, 12f, 10f, new Rgb(0.93f, 0.76f, 0.6f)));
            cowl.Worn.Add(new WornPart { Bone = Bone.Body, Model = new Model()  // a hood round the head, the face showing
                .Sphere(0f, 1.73f, -0.03f, 0.38f, 0.39f, 0.38f, c)
                .Cylinder(0f, 1.48f, 0f, 0.28f, 0.06f, 0.26f, c * 0.9f) });

            var vambraces = Armour(db, LeatherVambraces, "Leather vambraces", "Leather vambraces, for archers.", 18, EquipSlot.Hands, 11, 22,
                new Bonuses { StabDefence = 2, SlashDefence = 2, CrushDefence = 2, RangedDefence = 2, RangedAttack = 4 },
                new Icon().Rect(10f, 8f, 8f, 24f, c).Rect(22f, 8f, 8f, 24f, c).Rect(10f, 14f, 8f, 2f, c * 0.75f).Rect(22f, 14f, 8f, 2f, c * 0.75f));
            var bracer = new Model().Capsule(0f, -0.45f, 0f, 0.15f, 0.13f, 0.15f, c);
            vambraces.Worn.Add(new WornPart { Bone = Bone.ArmLeft, Model = bracer });
            vambraces.Worn.Add(new WornPart { Bone = Bone.ArmRight, Model = bracer });

            var body = Armour(db, LeatherBody, "Leather body", "Better than no armour!", 21, EquipSlot.Body, 14, 25,
                new Bonuses { StabDefence = 8, SlashDefence = 9, CrushDefence = 10, RangedDefence = 9, RangedAttack = 2 },
                new Icon().Rect(7f, 9f, 26f, 7f, c).Rect(11f, 15f, 18f, 18f, c).Rect(19.5f, 15f, 1f, 18f, c * 0.75f)
                    .Rect(7f, 16f, 3f, 10f, c * 0.9f).Rect(30f, 16f, 3f, 10f, c * 0.9f));
            body.Worn.Add(new WornPart { Bone = Bone.Body, Model = new Model()
                .Capsule(0f, 1.2f, 0f, 0.53f, 0.39f, 0.35f, c)
                .Cube(0f, 1.24f, 0.17f, 0.03f, 0.3f, 0.03f, c * 0.75f) });

            var chaps = Armour(db, LeatherChaps, "Leather chaps", "Better than no armour!", 20, EquipSlot.Legs, 18, 27,
                new Bonuses { StabDefence = 2, SlashDefence = 2, CrushDefence = 2, RangedDefence = 4, RangedAttack = 4 },
                new Icon().Rect(11f, 7f, 18f, 6f, c).Rect(11f, 13f, 7f, 21f, c).Rect(22f, 13f, 7f, 21f, c));
            var chap = new Model().Capsule(0f, -0.3f, 0f, 0.2f, 0.32f, 0.2f, c);
            chaps.Worn.Add(new WornPart { Bone = Bone.Body, Model = new Model().Cube(0f, 0.88f, 0f, 0.42f, 0.16f, 0.26f, c) });
            chaps.Worn.Add(new WornPart { Bone = Bone.LegLeft, Model = chap });
            chaps.Worn.Add(new WornPart { Bone = Bone.LegRight, Model = chap });
        }

        /// A leather item, sewn from a piece of leather with a needle (and a thread used up).
        static ItemDef Armour(ContentDb db, string id, string name, string examine, int value, EquipSlot slot, int level, double xp, Bonuses bonuses, Icon icon)
        {
            var c = LeatherBrown;
            var item = db.Item(id, name, examine, value, c);
            item.Slot = slot;
            item.Bonuses = bonuses;
            item.Icon = icon;
            item.GroundModel = new Model().Cube(0f, 0.03f, 0f, 0.36f, 0.06f, 0.32f, c).Cube(0f, 0.05f, 0.12f, 0.2f, 0.03f, 0.12f, c * 0.85f);
            db.Add(new RecipeDef
            {
                Id = "craft_" + id, Name = name, Station = Making.InHand, Skill = Skill, Level = level, Xp = xp, ToolType = "needle", Ticks = 3,
                Inputs = new[] { new Ingredient(Leather, 1), new Ingredient(Thread, 1) }, Product = id,
                SuccessMessage = "You make " + Making.Article(name) + ".",
            });
            return item;
        }
    }
}
