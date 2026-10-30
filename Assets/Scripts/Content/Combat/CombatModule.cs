using Epacenur.Core;

namespace Epacenur.Content
{
    /// Melee combat: Attack, Strength, Defence and Hitpoints, the kinds of weapon and their attack styles, the
    /// wooden shield, and the monsters: where each lives (its Spawns: kind of region, distance from the start),
    /// its stats (OSRS-like) and drops. Weapons and armour are smithed (SmithingModule).
    public sealed class CombatModule : IContentModule
    {
        public const string BronzeSword = "bronze_sword", WoodenShield = "wooden_shield", Cowhide = "cowhide",
            Goblin = "goblin", Chicken = "chicken", Cow = "cow", Wolf = "wolf", Bandit = "bandit", HillGiant = "hill_giant";
        public const string Sword = "sword", Dagger = "dagger", Scimitar = "scimitar", Mace = "mace", TwoHandedSword = "2h_sword",
            Shortbow = "shortbow", Longbow = "longbow";

        static void Kind(ContentDb db, string id, string name, params WeaponStyle[] styles) =>
            db.Add(new WeaponKindDef { Id = id, Name = name, Styles = styles });

        static WeaponStyle S(string name, AttackType type, CombatStyle style) => new WeaponStyle(name, type, style);

        public void Register(ContentDb db)
        {
            // Attack styles per kind of weapon, as in OSRS: each style's attack type and the skill it trains.
            const AttackType stab = AttackType.Stab, slash = AttackType.Slash, crush = AttackType.Crush;
            const CombatStyle acc = CombatStyle.Accurate, agg = CombatStyle.Aggressive, def = CombatStyle.Defensive;
            Kind(db, WeaponKindIds.Unarmed, "Unarmed", S("Punch", crush, acc), S("Kick", crush, agg), S("Block", crush, def));
            Kind(db, Sword, "Sword", S("Stab", stab, acc), S("Lunge", stab, agg), S("Slash", slash, agg), S("Block", stab, def));
            Kind(db, Dagger, "Dagger", S("Stab", stab, acc), S("Lunge", stab, agg), S("Slash", slash, agg), S("Block", stab, def));
            Kind(db, Scimitar, "Scimitar", S("Chop", slash, acc), S("Slash", slash, agg), S("Lunge", stab, agg), S("Block", slash, def));
            Kind(db, Mace, "Mace", S("Pound", crush, acc), S("Pummel", crush, agg), S("Spike", stab, agg), S("Block", crush, def));
            Kind(db, TwoHandedSword, "Two-handed sword", S("Chop", slash, acc), S("Slash", slash, agg), S("Smash", crush, agg), S("Block", slash, def));
            // Bows: Accurate (+3 Ranged), Rapid (a tick faster), Longrange (2 tiles further, trains Defence too).
            const AttackType ranged = AttackType.Ranged;
            foreach (var bow in new[] { Shortbow, Longbow })
                db.Add(new WeaponKindDef
                {
                    Id = bow, Name = bow == Shortbow ? "Shortbow" : "Longbow", Ranged = true,
                    Styles = new[] { S("Accurate", ranged, acc), S("Rapid", ranged, CombatStyle.Rapid), S("Longrange", ranged, CombatStyle.Longrange) },
                });

            var steel = Rgb.Steel;
            var gold = Rgb.Gold;
            var wood = Rgb.Wood;

            db.Skill(SkillIds.Attack, "Attack", new Rgb(0.85f, 0.55f, 0.15f), new Icon(22f)  // a sword, point up and to the right
                .Turned(9.5f, 1f, 3f, 14f, steel, 45f).Turned(6f, 15f, 10f, 2f, gold, 45f).Turned(10f, 17f, 2f, 4f, wood, 45f));
            var skin = new Rgb(0.86f, 0.6f, 0.4f);
            db.Skill(SkillIds.Strength, "Strength", new Rgb(0.2f, 0.7f, 0.55f), new Icon(22f)  // a clenched fist
                .Rect(5f, 6f, 12f, 11f, skin).Rect(2f, 10f, 4f, 6f, skin)
                .Rect(8f, 6f, 1f, 5f, skin * 0.7f).Rect(11f, 6f, 1f, 5f, skin * 0.7f).Rect(14f, 6f, 1f, 5f, skin * 0.7f)
                .Rect(7f, 17f, 8f, 4f, new Rgb(0.55f, 0.35f, 0.2f)));
            var rim = new Rgb(0.6f, 0.62f, 0.66f);
            var face = new Rgb(0.3f, 0.45f, 0.75f);
            db.Skill(SkillIds.Defence, "Defence", new Rgb(0.45f, 0.6f, 0.95f), new Icon(22f)  // a kite shield
                .Rect(3f, 2f, 16f, 11f, rim).Rect(5f, 13f, 12f, 4f, rim).Rect(7f, 17f, 8f, 3f, rim).Rect(9f, 20f, 4f, 2f, rim)
                .Rect(5f, 4f, 12f, 9f, face).Rect(7f, 13f, 8f, 3f, face).Rect(10f, 4f, 2f, 13f, rim));
            var red = new Rgb(0.85f, 0.12f, 0.15f);
            db.Skill(SkillIds.Hitpoints, "Hitpoints", new Rgb(0.9f, 0.25f, 0.3f), new Icon(22f)  // a heart
                .Rect(3f, 4f, 7f, 6f, red).Rect(12f, 4f, 7f, 6f, red).Rect(3f, 8f, 16f, 4f, red).Rect(5f, 12f, 12f, 3f, red)
                .Rect(7f, 15f, 8f, 3f, red).Rect(9f, 18f, 4f, 2f, red).Rect(5f, 5f, 2f, 2f, new Rgb(1f, 0.7f, 0.7f)), startLevel: 10);
            var bowWood = new Rgb(0.55f, 0.36f, 0.18f);
            db.Skill(SkillIds.Ranged, "Ranged", new Rgb(0.35f, 0.7f, 0.25f), new Icon(22f)  // a bow and arrow
                .TurnedAbout(3f, 3f, 2f, 9f, bowWood, -30f, 11f, 11f).TurnedAbout(3f, 10f, 2f, 9f, bowWood, 30f, 11f, 11f)
                .Rect(4f, 2f, 1f, 18f, new Rgb(0.9f, 0.9f, 0.85f)).Rect(5f, 10f, 14f, 1.5f, Rgb.Wood).Diamond(19f, 10.75f, 2.5f, Rgb.Steel));

            // Wooden shield: +5 defence. A round shield facing out from the left arm.
            var shieldWood = new Rgb(0.5f, 0.33f, 0.18f);
            var shield = db.Item(WoodenShield, "Wooden shield", "A solid wooden shield.", 20, shieldWood);
            shield.Slot = EquipSlot.Shield;
            shield.Bonuses = new Bonuses { StabDefence = 4, SlashDefence = 5, CrushDefence = 3 };
            shield.Icon = new Icon()
                .Rect(8f, 6f, 24f, 28f, new Rgb(0.45f, 0.45f, 0.5f)).Rect(10f, 8f, 20f, 24f, shieldWood)
                .Rect(16f, 8f, 1f, 24f, shieldWood * 0.7f).Rect(23f, 8f, 1f, 24f, shieldWood * 0.7f)
                .Rect(17f, 17f, 6f, 6f, new Rgb(0.6f, 0.6f, 0.65f));
            shield.HeldModel = new Model()
                .Cylinder(0f, 0f, 0f, 0.5f, 0.02f, 0.5f, new Rgb(0.45f, 0.45f, 0.5f)).Turn(0f, 0f, 90f)
                .Cylinder(-0.012f, 0f, 0f, 0.44f, 0.02f, 0.44f, shieldWood).Turn(0f, 0f, 90f)
                .Sphere(-0.03f, 0f, 0f, 0.06f, 0.1f, 0.1f, new Rgb(0.6f, 0.6f, 0.65f));
            shield.GroundModel = new Model().Child(shield.HeldModel, 0f, 0.03f, 0f, 0f, 0f, 90f);

            // Goblins: level 2, fight back with a max hit of 1, always drop bones and some coins.
            var green = new Rgb(0.35f, 0.6f, 0.25f);
            var cloth = new Rgb(0.45f, 0.32f, 0.2f);
            var eye = new Rgb(0.8f, 0.1f, 0.05f);
            var goblin = new NpcDef
            {
                Id = Goblin, Name = "Goblin", Examine = "An ugly green creature.", CombatLevel = 2, Hitpoints = 5,
                DefenceLevel = 1, Bonuses = Bonuses.AllDefence(-2), AttackLevel = 1, AttackBonus = 0, AttackType = AttackType.Crush,
                MaxHit = 1, AttackTicks = 4,
                RespawnSeconds = 15f, Height = 1.5f,
                Model = new Model()
                    .Capsule(0f, 0.72f, 0f, 0.45f, 0.4f, 0.35f, cloth)
                    .Capsule(-0.12f, 0.22f, 0f, 0.14f, 0.22f, 0.14f, green)
                    .Capsule(0.12f, 0.22f, 0f, 0.14f, 0.22f, 0.14f, green)
                    .Capsule(-0.29f, 0.75f, 0.05f, 0.11f, 0.24f, 0.11f, green)
                    .Capsule(0.29f, 0.75f, 0.05f, 0.11f, 0.24f, 0.11f, green)
                    .Sphere(0f, 1.2f, 0.02f, 0.36f, 0.34f, 0.36f, green)
                    .Cube(0f, 1.17f, 0.18f, 0.07f, 0.1f, 0.08f, green * 0.85f)
                    .Cube(-0.08f, 1.25f, 0.16f, 0.05f, 0.04f, 0.03f, eye)
                    .Cube(0.08f, 1.25f, 0.16f, 0.05f, 0.04f, 0.03f, eye)
                    .Cube(-0.22f, 1.26f, 0f, 0.2f, 0.05f, 0.08f, green).Turn(0f, 0f, -25f)
                    .Cube(0.22f, 1.26f, 0f, 0.2f, 0.05f, 0.08f, green).Turn(0f, 0f, 25f),
            };
            goblin.Drops.Add(ItemIds.Coins, 1, 5, 50).Add(ItemIds.Coins, 6, 15, 35).Add(ItemIds.Coins, 16, 30, 15);
            goblin.AlwaysDrops.Add(PrayerModule.Bones);
            goblin.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Camp, MaxDistance = 700, Weight = 3, MinGroup = 4, MaxGroup = 5 });
            goblin.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Wild, MaxDistance = 300, Weight = 1, MinGroup = 2, MaxGroup = 3 });
            db.Add(goblin);

            var hide = db.Item(Cowhide, "Cowhide", "I should take this to a tanner.", 20, new Rgb(0.85f, 0.82f, 0.75f));
            hide.Icon = new Icon().Rect(8f, 10f, 24f, 20f, new Rgb(0.85f, 0.82f, 0.75f)).Rect(12f, 14f, 6f, 5f, Looks.Dark).Rect(22f, 20f, 7f, 6f, Looks.Dark)
                .Rect(6f, 8f, 4f, 4f, new Rgb(0.85f, 0.82f, 0.75f)).Rect(30f, 28f, 4f, 4f, new Rgb(0.85f, 0.82f, 0.75f));
            hide.GroundModel = new Model().Cube(0f, 0.015f, 0f, 0.5f, 0.03f, 0.4f, new Rgb(0.85f, 0.82f, 0.75f))
                .Cube(0.08f, 0.032f, 0.05f, 0.15f, 0.01f, 0.12f, Looks.Dark);

            Monsters(db);
        }

        static NpcDef Monster(ContentDb db, string id, string name, string examine, int combat, int hp, int attack, int defence,
            int defenceBonus, int maxHit, AttackType type, float respawn, float height, bool aggressive, Model model)
        {
            var npc = new NpcDef
            {
                Id = id, Name = name, Examine = examine, CombatLevel = combat, Hitpoints = hp,
                AttackLevel = attack, DefenceLevel = defence, Bonuses = Bonuses.AllDefence(defenceBonus), AttackBonus = 0,
                AttackType = type, MaxHit = maxHit, AttackTicks = 4, RespawnSeconds = respawn, Height = height,
                Aggressive = aggressive, Model = model,
            };
            db.Add(npc);
            return npc;
        }

        static void Monsters(ContentDb db)
        {
            // Chickens: harmless, round towns and in the countryside near the start. Feathers for fly fishing.
            var white = new Rgb(0.95f, 0.95f, 0.92f);
            var red = new Rgb(0.85f, 0.15f, 0.12f);
            var beak = new Rgb(0.95f, 0.7f, 0.2f);
            var chicken = Monster(db, Chicken, "Chicken", "Yep, definitely a chicken.", 1, 3, 1, 1, -10, 1, AttackType.Stab, 12f, 0.6f, false,
                new Model()
                    .Sphere(0f, 0.3f, 0f, 0.32f, 0.28f, 0.4f, white)
                    .Sphere(0f, 0.5f, 0.15f, 0.18f, 0.2f, 0.18f, white)
                    .Cube(0f, 0.5f, 0.27f, 0.05f, 0.04f, 0.08f, beak)
                    .Cube(0f, 0.62f, 0.15f, 0.04f, 0.08f, 0.1f, red)
                    .Cube(0f, 0.36f, -0.2f, 0.16f, 0.18f, 0.1f, white * 0.9f).Turn(-30f, 0f, 0f)
                    .Cylinder(-0.07f, 0.08f, 0f, 0.03f, 0.08f, 0.03f, beak).Cylinder(0.07f, 0.08f, 0f, 0.03f, 0.08f, 0.03f, beak));
            chicken.AlwaysDrops.Add(PrayerModule.Bones);
            chicken.AlwaysDrops.Add(CookingModule.RawChicken);
            chicken.Drops.Add(FishingModule.Feathers, 5, 5, 50).Add(FishingModule.Feathers, 10, 15, 25).Add(null, 0, 0, 25);
            chicken.WalkSpeed = 1.2f;
            chicken.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Town, Weight = 2, MinGroup = 3, MaxGroup = 5 });
            chicken.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Wild, MaxDistance = 300, Weight = 2, MinGroup = 4, MaxGroup = 6 });

            // Cows: big and harmless, in fields near the start. Hides, and beef to cook.
            var hide = new Rgb(0.92f, 0.9f, 0.85f);
            var cowModel = Bodies.Quadruped(hide, hide, hide, 1.5f, 1.2f, 0.6f)
                .Sphere(0.15f, 0.9f, 0.2f, 0.25f, 0.2f, 0.4f, Looks.Dark).Sphere(-0.2f, 0.85f, -0.3f, 0.2f, 0.22f, 0.3f, Looks.Dark)
                .Cube(-0.15f, 1.12f, 0.85f, 0.06f, 0.12f, 0.06f, new Rgb(0.85f, 0.8f, 0.65f)).Cube(0.15f, 1.12f, 0.85f, 0.06f, 0.12f, 0.06f, new Rgb(0.85f, 0.8f, 0.65f))
                .Cube(0f, 0.9f, 1.1f, 0.25f, 0.18f, 0.08f, new Rgb(0.9f, 0.65f, 0.65f));
            var cow = Monster(db, Cow, "Cow", "Converts grass to beef.", 2, 8, 1, 1, -10, 1, AttackType.Crush, 15f, 1.3f, false, cowModel);
            cow.AlwaysDrops.Add(PrayerModule.Bones);
            cow.AlwaysDrops.Add(CookingModule.RawBeef);
            cow.AlwaysDrops.Add(Cowhide);
            cow.WalkSpeed = 1f;
            cow.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Wild, MaxDistance = 900, Weight = 2.5, MinGroup = 3, MaxGroup = 5 });
            cow.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Town, Weight = 1, MinGroup = 2, MaxGroup = 3 });

            // Wolves: packs in the wilds further out. They go for weaker players.
            var grey = new Rgb(0.45f, 0.45f, 0.48f);
            var wolfModel = Bodies.Quadruped(grey, grey * 0.9f, grey * 0.8f, 1.1f, 0.85f, 0.4f)
                .Cube(-0.1f, 0.88f, 0.6f, 0.06f, 0.12f, 0.06f, grey).Cube(0.1f, 0.88f, 0.6f, 0.06f, 0.12f, 0.06f, grey)
                .Cube(-0.07f, 0.73f, 0.78f, 0.04f, 0.03f, 0.02f, new Rgb(0.9f, 0.8f, 0.2f)).Cube(0.07f, 0.73f, 0.78f, 0.04f, 0.03f, 0.02f, new Rgb(0.9f, 0.8f, 0.2f));
            var wolf = Monster(db, Wolf, "Wolf", "Not man's best friend.", 22, 20, 18, 14, 0, 3, AttackType.Stab, 25f, 0.95f, true, wolfModel);
            wolf.AlwaysDrops.Add(PrayerModule.Bones);
            wolf.WalkSpeed = 2.2f;
            wolf.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Wild, MinDistance = 150, Weight = 2, MinGroup = 3, MaxGroup = 4 });

            // Bandits: they hold the camps further out, and roam the wilds beyond. Coins and the odd weapon.
            var bandit = Monster(db, Bandit, "Bandit", "He's not very friendly.", 30, 30, 25, 20, 10, 4, AttackType.Slash, 25f, 1.9f, true,
                Bodies.Humanoid(new Rgb(0.85f, 0.66f, 0.5f), new Rgb(0.3f, 0.22f, 0.15f), new Rgb(0.2f, 0.18f, 0.16f), new Rgb(0.15f, 0.1f, 0.08f))
                    .Cube(0f, 1.62f, 0.15f, 0.3f, 0.12f, 0.05f, new Rgb(0.55f, 0.1f, 0.1f))  // a mask
                    .Cube(0.4f, 0.85f, 0.2f, 0.04f, 0.04f, 0.5f, Metals.Iron.Colour));      // a blade
            bandit.AlwaysDrops.Add(PrayerModule.Bones);
            bandit.Drops.Add(ItemIds.Coins, 10, 60, 55).Add("iron_dagger", 1, 1, 8).Add(MiningModule.IronOre, 1, 2, 8)
                .Add("iron_scimitar", 1, 1, 4).Add(null, 0, 0, 25);
            bandit.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Camp, MinDistance = 300, Weight = 2, MinGroup = 4, MaxGroup = 5 });
            bandit.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Wild, MinDistance = 400, Weight = 1, MinGroup = 2, MaxGroup = 3 });

            // Hill giants: big, slow and tough, far out in the hills. Big bones and better loot.
            var skin = new Rgb(0.82f, 0.6f, 0.45f);
            var giant = Monster(db, HillGiant, "Hill Giant", "A very large foe.", 28, 35, 18, 26, 0, 4, AttackType.Crush, 30f, 3.2f, true,
                new Model().Child(Bodies.Humanoid(skin, new Rgb(0.5f, 0.38f, 0.22f), new Rgb(0.45f, 0.32f, 0.2f), new Rgb(0.3f, 0.2f, 0.12f))
                    .Cube(0.42f, 0.9f, 0.3f, 0.12f, 0.12f, 0.7f, Rgb.Wood), 0f, 0f, 0f, scale: 1.7f));  // a club
            giant.AlwaysDrops.Add(PrayerModule.BigBones);
            giant.Drops.Add(ItemIds.Coins, 10, 80, 40).Add(MiningModule.IronOre, 1, 3, 10).Add(MiningModule.Coal, 1, 3, 10)
                .Add("steel_dagger", 1, 1, 5).Add("iron_full_helm", 1, 1, 5).Add("steel_platelegs", 1, 1, 2).Add(null, 0, 0, 28);
            giant.WalkSpeed = 1.3f;
            giant.Spawns.Add(new NpcSpawnRule { Region = RegionKind.Wild, MinDistance = 450, Weight = 1.5, MinGroup = 2, MaxGroup = 3 });
        }
    }
}
