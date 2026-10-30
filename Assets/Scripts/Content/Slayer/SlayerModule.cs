using Epacenur.Core;

namespace Epacenur.Content
{
    /// Slayer: a master in every town (in one of its houses) sets you a task, some number of one kind of monster
    /// that suits your combat level. Each kill on task gives Slayer xp (the monster's hitpoints); each finished
    /// task gives points (more every tenth in a row), spent on skipping a task or a slayer helmet. Some monsters
    /// can only be hurt with a Slayer level: crawling hands (5) and rockslugs (20), down in the dungeons (Dungeons).
    public sealed class SlayerModule : IContentModule
    {
        public const string Skill = Slayer.Skill, Master = "slayer_master", Helmet = "slayer_helmet",
            CrawlingHand = "crawling_hand", Rockslug = "rockslug";
        public const int HelmetCost = 60;
        /// Wearing a slayer helmet on task: accuracy and max hit times this (OSRS: 7/6).
        public const double HelmetBoost = 7.0 / 6.0;

        public void Register(ContentDb db)
        {
            var red = new Rgb(0.6f, 0.12f, 0.1f);
            db.Skill(Skill, "Slayer", new Rgb(0.55f, 0.1f, 0.1f), new Icon(22f)  // a skull
                .Rect(5f, 3f, 12f, 11f, new Rgb(0.92f, 0.9f, 0.82f)).Rect(7f, 14f, 8f, 4f, new Rgb(0.92f, 0.9f, 0.82f))
                .Rect(7f, 7f, 3f, 3f, Looks.Dark).Rect(12f, 7f, 3f, 3f, Looks.Dark).Rect(9f, 15f, 1f, 3f, Looks.Dark).Rect(12f, 15f, 1f, 3f, Looks.Dark));

            var skin = new Rgb(0.85f, 0.66f, 0.5f);
            db.Add(new StationDef
            {
                Id = Master, Name = "Slayer master", Action = "Get-task", Kind = StationKind.SlayerMaster,
                Examine = "A grizzled hunter of monsters. Right-click for rewards.", Label = "Slayer master", LabelHeight = 2.3f,
                Model = Bodies.Humanoid(skin, new Rgb(0.25f, 0.2f, 0.18f), new Rgb(0.2f, 0.16f, 0.14f), new Rgb(0.5f, 0.5f, 0.5f))
                    .Cylinder(0f, 1.2f, -0.05f, 0.6f, 0.55f, 0.45f, red)  // a cloak
                    .Cube(0.42f, 0.9f, 0.15f, 0.05f, 0.05f, 0.7f, Metals.Steel.Colour),
            });

            var helmColour = new Rgb(0.18f, 0.18f, 0.2f);
            var helm = db.Item(Helmet, "Slayer helmet", "A helmet that helps slay the monsters of your task (Slayer points).", 1000, helmColour);
            helm.Slot = EquipSlot.Head;
            helm.Bonuses = new Bonuses { StabDefence = 30, SlashDefence = 32, CrushDefence = 27, RangedDefence = 30, MagicDefence = 10 };
            helm.Requires.Add(new SkillReq(SkillIds.Defence, 10));
            helm.Icon = new Icon().Rect(10f, 8f, 20f, 6f, helmColour).Rect(8f, 13f, 24f, 18f, helmColour).Rect(12f, 18f, 6f, 4f, new Rgb(0.9f, 0.2f, 0.15f))
                .Rect(22f, 18f, 6f, 4f, new Rgb(0.9f, 0.2f, 0.15f));
            helm.GroundModel = new Model().Sphere(0f, 0.12f, 0f, 0.32f, 0.28f, 0.32f, helmColour);
            helm.Worn.Add(new WornPart { Bone = Bone.Body, Model = new Model()
                .Sphere(0f, 1.73f, -0.01f, 0.4f, 0.41f, 0.4f, helmColour)
                .Cube(-0.08f, 1.72f, 0.19f, 0.09f, 0.05f, 0.02f, new Rgb(0.9f, 0.2f, 0.15f)).Glowing()
                .Cube(0.08f, 1.72f, 0.19f, 0.09f, 0.05f, 0.02f, new Rgb(0.9f, 0.2f, 0.15f)).Glowing() });

            // Monsters only a slayer can hurt.
            var flesh = new Rgb(0.75f, 0.68f, 0.55f);
            var hand = db.Add(new NpcDef
            {
                Id = CrawlingHand, Name = "Crawling hand", Examine = "A severed hand, crawling along by itself.", CombatLevel = 8, Hitpoints = 16,
                AttackLevel = 6, DefenceLevel = 4, Bonuses = Bonuses.AllDefence(0), AttackType = AttackType.Crush, MaxHit = 2, AttackTicks = 4,
                RespawnSeconds = 20f, Height = 0.4f, WalkSpeed = 1.1f, Aggressive = true, SlayerLevel = 5,
                Model = new Model()
                    .Cube(0f, 0.12f, 0f, 0.35f, 0.12f, 0.4f, flesh)
                    .Cube(-0.13f, 0.06f, 0.28f, 0.06f, 0.06f, 0.22f, flesh).Cube(-0.04f, 0.06f, 0.3f, 0.06f, 0.06f, 0.25f, flesh)
                    .Cube(0.05f, 0.06f, 0.3f, 0.06f, 0.06f, 0.24f, flesh).Cube(0.14f, 0.06f, 0.27f, 0.06f, 0.06f, 0.2f, flesh)
                    .Cube(0.22f, 0.06f, 0.05f, 0.18f, 0.06f, 0.07f, flesh).Cylinder(0f, 0.12f, -0.25f, 0.15f, 0.05f, 0.15f, new Rgb(0.5f, 0.1f, 0.1f)),
            });
            hand.AlwaysDrops.Add(PrayerModule.Bones);
            hand.Drops.Add(ItemIds.Coins, 5, 30, 6).Add("grimy_guam", 1, 1, 2).Add(null, 0, 0, 4);
            hand.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Dungeon, MaxDanger = 22, Weight = 3, MinGroup = 3, MaxGroup = 5 });

            var slug = new Rgb(0.45f, 0.42f, 0.38f);
            var rockslug = db.Add(new NpcDef
            {
                Id = Rockslug, Name = "Rockslug", Examine = "Hard to believe it's alive.", CombatLevel = 29, Hitpoints = 27,
                AttackLevel = 24, DefenceLevel = 30, Bonuses = Bonuses.AllDefence(20), AttackType = AttackType.Crush, MaxHit = 4, AttackTicks = 4,
                RespawnSeconds = 25f, Height = 0.7f, WalkSpeed = 0.6f, Aggressive = true, SlayerLevel = 20,
                Model = new Model()
                    .Capsule(0f, 0.3f, 0f, 0.55f, 0.6f, 0.4f, slug).Turn(90f, 0f, 0f)
                    .Sphere(0.12f, 0.5f, 0.1f, 0.25f, 0.2f, 0.25f, slug * 0.8f).Sphere(-0.1f, 0.48f, -0.2f, 0.22f, 0.18f, 0.22f, slug * 0.85f)
                    .Cylinder(-0.08f, 0.55f, 0.55f, 0.03f, 0.12f, 0.03f, slug).Cylinder(0.08f, 0.55f, 0.55f, 0.03f, 0.12f, 0.03f, slug),
            });
            rockslug.AlwaysDrops.Add(PrayerModule.Bones);
            rockslug.Drops.Add(ItemIds.Coins, 20, 80, 6).Add("grimy_harralander", 1, 1, 2).Add("grimy_ranarr", 1, 1, 1).Add(MiningModule.IronOre, 1, 3, 3).Add(null, 0, 0, 3);
            rockslug.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Dungeon, MinDanger = 6, MaxDanger = 45, Weight = 2, MinGroup = 3, MaxGroup = 4 });
        }
    }
}
