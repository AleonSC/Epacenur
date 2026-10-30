using Epacenur.Core;

namespace Epacenur.Content
{
    /// What lives in the dungeons under the cave mouths: slayer monsters (crawling hands and rockslugs from
    /// Slayer; cave bugs, cave crawlers, banshees, pyrefiends and basilisks here), goblins near safety, and at
    /// the end of each dungeon a boss with a special attack you can pray against (a boss for each band of
    /// wilderness level), dropping its own unique item now and then.
    public sealed class DungeonsModule : IContentModule
    {
        public const string CaveBug = "cave_bug", CaveCrawler = "cave_crawler", Banshee = "banshee", Pyrefiend = "pyrefiend", Basilisk = "basilisk",
            CrawlingHorror = "crawling_horror", RockBehemoth = "rock_behemoth", PyreLord = "pyre_lord",
            ShadowMace = "shadow_mace", BehemothShield = "behemoth_shield", PyreStaff = "pyre_staff";

        public void Register(ContentDb db)
        {
            // Goblins have warrens near safety too.
            db.Npc(CombatModule.Goblin).Spawns.Add(new NpcSpawnRule { Region = RegionKind.Dungeon, MaxDanger = 12, Weight = 2, MinGroup = 3, MaxGroup = 4 });

            var bug = new Rgb(0.35f, 0.5f, 0.3f);
            Monster(db, CaveBug, "Cave bug", "A large, glowing bug.", 6, 5, 4, 3, 1, AttackType.Crush, 7, 0.5f, 0, 18, 3, 3, 5,
                new Model().Sphere(0f, 0.18f, 0f, 0.4f, 0.3f, 0.5f, bug).Sphere(0f, 0.2f, 0.3f, 0.2f, 0.18f, 0.2f, bug * 0.8f)
                    .Cube(-0.25f, 0.08f, 0f, 0.3f, 0.04f, 0.04f, bug * 0.7f).Cube(0.25f, 0.08f, 0f, 0.3f, 0.04f, 0.04f, bug * 0.7f)
                    .Sphere(0f, 0.32f, -0.05f, 0.18f, 0.1f, 0.2f, new Rgb(0.6f, 1f, 0.4f)).Glowing())
                .Drops.Add(ItemIds.Coins, 2, 15, 4).Add("grimy_guam", 1, 1, 2).Add(null, 0, 0, 4);
            var crawler = new Rgb(0.4f, 0.45f, 0.3f);
            Monster(db, CaveCrawler, "Cave crawler", "A spiky crawling critter.", 23, 22, 18, 20, 3, AttackType.Stab, 10, 0.6f, 0, 30, 2, 3, 4,
                new Model().Capsule(0f, 0.25f, 0f, 0.5f, 0.45f, 0.35f, crawler).Turn(90f, 0f, 0f)
                    .Cube(0f, 0.45f, 0f, 0.05f, 0.25f, 0.05f, crawler * 0.6f).Turn(20f, 0f, 0f).Cube(0f, 0.45f, -0.2f, 0.05f, 0.25f, 0.05f, crawler * 0.6f).Turn(-20f, 0f, 0f)
                    .Sphere(-0.1f, 0.3f, 0.42f, 0.06f, 0.06f, 0.06f, new Rgb(0.9f, 0.3f, 0.2f)).Sphere(0.1f, 0.3f, 0.42f, 0.06f, 0.06f, 0.06f, new Rgb(0.9f, 0.3f, 0.2f)))
                .Drops.Add(ItemIds.Coins, 8, 40, 4).Add("grimy_tarromin", 1, 1, 2).Add("grimy_harralander", 1, 1, 1).Add(null, 0, 0, 3);
            var pale = new Rgb(0.82f, 0.85f, 0.9f);
            Monster(db, Banshee, "Banshee", "A tortured, screaming soul.", 23, 22, 22, 18, 3, AttackType.Magic, 15, 1.9f, 5, 40, 2, 2, 3,
                new Model().Cylinder(0f, 0.85f, 0f, 0.45f, 0.85f, 0.4f, pale * 0.9f).Sphere(0f, 1.75f, 0f, 0.32f, 0.36f, 0.32f, pale)
                    .Sphere(0f, 1.82f, -0.04f, 0.36f, 0.3f, 0.36f, new Rgb(0.2f, 0.2f, 0.22f))
                    .Cube(0f, 1.66f, 0.16f, 0.12f, 0.12f, 0.02f, Looks.Dark))
                .Drops.Add(ItemIds.Coins, 10, 60, 4).Add("grimy_ranarr", 1, 1, 1).Add(RunecraftingModule.Fire, 5, 15, 2).Add(null, 0, 0, 3);
            var fire = new Rgb(1f, 0.45f, 0.1f);
            var pyre = Monster(db, Pyrefiend, "Pyrefiend", "A small fire demon.", 43, 45, 40, 30, 5, AttackType.Magic, 30, 1.4f, 15, 60, 2, 2, 3,
                new Model().Capsule(0f, 0.6f, 0f, 0.45f, 0.5f, 0.35f, new Rgb(0.6f, 0.15f, 0.05f)).Sphere(0f, 1.15f, 0f, 0.3f, 0.3f, 0.3f, new Rgb(0.6f, 0.15f, 0.05f))
                    .Sphere(0f, 1.5f, 0f, 0.25f, 0.45f, 0.25f, fire).Glowing());
            pyre.AttackRange = 5;
            pyre.Drops.Add(ItemIds.Coins, 30, 120, 4).Add(RunecraftingModule.Fire, 15, 40, 3).Add("grimy_irit", 1, 1, 1).Add(null, 0, 0, 3);
            var lizard = new Rgb(0.35f, 0.45f, 0.3f);
            Monster(db, Basilisk, "Basilisk", "The eyes of evil.", 61, 75, 55, 60, 8, AttackType.Stab, 40, 1.1f, 25, int.MaxValue, 2, 2, 3,
                Bodies.Quadruped(lizard, lizard * 1.1f, lizard * 0.85f, 1.7f, 0.9f, 0.6f)
                    .Sphere(-0.12f, 0.95f, 1.05f, 0.08f, 0.08f, 0.04f, new Rgb(1f, 0.85f, 0.2f)).Glowing()
                    .Sphere(0.12f, 0.95f, 1.05f, 0.08f, 0.08f, 0.04f, new Rgb(1f, 0.85f, 0.2f)).Glowing())
                .Drops.Add(ItemIds.Coins, 50, 200, 4).Add("grimy_kwuarm", 1, 1, 1).Add("adamantite_ore", 1, 2, 1).Add(null, 0, 0, 3);

            Uniques(db);

            // Bosses, one for each band of wilderness level: a special attack every few swings, of a different kind
            // from their usual one, so you have to switch your protection prayer.
            var shadow = new Rgb(0.25f, 0.15f, 0.3f);
            var horror = Boss(db, CrawlingHorror, "Crawling Horror", "A heap of crawling hands, grown huge and hungry.", 45, 60, 40, 30, 15,
                AttackType.Crush, 5, 4, AttackType.Magic, 9, "The Crawling Horror hurls a bolt of shadow!", 0, 15,
                new Model()
                    .Sphere(0f, 0.6f, 0f, 1.6f, 1.1f, 1.6f, new Rgb(0.65f, 0.55f, 0.45f))
                    .Sphere(0.4f, 1.0f, 0.3f, 0.7f, 0.5f, 0.6f, new Rgb(0.7f, 0.6f, 0.5f)).Sphere(-0.45f, 0.95f, -0.2f, 0.7f, 0.55f, 0.7f, new Rgb(0.6f, 0.5f, 0.42f))
                    .Cube(0.6f, 0.2f, 0.75f, 0.12f, 0.12f, 0.5f, new Rgb(0.7f, 0.6f, 0.5f)).Cube(-0.6f, 0.2f, 0.7f, 0.12f, 0.12f, 0.5f, new Rgb(0.7f, 0.6f, 0.5f))
                    .Sphere(0f, 1.3f, 0.5f, 0.35f, 0.3f, 0.3f, shadow).Glowing());
            horror.Drops.Add(ItemIds.Coins, 100, 400, 10).Add(ShadowMace, 1, 1, 2).Add("grimy_ranarr", 2, 4, 3).Add(RunecraftingModule.Law, 3, 8, 2);
            var stone = new Rgb(0.5f, 0.48f, 0.45f);
            var behemoth = Boss(db, RockBehemoth, "Rock Behemoth", "A mountain that walks.", 85, 120, 70, 80, 40,
                AttackType.Crush, 10, 5, AttackType.Ranged, 14, "The Rock Behemoth hurls a boulder!", 15, 35,
                Bodies.Humanoid(stone, stone * 0.9f, stone * 0.8f, stone * 0.7f)
                    .Sphere(0f, 1.2f, 0f, 1.2f, 0.9f, 0.8f, stone * 0.85f).Sphere(-0.5f, 1.7f, 0f, 0.4f, 0.35f, 0.4f, stone).Sphere(0.5f, 1.7f, 0f, 0.4f, 0.35f, 0.4f, stone));
            behemoth.Drops.Add(ItemIds.Coins, 300, 900, 10).Add(BehemothShield, 1, 1, 2).Add("mithril_ore", 3, 8, 3).Add("adamantite_ore", 1, 4, 2);
            var lord = Boss(db, PyreLord, "Pyre Lord", "Lord of the pyrefiends, wreathed in flame.", 130, 200, 110, 100, 60,
                AttackType.Magic, 14, 3, AttackType.Ranged, 18, "The Pyre Lord rains embers down on you!", 35, int.MaxValue,
                Bodies.Humanoid(new Rgb(0.5f, 0.12f, 0.05f), new Rgb(0.35f, 0.08f, 0.04f), new Rgb(0.3f, 0.07f, 0.03f), fire)
                    .Sphere(0f, 2.15f, 0f, 0.5f, 0.7f, 0.5f, fire).Glowing().Cylinder(0.45f, 1.1f, 0.2f, 0.06f, 1.1f, 0.06f, Looks.Dark));
            lord.AttackRange = 6;
            lord.Drops.Add(ItemIds.Coins, 800, 2500, 10).Add(PyreStaff, 1, 1, 2).Add(RunecraftingModule.Nature, 10, 30, 3).Add("grimy_kwuarm", 3, 6, 3);
        }

        static NpcDef Monster(ContentDb db, string id, string name, string examine, int combat, int hp, int attack, int defence, int maxHit,
            AttackType type, int slayer, float height, int minDanger, int maxDanger, double weight, int minGroup, int maxGroup, Model model)
        {
            var npc = db.Add(new NpcDef
            {
                Id = id, Name = name, Examine = examine, CombatLevel = combat, Hitpoints = hp, AttackLevel = attack, DefenceLevel = defence,
                Bonuses = Bonuses.AllDefence(defence / 3), AttackType = type, MaxHit = maxHit, AttackTicks = 4, RespawnSeconds = 25f,
                Height = height, Aggressive = true, SlayerLevel = slayer, Model = model,
            });
            npc.AlwaysDrops.Add(PrayerModule.Bones);
            npc.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Dungeon, MinDanger = minDanger, MaxDanger = maxDanger, Weight = weight, MinGroup = minGroup, MaxGroup = maxGroup });
            return npc;
        }

        static NpcDef Boss(ContentDb db, string id, string name, string examine, int combat, int hp, int attack, int defence, int defenceBonus,
            AttackType type, int maxHit, int specialEvery, AttackType specialType, int specialMaxHit, string specialMessage,
            int minDanger, int maxDanger, Model model)
        {
            var npc = db.Add(new NpcDef
            {
                Id = id, Name = name, Examine = examine, CombatLevel = combat, Hitpoints = hp, AttackLevel = attack, DefenceLevel = defence,
                Bonuses = Bonuses.AllDefence(defenceBonus), AttackType = type, MaxHit = maxHit, AttackTicks = 5, RespawnSeconds = 120f,
                Height = 2.6f, WalkSpeed = 1.1f, Aggressive = true, Boss = true, Model = model,
                SpecialEvery = specialEvery, SpecialType = specialType, SpecialMaxHit = specialMaxHit, SpecialMessage = specialMessage,
            });
            npc.AlwaysDrops.Add(PrayerModule.BigBones);
            npc.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Dungeon, MinDanger = minDanger, MaxDanger = maxDanger, Weight = 1, MinGroup = 1, MaxGroup = 1 });
            return npc;
        }

        /// The bosses' unique drops.
        static void Uniques(ContentDb db)
        {
            var dark = new Rgb(0.28f, 0.2f, 0.35f);
            var mace = db.Item(ShadowMace, "Shadow mace", "A mace that drinks the light. Dropped by the Crawling Horror.", 9000, dark);
            mace.Slot = EquipSlot.Weapon;
            mace.WeaponKind = CombatModule.Mace;
            mace.AttackTicks = 4;
            mace.Bonuses = new Bonuses { StabAttack = 18, CrushAttack = 36, Strength = 32, StabDefence = 2, SlashDefence = 2 };
            mace.Requires.Add(new SkillReq(SkillIds.Attack, 30));
            mace.Icon = new Icon().Turned(18.5f, 10f, 3f, 24f, Rgb.Wood * 0.6f, 35f).Diamond(27f, 10f, 6f, dark).Diamond(27f, 10f, 3f, new Rgb(0.6f, 0.4f, 0.8f));
            mace.HeldModel = new Model().Cylinder(0f, 0f, 0.3f, 0.05f, 0.35f, 0.05f, Rgb.Wood * 0.6f).Turn(90f, 0f, 0f)
                .Sphere(0f, 0f, 0.72f, 0.2f, 0.2f, 0.22f, dark).Sphere(0f, 0f, 0.72f, 0.1f, 0.1f, 0.24f, new Rgb(0.6f, 0.4f, 0.8f)).Glowing();
            mace.GroundModel = Looks.OnGround(mace.HeldModel);

            var stone = new Rgb(0.52f, 0.5f, 0.46f);
            var shield = db.Item(BehemothShield, "Behemoth shield", "A slab of living rock. Dropped by the Rock Behemoth.", 15000, stone);
            shield.Slot = EquipSlot.Shield;
            shield.Bonuses = new Bonuses { StabDefence = 44, SlashDefence = 46, CrushDefence = 42, RangedDefence = 46, MagicDefence = -5, Strength = 2 };
            shield.Requires.Add(new SkillReq(SkillIds.Defence, 40));
            shield.Icon = new Icon().Rect(8f, 6f, 24f, 28f, stone * 0.8f).Rect(10f, 8f, 20f, 24f, stone).Rect(14f, 12f, 5f, 4f, stone * 0.7f).Rect(21f, 20f, 6f, 5f, stone * 0.7f);
            shield.HeldModel = new Model().Cube(0f, 0f, 0f, 0.08f, 0.9f, 0.7f, stone).Sphere(-0.04f, 0.1f, 0.05f, 0.08f, 0.3f, 0.25f, stone * 0.85f);
            shield.GroundModel = new Model().Child(shield.HeldModel, 0f, 0.05f, 0f, 0f, 0f, 90f);

            var flame = new Rgb(1f, 0.45f, 0.1f);
            var staff = db.Item(PyreStaff, "Pyre staff", "It never stops burning. An endless supply of fire runes. Dropped by the Pyre Lord.", 25000, flame);
            staff.Slot = EquipSlot.Weapon;
            staff.WeaponKind = MagicModule.StaffKind;
            staff.AttackTicks = 5;
            staff.InfiniteRune = RunecraftingModule.Fire;
            staff.Bonuses = new Bonuses { CrushAttack = 12, MagicAttack = 20, MagicDefence = 18, StabDefence = 3, SlashDefence = 3, CrushDefence = 2, Strength = 8 };
            staff.Requires.Add(new SkillReq(SkillIds.Magic, 50));
            staff.Icon = new Icon().Turned(18.5f, 4f, 3f, 32f, new Rgb(0.25f, 0.12f, 0.08f), 35f).Diamond(29f, 9f, 6f, flame);
            staff.HeldModel = new Model().Cylinder(0f, 0f, 0.4f, 0.05f, 0.75f, 0.05f, new Rgb(0.25f, 0.12f, 0.08f)).Turn(90f, 0f, 0f)
                .Sphere(0f, 0f, 1.17f, 0.18f, 0.18f, 0.18f, flame).Glowing();
            staff.GroundModel = Looks.OnGround(staff.HeldModel, -0.5f);
        }
    }
}
