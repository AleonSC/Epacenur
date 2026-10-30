using Epacenur.Core;

namespace Epacenur.Content
{
    /// Herblore: grimy herbs (dropped by monsters, stolen from herb stalls, picked from farmers' pockets) are
    /// cleaned, put in a vial of water, and finished with a second ingredient into a three-dose potion that
    /// boosts a skill for a while or restores prayer or run energy. OSRS levels and xp. One change from
    /// OSRS: an energy potion's second ingredient is ashes (OSRS uses chocolate dust).
    public sealed class HerbloreModule : IContentModule
    {
        public const string Skill = "herblore", Vial = "vial", VialOfWater = "vial_of_water",
            EyeOfNewt = "eye_of_newt", LimpwurtRoot = "limpwurt_root", SnapeGrass = "snape_grass";

        static readonly Rgb Glass = new Rgb(0.8f, 0.88f, 0.92f), Water = new Rgb(0.45f, 0.65f, 0.9f);

        /// A herb: its name, the level to clean it (and make its potion's first step), cleaning xp and colour.
        sealed class Herb
        {
            public string Id, Name;
            public int Level;
            public double Xp;
            public Rgb Colour;
            public Herb(string id, string name, int level, double xp, Rgb c) { Id = id; Name = name; Level = level; Xp = xp; Colour = c; }
        }

        static readonly Herb[] Herbs =
        {
            new Herb("guam", "Guam leaf", 3, 2.5, new Rgb(0.35f, 0.65f, 0.25f)),
            new Herb("tarromin", "Tarromin", 11, 5, new Rgb(0.55f, 0.6f, 0.2f)),
            new Herb("harralander", "Harralander", 20, 6.3, new Rgb(0.65f, 0.5f, 0.2f)),
            new Herb("ranarr", "Ranarr weed", 25, 7.5, new Rgb(0.25f, 0.55f, 0.45f)),
            new Herb("irit", "Irit leaf", 40, 8.8, new Rgb(0.6f, 0.65f, 0.35f)),
            new Herb("kwuarm", "Kwuarm", 54, 9.8, new Rgb(0.45f, 0.4f, 0.25f)),
        };

        public void Register(ContentDb db)
        {
            db.Skill(Skill, "Herblore", new Rgb(0.3f, 0.65f, 0.3f), new Icon(22f)  // a potion
                .Rect(9f, 2f, 4f, 5f, Glass).Rect(5f, 7f, 12f, 12f, Glass).Rect(6f, 11f, 10f, 7f, new Rgb(0.3f, 0.75f, 0.35f)));

            var vial = db.Item(Vial, "Vial", "An empty glass vial.", 2, Glass);
            vial.Icon = VialIcon(Glass, 0);
            vial.GroundModel = VialModel(Glass, 0);
            var water = db.Item(VialOfWater, "Vial of water", "A glass vial containing water.", 3, Water);
            water.Icon = VialIcon(Water, 3);
            water.GroundModel = VialModel(Water, 3);

            var newt = db.Item(EyeOfNewt, "Eye of newt", "It seems to be looking at me.", 3, new Rgb(0.85f, 0.8f, 0.3f));
            newt.Stackable = true;
            newt.Icon = new Icon().Rect(13f, 13f, 14f, 14f, new Rgb(0.9f, 0.85f, 0.4f)).Rect(17f, 17f, 6f, 6f, Looks.Dark);
            newt.GroundModel = new Model().Sphere(0f, 0.05f, 0f, 0.1f, 0.1f, 0.1f, new Rgb(0.9f, 0.85f, 0.4f));
            var root = db.Item(LimpwurtRoot, "Limpwurt root", "The root of a limpwurt plant.", 40, new Rgb(0.75f, 0.6f, 0.4f));
            root.Icon = new Icon().Turned(17f, 6f, 6f, 28f, new Rgb(0.75f, 0.6f, 0.4f), 20f).Turned(12f, 20f, 3f, 12f, new Rgb(0.7f, 0.55f, 0.35f), -30f);
            root.GroundModel = new Model().Capsule(0f, 0.05f, 0f, 0.07f, 0.2f, 0.07f, new Rgb(0.75f, 0.6f, 0.4f)).Turn(90f, 20f, 0f);
            var snape = db.Item(SnapeGrass, "Snape grass", "Strange spiky grass.", 40, new Rgb(0.3f, 0.6f, 0.3f));
            snape.Icon = new Icon().Turned(12f, 6f, 2f, 26f, new Rgb(0.3f, 0.6f, 0.3f), -15f).Turned(19f, 5f, 2f, 28f, new Rgb(0.35f, 0.65f, 0.3f), 0f)
                .Turned(26f, 6f, 2f, 26f, new Rgb(0.3f, 0.6f, 0.3f), 15f);
            snape.GroundModel = new Model().Cube(0f, 0.02f, 0f, 0.3f, 0.03f, 0.08f, new Rgb(0.3f, 0.6f, 0.3f));

            foreach (var h in Herbs)
                HerbItems(db, h);

            Potion(db, "attack", "Attack potion", "guam", EyeOfNewt, 3, 25, new Rgb(0.3f, 0.6f, 0.85f), 14,
                p => p.Boosts.Add(new SkillBoost(SkillIds.Attack, 3, 0.1)), "Attack");
            Potion(db, "strength", "Strength potion", "tarromin", LimpwurtRoot, 12, 50, new Rgb(0.85f, 0.25f, 0.25f), 40,
                p => p.Boosts.Add(new SkillBoost(SkillIds.Strength, 3, 0.1)), "Strength");
            Potion(db, "energy", "Energy potion", "harralander", FiremakingModule.Ashes, 26, 67.5, new Rgb(0.85f, 0.4f, 0.75f), 60,
                p => p.Energy = 20, null);
            Potion(db, "prayer", "Prayer potion", "ranarr", SnapeGrass, 38, 87.5, new Rgb(0.3f, 0.85f, 0.75f), 150,
                p => { p.PrayerFlat = 7; p.PrayerPercent = 0.25; }, null);
            Potion(db, "super_attack", "Super attack", "irit", EyeOfNewt, 45, 100, new Rgb(0.2f, 0.35f, 0.85f), 180,
                p => p.Boosts.Add(new SkillBoost(SkillIds.Attack, 5, 0.15)), "Attack");
            Potion(db, "super_strength", "Super strength", "kwuarm", LimpwurtRoot, 55, 125, new Rgb(0.9f, 0.9f, 0.95f), 240,
                p => p.Boosts.Add(new SkillBoost(SkillIds.Strength, 5, 0.15)), "Strength");

            // Where herbs and second ingredients come from: monsters' drops (a herb now and then).
            Drops(db, CombatModule.Goblin, "grimy_guam", 3, "grimy_tarromin", 2, "grimy_harralander", 1, EyeOfNewt, 2);
            Drops(db, CombatModule.Wolf, "grimy_tarromin", 2, "grimy_harralander", 2, "grimy_ranarr", 1);
            Drops(db, CombatModule.Bandit, "grimy_harralander", 2, "grimy_ranarr", 2, "grimy_irit", 1, LimpwurtRoot, 3);
            Drops(db, CombatModule.HillGiant, "grimy_ranarr", 3, "grimy_irit", 2, "grimy_kwuarm", 2, LimpwurtRoot, 8, SnapeGrass, 4);
            Drops(db, MagicModule.DarkWizard, "grimy_irit", 2, "grimy_kwuarm", 1, SnapeGrass, 5, EyeOfNewt, 4);
        }

        /// Adds drops to a monster: item id, weight, item id, weight...
        static void Drops(ContentDb db, string npc, params object[] itemsAndWeights)
        {
            var def = db.Npc(npc);
            for (int i = 0; i + 1 < itemsAndWeights.Length; i += 2)
                def.Drops.Add((string)itemsAndWeights[i], 1, 1, (int)itemsAndWeights[i + 1]);
        }

        static void HerbItems(ContentDb db, Herb h)
        {
            var dirt = new Rgb(0.4f, 0.32f, 0.2f);
            var grimy = db.Item("grimy_" + h.Id, "Grimy " + h.Name.ToLowerInvariant(), "I need to clean this herb before I can use it.", 2 + h.Level * 3, h.Colour * 0.6f);
            grimy.Icon = LeafIcon(h.Colour * 0.6f, dirt);
            grimy.GroundModel = LeafModel(h.Colour * 0.6f);
            grimy.Process = "clean_" + h.Id;
            grimy.ProcessVerb = "Clean";
            var clean = db.Item(h.Id, h.Name, "A fresh herb.", 3 + h.Level * 3, h.Colour);
            clean.Icon = LeafIcon(h.Colour, h.Colour);
            clean.GroundModel = LeafModel(h.Colour);
            db.Add(new RecipeDef
            {
                Id = "clean_" + h.Id, Name = h.Name, Station = Making.InHand, Skill = Skill, Level = h.Level, Xp = h.Xp, Ticks = 1,
                Inputs = new[] { new Ingredient(grimy.Id, 1) }, Product = h.Id,
                SuccessMessage = "You clean the dirt from the " + h.Name.ToLowerInvariant() + ".",
            });
        }

        static void Potion(ContentDb db, string id, string name, string herb, string secondary, int level, double xp, Rgb colour, int value,
            System.Action<PotionDef> effect, string boosts)
        {
            var hd = System.Array.Find(Herbs, x => x.Id == herb);
            var unf = db.Item(herb + "_potion_unf", hd.Name + " potion (unf)", "I need another ingredient to finish this " + hd.Name.ToLowerInvariant() + " potion.",
                hd.Level * 3 + 5, hd.Colour);
            unf.Icon = VialIcon(hd.Colour, 3);
            unf.GroundModel = VialModel(hd.Colour, 3);
            db.Add(new RecipeDef
            {
                Id = "mix_" + herb + "_unf", Name = unf.Name, Station = Making.InHand, Skill = Skill, Level = hd.Level, Xp = 0, Ticks = 1,
                Inputs = new[] { new Ingredient(herb, 1), new Ingredient(VialOfWater, 1) }, Product = unf.Id,
                SuccessMessage = "You put the " + hd.Name.ToLowerInvariant() + " into the vial of water.",
            });

            // Three doses: drinking one leaves the potion with one fewer, and the last leaves the vial.
            for (int dose = 3; dose >= 1; dose--)
            {
                var item = db.Item(id + "_potion_" + dose, name + "(" + dose + ")",
                    boosts != null ? dose + " dose" + (dose > 1 ? "s" : "") + " of " + name.ToLowerInvariant() + ": it raises your " + boosts + " for a while."
                        : dose + " dose" + (dose > 1 ? "s" : "") + " of " + name.ToLowerInvariant() + ".", value * dose / 3, colour);
                item.Icon = VialIcon(colour, dose);
                item.GroundModel = VialModel(colour, dose);
                var p = new PotionDef { Leaves = dose > 1 ? id + "_potion_" + (dose - 1) : Vial, Message = "You drink some of your " + name.ToLowerInvariant() + "." };
                effect(p);
                item.Potion = p;
            }
            db.Add(new RecipeDef
            {
                Id = "mix_" + id, Name = name + "(3)", Station = Making.InHand, Skill = Skill, Level = level, Xp = xp, Ticks = 2,
                Inputs = new[] { new Ingredient(unf.Id, 1), new Ingredient(secondary, 1) }, Product = id + "_potion_3",
                SuccessMessage = "You mix the ingredients into " + Making.Article(name.ToLowerInvariant()) + ".",
            });
        }

        /// A vial, filled `doses` thirds of the way with something.
        static Icon VialIcon(Rgb liquid, int doses)
        {
            var icon = new Icon().Rect(17f, 6f, 6f, 4f, new Rgb(0.6f, 0.45f, 0.3f)).Rect(16f, 10f, 8f, 5f, Glass).Rect(11f, 15f, 18f, 18f, Glass);
            if (doses > 0)
            {
                float h = 16f * doses / 3f;
                icon.Rect(12f, 32f - h, 16f, h, liquid);
            }
            return icon;
        }

        static Model VialModel(Rgb liquid, int doses)
        {
            var m = new Model().Cylinder(0f, 0.1f, 0f, 0.12f, 0.1f, 0.12f, Glass).Cylinder(0f, 0.24f, 0f, 0.05f, 0.05f, 0.05f, Glass);
            if (doses > 0)
                m.Cylinder(0f, 0.02f + 0.06f * doses / 3f, 0f, 0.125f, 0.06f * doses / 3f + 0.01f, 0.125f, liquid);
            return m;
        }

        static Icon LeafIcon(Rgb c, Rgb vein) => new Icon()
            .Turned(14f, 8f, 8f, 22f, c, -25f).Turned(20f, 9f, 8f, 22f, c * 0.9f, 20f).Turned(18f, 22f, 2f, 12f, vein * 0.7f, 0f);

        static Model LeafModel(Rgb c) => new Model()
            .Sphere(-0.05f, 0.02f, 0f, 0.12f, 0.03f, 0.22f, c).Turn(0f, 25f, 0f)
            .Sphere(0.06f, 0.025f, 0.02f, 0.12f, 0.03f, 0.2f, c * 0.9f).Turn(0f, -20f, 0f);
    }
}
