using Epacenur.Core;

namespace Epacenur.Content
{
    /// Melee combat: Attack, Strength, Defence and Hitpoints, the kinds of weapon and their attack styles,
    /// a bronze sword and wooden shield, and goblins. (Smithing makes the other bronze weapons.)
    public sealed class CombatModule : IContentModule
    {
        public const string BronzeSword = "bronze_sword", WoodenShield = "wooden_shield", Goblin = "goblin";
        public const string Sword = "sword", Dagger = "dagger", Scimitar = "scimitar", Mace = "mace", TwoHandedSword = "2h_sword";

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

            // Bronze sword: best at stabbing, a hit every 4 ticks (OSRS stats).
            var sword = db.Item(BronzeSword, "Bronze sword", "A razor-sharp sword.", 26, Looks.Bronze);
            sword.Slot = EquipSlot.Weapon;
            sword.WeaponKind = Sword;
            sword.Bonuses = new Bonuses { StabAttack = 4, SlashAttack = 3, CrushAttack = -2, SlashDefence = 2, CrushDefence = 1, Strength = 5 };
            sword.AttackTicks = 4;
            sword.Icon = new Icon()
                .Rect(18f, 4f, 5f, 22f, Looks.Bronze).Rect(19f, 4f, 2f, 22f, new Rgb(0.9f, 0.7f, 0.45f))
                .Rect(12f, 26f, 17f, 3f, new Rgb(0.4f, 0.4f, 0.45f)).Rect(18f, 29f, 5f, 6f, new Rgb(0.35f, 0.22f, 0.12f));
            sword.HeldModel = new Model()
                .Cube(0f, 0f, 0.05f, 0.04f, 0.04f, 0.14f, new Rgb(0.35f, 0.22f, 0.12f))
                .Cube(0f, 0f, 0.13f, 0.2f, 0.04f, 0.04f, new Rgb(0.45f, 0.45f, 0.5f))
                .Cube(0f, 0f, 0.43f, 0.06f, 0.015f, 0.56f, Looks.Bronze);
            sword.GroundModel = Looks.OnGround(sword.HeldModel);

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
            db.Add(goblin);
        }
    }
}
