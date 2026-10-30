using Epacenur.Core;

namespace Epacenur.Content
{
    /// Construction: build bases anywhere outside towns, at any angle. Saw logs into planks (a saw on the logs),
    /// smith nails at an anvil, mine limestone; then, with a hammer, place foundations, walls, doorways, windows,
    /// roofs and furniture (a storage chest, a bed to wake up in, and the crafting stations) from the build
    /// menu. Wood first; stone from Construction 20.
    public sealed class ConstructionModule : IContentModule
    {
        public const string Skill = "construction", Plank = "plank", OakPlank = "oak_plank", Nails = "nails", Limestone = "limestone",
            Saw = "saw", LimestoneRock = "limestone_rock", Chest = "storage_chest", Bed = "bed";
        public const int ChestSlots = 30;

        static readonly Rgb PlankBrown = new Rgb(0.6f, 0.44f, 0.26f), OakBrown = new Rgb(0.66f, 0.5f, 0.3f), Stone = new Rgb(0.78f, 0.75f, 0.66f);

        public void Register(ContentDb db)
        {
            db.Skill(Skill, "Construction", new Rgb(0.8f, 0.62f, 0.4f), new Icon(22f)  // a saw
                .Rect(3f, 9f, 13f, 5f, Rgb.Steel).Rect(3f, 14f, 13f, 1f, Rgb.Steel * 0.7f).Rect(15f, 7f, 5f, 9f, Rgb.Wood));

            // Materials.
            PlankItem(db, Plank, "Plank", "A plank of wood!", 10, PlankBrown);
            PlankItem(db, OakPlank, "Oak plank", "A plank of sturdy oak.", 60, OakBrown);
            var nails = db.Item(Nails, "Nails", "Good for building with.", 2, Looks.Iron);
            nails.Stackable = true;
            nails.Icon = new Icon().Rect(10f, 10f, 3f, 18f, Looks.Iron).Rect(8f, 9f, 7f, 2f, Looks.Iron * 1.2f)
                .Rect(19f, 14f, 3f, 18f, Looks.Iron * 0.9f).Rect(17f, 13f, 7f, 2f, Looks.Iron * 1.1f).Rect(27f, 8f, 3f, 18f, Looks.Iron).Rect(25f, 7f, 7f, 2f, Looks.Iron * 1.2f);
            nails.GroundModel = new Model().Cylinder(0f, 0.01f, 0f, 0.02f, 0.06f, 0.02f, Looks.Iron).Turn(90f, 0f, 0f)
                .Cylinder(0.05f, 0.01f, 0.03f, 0.02f, 0.06f, 0.02f, Looks.Iron).Turn(90f, 40f, 0f);
            var lime = db.Item(Limestone, "Limestone", "Some limestone, good for building.", 10, Stone);
            lime.Icon = new Icon().Rect(9f, 13f, 22f, 16f, Stone).Rect(12f, 10f, 15f, 4f, Stone * 1.08f).Rect(11f, 29f, 18f, 3f, Stone * 0.8f).Rect(14f, 17f, 6f, 3f, Stone * 1.12f);
            lime.GroundModel = new Model().Cube(0f, 0.08f, 0f, 0.26f, 0.16f, 0.2f, Stone).Turn(0f, 20f, 0f);

            var saw = db.Item(Saw, "Saw", "Good for cutting wood.", 13, Rgb.Steel);
            saw.ToolType = "saw";
            saw.ToolTier = 1;
            saw.Icon = new Icon().Rect(6f, 16f, 22f, 7f, Rgb.Steel).Rect(6f, 23f, 22f, 2f, Rgb.Steel * 0.7f).Rect(27f, 13f, 8f, 13f, Rgb.Wood);
            saw.HeldModel = new Model().Cube(0f, -0.02f, 0.32f, 0.01f, 0.1f, 0.5f, Rgb.Steel).Cube(0f, 0f, 0.02f, 0.04f, 0.14f, 0.12f, Looks.Handle);
            saw.GroundModel = Looks.OnGround(saw.HeldModel);

            // A saw on logs makes planks (in your hands); nails are smithed at an anvil, limestone mined.
            db.Add(new RecipeDef
            {
                Id = "saw_plank", Name = "Plank", Station = Making.InHand, Skill = Skill, Level = 1, Xp = 6, ToolType = "saw", Ticks = 2,
                Inputs = new[] { new Ingredient(WoodcuttingModule.Logs, 1) }, Product = Plank, SuccessMessage = "You saw the logs into a plank.",
            });
            db.Add(new RecipeDef
            {
                Id = "saw_oak_plank", Name = "Oak plank", Station = Making.InHand, Skill = Skill, Level = 15, Xp = 12, ToolType = "saw", Ticks = 2,
                Inputs = new[] { new Ingredient(WoodcuttingModule.OakLogs, 1) }, Product = OakPlank, SuccessMessage = "You saw the oak logs into a plank.",
            });
            foreach (var m in new[] { Metals.Bronze, Metals.Iron })
                db.Add(new RecipeDef
                {
                    Id = "smith_nails_" + m.Id, Name = "Nails (" + m.Name.ToLowerInvariant() + ")", Station = SmithingModule.Anvil, Skill = SmithingModule.Skill,
                    Level = m == Metals.Bronze ? 4 : 19, Xp = m.XpPerBar, ToolType = "hammer", Inputs = new[] { new Ingredient(m.Bar, 1) },
                    Product = Nails, ProductCount = m == Metals.Bronze ? 15 : 30, SuccessMessage = "You hammer the bar into nails.",
                });
            LimestoneNode(db);

            // The pieces: level, xp, cost. Wood from level 1, stone from 20.
            var wood = PlankBrown * 0.95f;
            var woodTrim = new Rgb(0.38f, 0.26f, 0.15f);
            var stone = new Rgb(0.66f, 0.64f, 0.6f);
            var stoneTrim = new Rgb(0.46f, 0.44f, 0.41f);
            Piece(db, "wooden_foundation", "Wooden foundation", "A floor of planks on sturdy posts.", PieceKind.Foundation, "Wood", 1, 20, wood, woodTrim,
                Of(Plank, 2), Of(Nails, 2));
            Piece(db, "wooden_wall", "Wooden wall", "A wall of planks.", PieceKind.Wall, "Wood", 1, 20, wood, woodTrim, Of(Plank, 2), Of(Nails, 2));
            Piece(db, "wooden_doorway", "Wooden doorway", "A frame to walk through.", PieceKind.Doorway, "Wood", 3, 25, wood, woodTrim, Of(Plank, 2), Of(Nails, 2));
            Piece(db, "wooden_window", "Wooden window", "A wall with a window in it.", PieceKind.Window, "Wood", 5, 28, wood, woodTrim, Of(Plank, 2), Of(Nails, 3));
            Piece(db, "wooden_roof", "Wooden roof", "Shingles to keep the rain off.", PieceKind.Roof, "Wood", 8, 35, new Rgb(0.46f, 0.28f, 0.18f), woodTrim,
                Of(Plank, 3), Of(Nails, 3));
            Piece(db, "stone_foundation", "Stone foundation", "A floor of flagstones.", PieceKind.Foundation, "Stone", 20, 45, stone, stoneTrim, Of(Limestone, 3));
            Piece(db, "stone_wall", "Stone wall", "A thick wall of limestone.", PieceKind.Wall, "Stone", 22, 50, stone, stoneTrim, Of(Limestone, 3));
            Piece(db, "stone_doorway", "Stone doorway", "An arch to walk through.", PieceKind.Doorway, "Stone", 24, 55, stone, stoneTrim, Of(Limestone, 3));
            Piece(db, "stone_window", "Stone window", "A stone wall with a window in it.", PieceKind.Window, "Stone", 26, 58, stone, stoneTrim, Of(Limestone, 3), Of(Plank, 1));
            Piece(db, "slate_roof", "Slate roof", "Grey slates on oak beams.", PieceKind.Roof, "Stone", 30, 70, new Rgb(0.36f, 0.38f, 0.44f), stoneTrim,
                Of(OakPlank, 2), Of(Limestone, 2));

            // Furniture.
            var chest = Piece(db, Chest, "Storage chest", "A sturdy chest to keep things in.", PieceKind.Furniture, "Furniture", 4, 40, wood, woodTrim,
                Of(Plank, 4), Of(Nails, 4));
            chest.Use = FurnitureUse.Chest;
            chest.Model = new Model()
                .Cube(0f, 0.25f, 0f, 0.8f, 0.5f, 0.55f, wood)
                .Cube(0f, 0.53f, 0f, 0.84f, 0.08f, 0.59f, wood * 0.85f)
                .Cube(-0.25f, 0.29f, 0f, 0.06f, 0.56f, 0.6f, Looks.Iron).Cube(0.25f, 0.29f, 0f, 0.06f, 0.56f, 0.6f, Looks.Iron)
                .Cube(0f, 0.4f, 0.29f, 0.1f, 0.12f, 0.03f, Rgb.Gold);
            chest.Icon = new Icon().Rect(7f, 14f, 26f, 17f, wood).Rect(6f, 11f, 28f, 5f, wood * 0.85f).Rect(12f, 11f, 3f, 20f, Looks.Iron).Rect(25f, 11f, 3f, 20f, Looks.Iron)
                .Rect(18f, 17f, 4f, 4f, Rgb.Gold);
            var blanket = new Rgb(0.6f, 0.2f, 0.18f);
            var bed = Piece(db, Bed, "Bed", "Somewhere to sleep. You'll wake up here.", PieceKind.Furniture, "Furniture", 10, 60, wood, woodTrim,
                Of(Plank, 3), Of(Nails, 2), Of(CraftingModule.Leather, 2));
            bed.Use = FurnitureUse.Bed;
            bed.Model = new Model()
                .Cube(0f, 0.18f, 0f, 0.8f, 0.2f, 0.95f, wood)
                .Cube(0f, 0.33f, -0.05f, 0.74f, 0.12f, 0.8f, blanket)
                .Cube(0f, 0.36f, 0.36f, 0.6f, 0.1f, 0.18f, new Rgb(0.92f, 0.9f, 0.84f))
                .Cube(0f, 0.4f, 0.47f, 0.8f, 0.5f, 0.06f, wood * 0.85f);
            bed.Icon = new Icon().Rect(5f, 18f, 30f, 8f, wood).Rect(7f, 14f, 22f, 5f, blanket).Rect(28f, 12f, 6f, 6f, new Rgb(0.92f, 0.9f, 0.84f))
                .Rect(4f, 10f, 3f, 20f, wood * 0.8f);
            Station(db, "build_spinning_wheel", "Spinning wheel", CraftingModule.SpinningWheel, 12, 50, Of(Plank, 4), Of(Nails, 2));
            Station(db, "build_tanning_rack", "Tanning rack", CraftingModule.TanningRack, 14, 55, Of(Plank, 3), Of(Nails, 3));
            Station(db, "build_furnace", "Furnace", SmithingModule.Furnace, 25, 90, Of(Limestone, 6), Of(SmithingModule.BronzeBar, 2));
            Station(db, "build_anvil", "Anvil", SmithingModule.Anvil, 30, 100, Of(Metals.Iron.Bar, 3), Of(Plank, 1));
            Station(db, "build_altar", "Altar", PrayerModule.Altar, 35, 120, Of(Limestone, 6), Of(OakPlank, 2));
        }

        static Ingredient Of(string item, int n) => new Ingredient(item, n);

        static void PlankItem(ContentDb db, string id, string name, string examine, int value, Rgb c)
        {
            var plank = db.Item(id, name, examine, value, c);
            plank.Icon = new Icon().Turned(5f, 17f, 30f, 7f, c, -20f).Turned(5f, 17f, 30f, 2f, c * 0.8f, -20f);
            plank.GroundModel = new Model().Cube(0f, 0.03f, 0f, 0.14f, 0.05f, 0.7f, c).Turn(0f, 20f, 0f);
        }

        /// Limestone: pale rocks in the quarries near towns (some quarries are mostly limestone). Mined for building stone.
        static void LimestoneNode(ContentDb db)
        {
            var pale = new Rgb(0.76f, 0.73f, 0.64f);
            var empty = new Model()
                .Sphere(0f, 0.32f, 0f, 0.95f, 0.7f, 0.85f, Looks.Rock)
                .Sphere(0.22f, 0.2f, 0.18f, 0.55f, 0.45f, 0.55f, new Rgb(0.42f, 0.4f, 0.38f));
            var full = new Model()
                .Sphere(0f, 0.34f, 0f, 1f, 0.74f, 0.9f, pale)
                .Sphere(0.25f, 0.22f, 0.2f, 0.6f, 0.5f, 0.55f, pale * 0.9f)
                .Sphere(-0.28f, 0.2f, -0.12f, 0.5f, 0.42f, 0.55f, pale * 1.06f);
            var node = db.Add(new NodeDef
            {
                Id = LimestoneRock, Name = "Limestone", Action = "Mine", Examine = "A pale rock of limestone.",
                EmptyName = "Rocks", EmptyExamine = "There is currently no limestone in this rock.",
                Skill = MiningModule.Skill, ToolType = "pickaxe",
                Yields = new[] { new NodeYield { ItemId = Limestone, Level = 1, Xp = 12, LowChance = 100, HighChance = 350, Message = "You manage to mine some limestone." } },
                AttemptTicks = 4, FirstPersonTicks = 2, MinLives = 2, MaxLives = 4, RespawnSeconds = 6f, Look = NodeLook.Rock,
                Model = full, EmptyModel = empty,
                StartMessage = "You swing your pick at the rock.",
                NeedToolMessage = "You need a pickaxe you can use to mine this rock.",
                FullMessage = "Your inventory is too full to hold any more limestone.",
                LevelMessage = "You need a Mining level of {0} to mine this rock.",
                EmptyMessage = "There is currently no limestone in this rock.",
            });
            node.Spawns.Add(new SpawnRule { On = Terrain.Rock, MaxDanger = 20, Weight = 2 });
        }

        static PieceDef Piece(ContentDb db, string id, string name, string examine, PieceKind kind, string material, int level, double xp,
            Rgb colour, Rgb trim, params Ingredient[] inputs)
        {
            var piece = db.Add(new PieceDef
            {
                Id = id, Name = name, Examine = examine, Kind = kind, Material = material, Level = level, Xp = xp,
                Inputs = inputs, Colour = colour, Trim = trim,
            });
            piece.Icon = PieceIcon(kind, colour, trim);
            return piece;
        }

        /// A crafting station you can build into a base.
        static void Station(ContentDb db, string id, string name, string station, int level, double xp, params Ingredient[] inputs)
        {
            var def = db.Station(station);
            var piece = Piece(db, id, name, def.Examine, PieceKind.Furniture, "Furniture", level, xp, new Rgb(0.5f, 0.5f, 0.5f), new Rgb(0.3f, 0.3f, 0.3f), inputs);
            piece.Use = FurnitureUse.Station;
            piece.Station = station;
            piece.Icon = new Icon().Rect(8f, 16f, 24f, 16f, new Rgb(0.5f, 0.48f, 0.45f)).Rect(6f, 12f, 28f, 5f, new Rgb(0.4f, 0.38f, 0.36f))
                .Rect(16f, 20f, 8f, 8f, new Rgb(0.95f, 0.6f, 0.2f));
        }

        /// The build menu's picture of a piece.
        static Icon PieceIcon(PieceKind kind, Rgb c, Rgb trim)
        {
            switch (kind)
            {
                case PieceKind.Foundation:
                    return new Icon().Rect(4f, 20f, 32f, 8f, c).Rect(4f, 20f, 32f, 2f, c * 1.15f).Rect(6f, 28f, 4f, 8f, trim).Rect(30f, 28f, 4f, 8f, trim);
                case PieceKind.Wall:
                    return new Icon().Rect(8f, 5f, 24f, 30f, c).Rect(8f, 5f, 24f, 3f, trim).Rect(8f, 33f, 24f, 2f, trim).Rect(19f, 8f, 1f, 25f, c * 0.8f);
                case PieceKind.Doorway:
                    return new Icon().Rect(8f, 5f, 6f, 30f, c).Rect(26f, 5f, 6f, 30f, c).Rect(8f, 5f, 24f, 6f, trim);
                case PieceKind.Window:
                    return new Icon().Rect(8f, 5f, 24f, 30f, c).Rect(14f, 12f, 12f, 11f, new Rgb(0.55f, 0.75f, 0.9f)).Rect(19.5f, 12f, 1f, 11f, trim)
                        .Rect(14f, 17f, 12f, 1f, trim);
                case PieceKind.Roof:
                    return new Icon().Turned(4f, 16f, 18f, 6f, c, -30f).Turned(18f, 16f, 18f, 6f, c * 0.85f, 30f).Rect(6f, 26f, 28f, 3f, trim);
                default:
                    return new Icon().Rect(8f, 14f, 24f, 18f, c);
            }
        }
    }
}
