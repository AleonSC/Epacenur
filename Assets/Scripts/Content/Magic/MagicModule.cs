using Epacenur.Core;

namespace Epacenur.Content
{
    /// Magic: the spellbook (strike and bolt combat spells, teleports, alchemy, with OSRS levels, runes and
    /// xp), staffs (each elemental staff stands in for its element's runes), wizard robes, and the dark
    /// wizards who wear them. One change from OSRS: bolts take a body rune where OSRS takes a chaos rune.
    public sealed class MagicModule : IContentModule
    {
        public const string Skill = "magic", StaffKind = "staff", Staff = "staff",
            StaffOfAir = "staff_of_air", StaffOfWater = "staff_of_water", StaffOfEarth = "staff_of_earth", StaffOfFire = "staff_of_fire",
            WizardHat = "wizard_hat", WizardRobe = "wizard_robe", WizardSkirt = "wizard_skirt", DarkWizard = "dark_wizard",
            HomeTeleport = "home_teleport", TownTeleport = "town_teleport", LowAlchemy = "low_alchemy", HighAlchemy = "high_alchemy";

        static readonly Rgb Blue = new Rgb(0.2f, 0.25f, 0.65f), Wood = new Rgb(0.45f, 0.3f, 0.16f);

        static Ingredient R(string rune, int n) => new Ingredient(rune, n);

        public void Register(ContentDb db)
        {
            db.Skill(Skill, "Magic", new Rgb(0.4f, 0.45f, 0.9f), new Icon(22f)  // a wizard's hat
                .Rect(3f, 16f, 16f, 3f, Blue).Rect(6f, 11f, 10f, 5f, Blue).Rect(8f, 6f, 6f, 5f, Blue).Rect(10f, 2f, 3f, 4f, Blue)
                .Rect(9f, 10f, 2f, 2f, new Rgb(1f, 0.9f, 0.3f)));

            // Staffs: crush weapons that help your spells hit; elemental ones are endless runes.
            db.Add(new WeaponKindDef
            {
                Id = StaffKind, Name = "Staff",
                Styles = new[] { new WeaponStyle("Bash", AttackType.Crush, CombatStyle.Accurate), new WeaponStyle("Pound", AttackType.Crush, CombatStyle.Aggressive),
                    new WeaponStyle("Focus", AttackType.Crush, CombatStyle.Defensive) },
            });
            StaffItem(db, Staff, "Staff", "It's a slightly magical stick.", 15, null, Wood * 1.3f);
            StaffItem(db, StaffOfAir, "Staff of air", "A magical staff.", 1500, RunecraftingModule.Air, new Rgb(0.85f, 0.9f, 0.95f));
            StaffItem(db, StaffOfWater, "Staff of water", "A magical staff.", 1500, RunecraftingModule.Water, new Rgb(0.25f, 0.5f, 0.95f));
            StaffItem(db, StaffOfEarth, "Staff of earth", "A magical staff.", 1500, RunecraftingModule.Earth, new Rgb(0.45f, 0.32f, 0.18f));
            StaffItem(db, StaffOfFire, "Staff of fire", "A magical staff.", 1500, RunecraftingModule.Fire, new Rgb(0.95f, 0.3f, 0.12f));

            // Wizard robes: a little magic accuracy, no defence to speak of.
            var star = new Rgb(1f, 0.9f, 0.3f);
            var hat = Robe(db, WizardHat, "Wizard hat", "A silly pointed hat.", 2, EquipSlot.Head, new Bonuses { MagicAttack = 2, MagicDefence = 2 },
                new Icon().Rect(6f, 28f, 28f, 4f, Blue).Rect(11f, 21f, 18f, 7f, Blue).Rect(14f, 14f, 12f, 7f, Blue).Rect(17f, 7f, 6f, 7f, Blue).Rect(18f, 18f, 3f, 3f, star));
            hat.Worn.Add(new WornPart { Bone = Bone.Body, Model = new Model()
                .Cylinder(0f, 1.88f, 0f, 0.62f, 0.02f, 0.62f, Blue)
                .Cylinder(0f, 2.0f, 0f, 0.36f, 0.12f, 0.36f, Blue).Cylinder(0f, 2.2f, -0.02f, 0.24f, 0.1f, 0.24f, Blue)
                .Cylinder(0f, 2.36f, -0.05f, 0.12f, 0.08f, 0.12f, Blue).Sphere(0f, 2.05f, 0.17f, 0.08f, 0.08f, 0.03f, star) });

            var top = Robe(db, WizardRobe, "Wizard robe top", "I can do magic better in this.", 15, EquipSlot.Body, new Bonuses { MagicAttack = 3, MagicDefence = 3 },
                new Icon().Rect(7f, 9f, 26f, 7f, Blue).Rect(11f, 15f, 18f, 18f, Blue).Rect(7f, 16f, 3f, 12f, Blue * 0.85f).Rect(30f, 16f, 3f, 12f, Blue * 0.85f)
                    .Rect(18f, 15f, 4f, 18f, star * 0.8f));
            top.Worn.Add(new WornPart { Bone = Bone.Body, Model = new Model()
                .Capsule(0f, 1.2f, 0f, 0.53f, 0.39f, 0.35f, Blue).Cube(0f, 1.2f, 0.175f, 0.06f, 0.36f, 0.02f, star * 0.8f) });
            var sleeve = new Model().Capsule(0f, -0.3f, 0f, 0.17f, 0.3f, 0.17f, Blue);
            top.Worn.Add(new WornPart { Bone = Bone.ArmLeft, Model = sleeve });
            top.Worn.Add(new WornPart { Bone = Bone.ArmRight, Model = sleeve });

            var skirt = Robe(db, WizardSkirt, "Wizard robe skirt", "I feel closer to the gods when I am wearing this.", 15, EquipSlot.Legs,
                new Bonuses { MagicAttack = 2, MagicDefence = 2 },
                new Icon().Rect(12f, 7f, 16f, 5f, Blue).Rect(10f, 12f, 20f, 10f, Blue).Rect(8f, 22f, 24f, 11f, Blue));
            skirt.Worn.Add(new WornPart { Bone = Bone.Body, Model = new Model().Cylinder(0f, 0.55f, 0f, 0.58f, 0.38f, 0.42f, Blue) });

            Spells(db);

            // Dark wizards: in groups in the wilds a little out, striking with spells. Runes, robes, staffs.
            var dark = new Rgb(0.12f, 0.1f, 0.18f);
            var wizard = db.Add(new NpcDef
            {
                Id = DarkWizard, Name = "Dark wizard", Examine = "He works evil magic.", CombatLevel = 20, Hitpoints = 24,
                AttackLevel = 17, DefenceLevel = 14, Bonuses = new Bonuses { MagicDefence = 15 }, AttackBonus = 6,
                AttackType = AttackType.Magic, MaxHit = 5, AttackTicks = 5, AttackRange = 6, RespawnSeconds = 30f, Height = 2.1f, Aggressive = true,
                Model = Bodies.Humanoid(new Rgb(0.85f, 0.66f, 0.5f), dark, dark, new Rgb(0.6f, 0.6f, 0.6f))
                    .Cylinder(0f, 1.95f, 0f, 0.6f, 0.02f, 0.6f, dark).Cylinder(0f, 2.1f, -0.02f, 0.3f, 0.15f, 0.3f, dark)
                    .Cylinder(0.42f, 1f, 0.2f, 0.05f, 0.9f, 0.05f, Wood).Sphere(0.42f, 1.95f, 0.2f, 0.12f, 0.12f, 0.12f, new Rgb(0.7f, 0.3f, 0.9f)).Glowing(),
            });
            wizard.AlwaysDrops.Add(PrayerModule.Bones);
            wizard.Drops.Add(RunecraftingModule.Mind, 5, 15, 20).Add(RunecraftingModule.Air, 10, 20, 15).Add(RunecraftingModule.Water, 5, 12, 10)
                .Add(RunecraftingModule.Earth, 5, 12, 10).Add(RunecraftingModule.Fire, 5, 12, 10).Add(RunecraftingModule.Body, 3, 8, 8)
                .Add(RunecraftingModule.Nature, 1, 3, 3).Add(RunecraftingModule.Law, 1, 2, 2)
                .Add(WizardHat, 1, 1, 3).Add(WizardRobe, 1, 1, 2).Add(WizardSkirt, 1, 1, 2).Add(Staff, 1, 1, 3).Add(ItemIds.Coins, 5, 40, 12);
            wizard.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Wild, MinDistance = 200, Weight = 1.2, MinGroup = 2, MaxGroup = 4 });
            wizard.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Camp, MinDistance = 150, MaxDistance = 700, Weight = 1, MinGroup = 3, MaxGroup = 4 });
        }

        static void StaffItem(ContentDb db, string id, string name, string examine, int value, string rune, Rgb orb)
        {
            var item = db.Item(id, name, examine, value, orb);
            item.Slot = EquipSlot.Weapon;
            item.WeaponKind = StaffKind;
            item.AttackTicks = 5;
            item.InfiniteRune = rune;
            item.Bonuses = rune == null
                ? new Bonuses { CrushAttack = 7, MagicAttack = 4, StabDefence = 2, SlashDefence = 3, CrushDefence = 1, MagicDefence = 4, Strength = 5 }
                : new Bonuses { StabAttack = 2, SlashAttack = -1, CrushAttack = 10, MagicAttack = 10, StabDefence = 2, SlashDefence = 3, CrushDefence = 1, MagicDefence = 10, Strength = 7 };
            item.Icon = new Icon().Turned(18.5f, 4f, 3f, 32f, Wood * 1.3f, 35f).Diamond(29f, 9f, rune == null ? 3f : 5f, orb);
            var held = new Model().Cylinder(0f, 0f, 0.4f, 0.05f, 0.75f, 0.05f, Wood * 1.3f).Turn(90f, 0f, 0f);
            held.Sphere(0f, 0f, 1.17f, rune == null ? 0.1f : 0.15f, rune == null ? 0.1f : 0.15f, rune == null ? 0.1f : 0.15f, orb);
            if (rune != null)
                held.Glowing();
            item.HeldModel = held;
            item.GroundModel = Looks.OnGround(held, -0.5f);
        }

        static ItemDef Robe(ContentDb db, string id, string name, string examine, int value, EquipSlot slot, Bonuses bonuses, Icon icon)
        {
            var item = db.Item(id, name, examine, value, Blue);
            item.Slot = slot;
            item.Bonuses = bonuses;
            item.Icon = icon;
            item.GroundModel = new Model().Cube(0f, 0.03f, 0f, 0.4f, 0.06f, 0.36f, Blue).Cube(0f, 0.05f, 0.1f, 0.2f, 0.03f, 0.14f, Blue * 0.85f);
            return item;
        }

        // ---- The spellbook ----

        static void Spells(ContentDb db)
        {
            string air = RunecraftingModule.Air, mind = RunecraftingModule.Mind, water = RunecraftingModule.Water, earth = RunecraftingModule.Earth,
                fire = RunecraftingModule.Fire, body = RunecraftingModule.Body, nature = RunecraftingModule.Nature, law = RunecraftingModule.Law;
            Rgb cAir = new Rgb(0.85f, 0.9f, 0.95f), cWater = new Rgb(0.25f, 0.5f, 0.95f), cEarth = new Rgb(0.45f, 0.32f, 0.18f), cFire = new Rgb(0.95f, 0.3f, 0.12f);

            db.Add(new SpellDef
            {
                Id = HomeTeleport, Name = "Home Teleport", Kind = SpellKind.HomeTeleport, Level = 1, CastTicks = 16, Colour = new Rgb(0.6f, 0.8f, 1f),
                Description = "Teleports you to the town you started in. Takes a while to cast, and moving stops it. No runes.",
                Icon = new Icon(22f).Rect(4f, 10f, 14f, 9f, new Rgb(0.75f, 0.6f, 0.45f)).Rect(3f, 7f, 16f, 3f, new Rgb(0.6f, 0.25f, 0.2f))
                    .Rect(9f, 13f, 4f, 6f, new Rgb(0.35f, 0.22f, 0.12f)),
            });
            Combat(db, "wind_strike", "Wind Strike", 1, 5.5, 2, cAir, R(air, 1), R(mind, 1));
            Combat(db, "water_strike", "Water Strike", 5, 7.5, 4, cWater, R(water, 1), R(air, 1), R(mind, 1));
            Combat(db, "earth_strike", "Earth Strike", 9, 9.5, 6, cEarth, R(earth, 2), R(air, 1), R(mind, 1));
            Combat(db, "fire_strike", "Fire Strike", 13, 11.5, 8, cFire, R(fire, 3), R(air, 2), R(mind, 1));
            Combat(db, "wind_bolt", "Wind Bolt", 17, 13.5, 9, cAir, R(air, 2), R(body, 1));
            db.Add(new SpellDef
            {
                Id = LowAlchemy, Name = "Low Level Alchemy", Kind = SpellKind.Alchemy, Level = 21, Xp = 31, Value = 0.4, Colour = new Rgb(0.9f, 0.75f, 0.3f),
                Runes = new[] { R(fire, 3), R(nature, 1) },
                Description = "Turns an item into coins: 40% of its value.",
                Icon = new Icon(22f).Rect(6f, 6f, 10f, 10f, new Rgb(0.8f, 0.65f, 0.25f)).Rect(8f, 8f, 6f, 6f, new Rgb(0.95f, 0.8f, 0.35f)),
            });
            Combat(db, "water_bolt", "Water Bolt", 23, 16.5, 10, cWater, R(water, 2), R(air, 2), R(body, 1));
            db.Add(new SpellDef
            {
                Id = TownTeleport, Name = "Town Teleport", Kind = SpellKind.TownTeleport, Level = 25, Xp = 35, Colour = new Rgb(0.4f, 0.4f, 1f),
                Runes = new[] { R(fire, 1), R(air, 3), R(law, 1) },
                Description = "Teleports you to the nearest town.",
                Icon = new Icon(22f).Rect(4f, 10f, 14f, 9f, new Rgb(0.7f, 0.7f, 0.75f)).Rect(3f, 7f, 16f, 3f, new Rgb(0.3f, 0.3f, 0.7f))
                    .Diamond(11f, 4f, 3f, new Rgb(0.5f, 0.5f, 1f)),
            });
            Combat(db, "earth_bolt", "Earth Bolt", 29, 19.5, 11, cEarth, R(earth, 3), R(air, 2), R(body, 1));
            Combat(db, "fire_bolt", "Fire Bolt", 35, 22.5, 12, cFire, R(fire, 4), R(air, 3), R(body, 1));
            db.Add(new SpellDef
            {
                Id = HighAlchemy, Name = "High Level Alchemy", Kind = SpellKind.Alchemy, Level = 55, Xp = 65, Value = 0.6, Colour = new Rgb(1f, 0.85f, 0.3f),
                Runes = new[] { R(fire, 5), R(nature, 1) },
                Description = "Turns an item into coins: 60% of its value.",
                Icon = new Icon(22f).Rect(4f, 4f, 14f, 14f, new Rgb(0.85f, 0.7f, 0.25f)).Rect(6f, 6f, 10f, 10f, new Rgb(1f, 0.88f, 0.4f)).Rect(9f, 9f, 4f, 4f, new Rgb(1f, 1f, 0.8f)),
            });
        }

        static void Combat(ContentDb db, string id, string name, int level, double xp, int maxHit, Rgb colour, params Ingredient[] runes)
        {
            bool bolt = name.EndsWith("Bolt");
            var icon = new Icon(22f);
            if (bolt)
                icon.Turned(3f, 9f, 16f, 4f, colour, -20f).Diamond(17f, 8f, 4f, colour * 1.2f);
            else
                icon.Diamond(11f, 11f, 6f, colour).Diamond(11f, 11f, 3f, colour * 1.3f);
            db.Add(new SpellDef
            {
                Id = id, Name = name, Kind = SpellKind.Combat, Level = level, Xp = xp, MaxHit = maxHit, Colour = colour, Runes = runes, Icon = icon,
                Description = "A " + (bolt ? "medium" : "low") + " level " + name.Split(' ')[0].ToLowerInvariant() + " missile (max hit " + maxHit + ").",
            });
        }
    }
}
