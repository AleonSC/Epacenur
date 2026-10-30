using Epacenur.Core;

namespace Epacenur.Content
{
    /// Fletching: cut logs with a knife into arrow shafts or unstrung bows, string the bows, feather the shafts
    /// and tip them with smithed arrowtips (OSRS levels and xp). All made in your hands from the inventory
    /// (Making.InHand): use the knife on logs, a bowstring on a bow, feathers on shafts, arrowtips on headless arrows.
    public sealed class FletchingModule : IContentModule
    {
        public const string Skill = "fletching", Knife = "knife", Bowstring = "bowstring", ArrowShaft = "arrow_shaft",
            HeadlessArrow = "headless_arrow", Shortbow = "shortbow", Longbow = "longbow", BronzeArrow = "bronze_arrow";

        /// The woods bows are made from, with OSRS stats: ranged accuracy, the Ranged level to use, the
        /// best arrows they fire (a metal tier), and the levels and xp to fletch the short and long bow.
        struct Wood
        {
            public string Prefix, Name, Logs; public Rgb Colour; public int Attack, Ranged, AmmoTier, ShortLevel, LongLevel, Shafts, ShaftLevel;
            public double ShortXp, LongXp, ShaftXp;
        }

        static readonly Wood[] Woods =
        {
            new Wood { Prefix = "", Name = "", Logs = WoodcuttingModule.Logs, Colour = new Rgb(0.55f, 0.36f, 0.18f), Attack = 8, Ranged = 1, AmmoTier = 2,
                ShortLevel = 5, ShortXp = 5, LongLevel = 10, LongXp = 10, Shafts = 15, ShaftLevel = 1, ShaftXp = 5 },
            new Wood { Prefix = "oak_", Name = "Oak ", Logs = WoodcuttingModule.OakLogs, Colour = new Rgb(0.62f, 0.46f, 0.25f), Attack = 14, Ranged = 5, AmmoTier = 3,
                ShortLevel = 20, ShortXp = 16.5, LongLevel = 25, LongXp = 25, Shafts = 30, ShaftLevel = 15, ShaftXp = 10 },
            new Wood { Prefix = "willow_", Name = "Willow ", Logs = WoodcuttingModule.WillowLogs, Colour = new Rgb(0.6f, 0.52f, 0.32f), Attack = 20, Ranged = 20, AmmoTier = 4,
                ShortLevel = 35, ShortXp = 33.3, LongLevel = 40, LongXp = 41.5, Shafts = 45, ShaftLevel = 30, ShaftXp = 15 },
            new Wood { Prefix = "maple_", Name = "Maple ", Logs = WoodcuttingModule.MapleLogs, Colour = new Rgb(0.65f, 0.35f, 0.16f), Attack = 29, Ranged = 30, AmmoTier = 5,
                ShortLevel = 50, ShortXp = 50, LongLevel = 55, LongXp = 58.3, Shafts = 60, ShaftLevel = 45, ShaftXp = 20 },
            new Wood { Prefix = "yew_", Name = "Yew ", Logs = WoodcuttingModule.YewLogs, Colour = new Rgb(0.42f, 0.24f, 0.14f), Attack = 47, Ranged = 40, AmmoTier = 5,
                ShortLevel = 65, ShortXp = 67.5, LongLevel = 70, LongXp = 75, Shafts = 75, ShaftLevel = 60, ShaftXp = 25 },
        };

        // Arrows by metal (OSRS): ranged strength, fletching level, xp each.
        static readonly int[] ArrowStrength = { 7, 10, 16, 22, 31 };
        static readonly int[] ArrowLevel = { 1, 15, 30, 45, 60 };
        static readonly double[] ArrowXp = { 1.3, 2.5, 5, 7.5, 10 };

        public void Register(ContentDb db)
        {
            var feather = new Rgb(0.95f, 0.95f, 0.93f);
            db.Skill(Skill, "Fletching", new Rgb(0.25f, 0.6f, 0.55f), new Icon(22f)  // an arrow
                .Turned(9.5f, 2f, 2f, 18f, Rgb.Wood, 45f).TurnedAbout(8f, 1f, 6f, 6f, Rgb.Steel, 45f, 11f, 11f)
                .TurnedAbout(8f, 15f, 6f, 5f, feather, 45f, 11f, 11f));

            var knife = db.Item(Knife, "Knife", "A dangerous looking knife.", 6, Rgb.Steel);
            knife.ToolType = "knife";
            knife.ToolTier = 1;
            knife.Icon = new Icon().Turned(18f, 6f, 4f, 16f, Rgb.Steel, -35f).Turned(18f, 22f, 4f, 11f, Rgb.Wood, -35f);
            knife.HeldModel = new Model().Cube(0f, 0f, 0.06f, 0.035f, 0.035f, 0.12f, Looks.Handle).Cube(0f, 0f, 0.2f, 0.04f, 0.01f, 0.16f, Rgb.Steel);
            knife.GroundModel = Looks.OnGround(knife.HeldModel, -0.12f);

            var stringColour = new Rgb(0.9f, 0.88f, 0.78f);
            var bowstring = db.Item(Bowstring, "Bowstring", "I need a bow to attach this to.", 5, stringColour);
            bowstring.Icon = new Icon().Rect(10f, 8f, 20f, 2f, stringColour).Rect(28f, 8f, 2f, 22f, stringColour).Rect(10f, 28f, 20f, 2f, stringColour)
                .Rect(10f, 10f, 2f, 10f, stringColour);
            bowstring.GroundModel = new Model().Cylinder(0f, 0.01f, 0f, 0.26f, 0.006f, 0.26f, stringColour);

            var shaftWood = new Rgb(0.7f, 0.55f, 0.32f);
            Stack(db, ArrowShaft, "Arrow shaft", "Wooden arrow shafts.", 1, shaftWood, new Icon()
                .Turned(19f, 4f, 2f, 32f, shaftWood, 40f).Turned(15f, 6f, 2f, 30f, shaftWood * 0.9f, 40f));
            Stack(db, HeadlessArrow, "Headless arrow", "I need to attach arrowtips to these.", 1, shaftWood, new Icon()
                .Turned(19f, 4f, 2f, 32f, shaftWood, 40f).TurnedAbout(16f, 26f, 8f, 6f, feather, 40f, 20f, 20f));

            // Bows: unstrung from logs (knife), then strung (bowstring), the same xp each step.
            foreach (var w in Woods)
            {
                Bow(db, w, false);
                Bow(db, w, true);
                db.Add(new RecipeDef
                {
                    Id = "fletch_" + w.Prefix + "shafts", Name = "Arrow shafts", Station = Making.InHand, Skill = Skill, Level = w.ShaftLevel, Xp = w.ShaftXp,
                    ToolType = "knife", Inputs = new[] { new Ingredient(w.Logs, 1) }, Product = ArrowShaft, ProductCount = w.Shafts, Ticks = 3,
                    SuccessMessage = "You carefully cut the wood into " + w.Shafts + " arrow shafts.",
                });
            }

            db.Add(new RecipeDef
            {
                Id = "fletch_headless_arrows", Name = "Headless arrows", Station = Making.InHand, Skill = Skill, Level = 1, Xp = 1,
                Inputs = new[] { new Ingredient(ArrowShaft, 1), new Ingredient(FishingModule.Feathers, 1) }, Product = HeadlessArrow,
                Batch = 15, Ticks = 2, SuccessMessage = "You attach feathers to the arrow shafts.",
            });

            // Arrows of each metal: worn in the ammo slot, fired by bows that can take them.
            for (int i = 0; i < Metals.All.Length; i++)
            {
                var m = Metals.All[i];
                var arrow = db.Item(m.ItemId("arrow"), m.Name + " arrow", "Arrows with " + m.Name.ToLowerInvariant() + " heads.", m.Value(1), m.Colour);
                arrow.Stackable = true;
                arrow.Slot = EquipSlot.Ammo;
                arrow.AmmoType = "arrow";
                arrow.AmmoTier = m.Tier;
                arrow.Bonuses = new Bonuses { RangedStrength = ArrowStrength[i] };
                if (m.WearLevel > 1)
                    arrow.Requires.Add(new SkillReq(SkillIds.Ranged, m.WearLevel));
                arrow.Icon = new Icon().Turned(19f, 6f, 2f, 28f, shaftWood, 40f).TurnedAbout(15f, 2f, 10f, 8f, m.Colour, 40f, 20f, 20f)
                    .TurnedAbout(16f, 28f, 8f, 6f, feather, 40f, 20f, 20f).Turned(13f, 8f, 2f, 26f, shaftWood * 0.9f, 40f);
                arrow.GroundModel = new Model()
                    .Cylinder(0f, 0.02f, 0f, 0.02f, 0.25f, 0.02f, shaftWood).Turn(90f, 20f, 0f)
                    .Cylinder(0.06f, 0.02f, 0.02f, 0.02f, 0.25f, 0.02f, shaftWood).Turn(90f, -10f, 0f);
                db.Add(new RecipeDef
                {
                    Id = "fletch_" + m.ItemId("arrow"), Name = m.Name + " arrows", Station = Making.InHand, Skill = Skill, Level = ArrowLevel[i], Xp = ArrowXp[i],
                    Inputs = new[] { new Ingredient(HeadlessArrow, 1), new Ingredient(m.ItemId("arrowtips"), 1) }, Product = m.ItemId("arrow"),
                    Batch = 15, Ticks = 2, SuccessMessage = "You attach arrowtips to the arrows.",
                });
            }
        }

        static ItemDef Stack(ContentDb db, string id, string name, string examine, int value, Rgb colour, Icon icon)
        {
            var item = db.Item(id, name, examine, value, colour);
            item.Stackable = true;
            item.Icon = icon;
            item.GroundModel = new Model().Cylinder(0f, 0.02f, 0f, 0.02f, 0.25f, 0.02f, colour).Turn(90f, 15f, 0f)
                .Cylinder(0.05f, 0.02f, 0.03f, 0.02f, 0.25f, 0.02f, colour).Turn(90f, -15f, 0f);
            return item;
        }

        /// A bow: unstrung (cut from logs) or strung (wieldable). Short bows are quicker, long bows reach further.
        static void Bow(ContentDb db, Wood w, bool strung)
        {
            foreach (bool longbow in new[] { false, true })
            {
                string kind = longbow ? "longbow" : "shortbow";
                string id = w.Prefix + kind + (strung ? "" : "_u");
                string name = (w.Name + kind).Substring(0, 1).ToUpperInvariant() + (w.Name + kind).Substring(1) + (strung ? "" : " (u)");
                int level = longbow ? w.LongLevel : w.ShortLevel;
                double xp = longbow ? w.LongXp : w.ShortXp;
                var c = w.Colour;
                float len = longbow ? 1f : 0.8f;
                var held = new Model()
                    .Cube(0f, 0f, 0f, 0.05f, 0.05f, 0.14f, c * 0.8f)
                    .Cube(0f, 0.04f, 0.3f * len, 0.035f, 0.035f, 0.5f * len, c).Turn(-12f, 0f, 0f)
                    .Cube(0f, 0.04f, -0.3f * len, 0.035f, 0.035f, 0.5f * len, c).Turn(12f, 0f, 0f);
                if (strung)
                    held.Cube(0f, 0.13f, 0f, 0.01f, 0.01f, 1.08f * len, new Rgb(0.9f, 0.88f, 0.78f));
                var icon = new Icon()
                    .TurnedAbout(10f, 4f, 3f, 17f, c, -25f, 20f, 20f).TurnedAbout(10f, 19f, 3f, 17f, c, 25f, 20f, 20f);
                if (strung)
                    icon.Rect(12f, 3f, 1f, 34f, new Rgb(0.9f, 0.88f, 0.78f));
                var bow = db.Item(id, name, strung ? "A nice sturdy bow." : "I need to find a string for this.", (w.Attack * w.Attack / 2) * (longbow ? 2 : 1) + 5, c);
                bow.Icon = icon;
                bow.GroundModel = Looks.OnGround(held, 0f);
                if (strung)
                {
                    bow.HeldModel = held;
                    bow.Slot = EquipSlot.Weapon;
                    bow.WeaponKind = longbow ? CombatModule.Longbow : CombatModule.Shortbow;
                    bow.TwoHanded = true;
                    bow.AttackTicks = longbow ? 6 : 4;
                    bow.Range = longbow ? 9 : 7;
                    bow.AmmoType = "arrow";
                    bow.AmmoTier = w.AmmoTier;
                    bow.Bonuses = new Bonuses { RangedAttack = w.Attack };
                    if (w.Ranged > 1)
                        bow.Requires.Add(new SkillReq(SkillIds.Ranged, w.Ranged));
                    db.Add(new RecipeDef
                    {
                        Id = "string_" + id, Name = name, Station = Making.InHand, Skill = Skill, Level = level, Xp = xp,
                        Inputs = new[] { new Ingredient(id + "_u", 1), new Ingredient(Bowstring, 1) }, Product = id, Ticks = 2,
                        SuccessMessage = "You add a string to the bow.",
                    });
                }
                else
                {
                    db.Add(new RecipeDef
                    {
                        Id = "fletch_" + id, Name = name, Station = Making.InHand, Skill = Skill, Level = level, Xp = xp, ToolType = "knife",
                        Inputs = new[] { new Ingredient(w.Logs, 1) }, Product = id, Ticks = 3,
                        SuccessMessage = "You carefully cut the wood into " + Making.Article(name) + ".",
                    });
                }
            }
        }
    }
}
