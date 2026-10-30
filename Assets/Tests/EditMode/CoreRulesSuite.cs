using System;
using System.Numerics;
using Epacenur.Content;
using Epacenur.Core;

namespace Epacenur.Tests
{
/// Checks the game rules that don't need Unity. Two runners use it: Unity's Test Runner (CoreRulesTests,
/// edit mode) and Tools/Tests/run.sh (mono, outside Unity).
public static class CoreRulesSuite
{
    static Action<bool, string> check;
    static Action<string> info;

    static void Check(bool ok, string what) => check(ok, what);
    static ItemDef Item(string id) => ContentDb.Current.Item(id);
    const string Logs = WoodcuttingModule.Logs, Coins = ItemIds.Coins, Sword = CombatModule.BronzeSword, Shield = CombatModule.WoodenShield;

    /// `check(ok, description)` is called for every check, `info` for section headings and notes.
    public static void Run(Action<bool, string> checkCallback, Action<string> infoCallback)
    {
        check = checkCallback;
        info = infoCallback;
        info("Content");
        var db = ContentDb.Load(Modules.All);
        Check(db.Validate().Count == 0, "every id the content refers to exists");
        Check(db.Skills.Count == 11 && db.Skill(SkillIds.Hitpoints).StartLevel == 10, "11 skills; Hitpoints starts at 10");
        bool dup = false;
        try { new ContentDb().Add(new ItemDef { Id = "x" }); var d2 = new ContentDb(); d2.Add(new ItemDef { Id = "x" }); d2.Add(new ItemDef { Id = "x" }); }
        catch (ArgumentException) { dup = true; }
        Check(dup, "two items with one id are refused");
        var broken = new ContentDb();
        broken.Add(new RecipeDef { Id = "r", Skill = "nope", Product = "nothing", Inputs = new Ingredient[0] });
        var brokenProblems = broken.Validate();
        Check(brokenProblems.Exists(m => m.Contains("no skill \"nope\"")) && brokenProblems.Exists(m => m.Contains("no item \"nothing\"")),
            "a recipe naming a missing skill and item is caught");
        bool icons = true;
        foreach (var it in db.Items) if (it.Icon == null || it.GroundModel == null) icons = false;
        Check(icons, "every item has an icon and a ground model");

        info("Experience curve");
        Check(Experience.XpForLevel(2) == 83, "level 2 = 83 xp");
        Check(Experience.XpForLevel(99) == 13034431, "level 99 = 13,034,431 xp");
        Check(Experience.XpForLevel(120) == 104273167, "level 120 = 104,273,167 xp");
        Check(Experience.LevelForXp(200000000) == 126, "200m xp is level 126");
        Check(Experience.LevelForXp(0) == 1 && Experience.LevelForXp(82) == 1 && Experience.LevelForXp(83) == 2, "level boundaries");
        Check(Experience.LevelForXp(Experience.MaxXp) == 999, "max xp is level 999");
        Check(Experience.LevelForXp(Experience.MaxXp * 2) == 999, "levels stop at 999");
        double ratio = (double)(Experience.XpForLevel(600) - Experience.XpForLevel(599)) / (double)(Experience.XpForLevel(599) - Experience.XpForLevel(598));
        Check(Math.Abs(ratio - Math.Pow(2, 1 / 7.0)) < 1e-6, "gap between levels grows by 2^(1/7) = " + ratio.ToString("0.00000"));
        info("  level 999 needs " + Experience.Format(Experience.MaxXp) + " xp (" + Experience.FormatShort(Experience.MaxXp) + ")");
        var py = BigInteger.Parse("6590753875914258720611083014045249960396472940");
        Check(BigInteger.Abs(Experience.MaxXp - py) <= py / 1000000000000, "level 999 xp matches the formula computed independently");
        for (int l = 1; l < 999; l++)
            if (Experience.XpForLevel(l + 1) <= Experience.XpForLevel(l)) { Check(false, "table rises at level " + l); break; }
        Check(Experience.Format(13034431) == "13,034,431" && Experience.Format(83) == "83", "comma formatting");

        info("Skills");
        var skills = new SkillSet();
        int ups = 0, lastLevel = 0;
        skills.LevelledUp += (id, lvl) => { ups++; lastLevel = lvl; };
        skills.AddXp(WoodcuttingModule.Skill, 25);
        Check(skills.GetXp(WoodcuttingModule.Skill) == 25 && skills.GetLevel(WoodcuttingModule.Skill) == 1, "25 xp is level 1");
        skills.AddXp(WoodcuttingModule.Skill, 67.5);
        skills.AddXp(WoodcuttingModule.Skill, 67.5);
        Check(skills.GetXpTenths(WoodcuttingModule.Skill) == 1600, "half xp adds up exactly (160.0)");
        Check(ups == 1 && lastLevel == 2, "levelled up once to 2");
        skills.AddXpTenths(WoodcuttingModule.Skill, Experience.MaxXp * 100);
        Check(skills.GetXp(WoodcuttingModule.Skill) == Experience.MaxXp && skills.GetLevel(WoodcuttingModule.Skill) == 999, "xp caps at level 999");
        skills.SetLevel(WoodcuttingModule.Skill, 99);
        Check(skills.GetLevel(WoodcuttingModule.Skill) == 99, "set level 99");

        info("Inventory");
        var inv = new Inventory();
        Check(inv.Add(Logs, 30) == 28, "28 logs fit, one per slot");
        Check(inv.IsFull && !inv.HasRoomFor(Logs) && inv.Count(Logs) == 28, "inventory full of logs");
        inv.RemoveAt(5);
        Check(inv.FreeSlots == 1 && inv.Add(Logs) == 1 && !inv[5].IsEmpty, "a log goes in the freed slot");

        info("Bank");
        var bank = new Bank();
        inv.Clear();
        inv.Add(Logs, 10);
        Check(bank.Deposit(inv, Logs, 3) == 3 && inv.Count(Logs) == 7, "deposit 3 of 10 logs");
        Check(bank.UsedSlots == 1 && bank[0].Count == 3, "logs stack in one bank slot");
        Check(bank.DepositAll(inv) == 7 && inv.FreeSlots == Inventory.Size && bank.UsedSlots == 1 && bank.Count(Logs) == 10, "deposit inventory: all 10 logs in one slot");
        bank.Add(Logs, 90);
        Check(bank.Withdraw(inv, 0, long.MaxValue) == 28 && bank.Count(Logs) == 72, "withdraw all: only 28 fit");
        Check(bank.Withdraw(inv, 0, 5) == 0, "nothing withdrawn into a full inventory");
        inv.Clear();
        bank.Clear();
        bank.Add(Logs, 5);
        Check(bank.Withdraw(inv, 0, long.MaxValue) == 5 && bank.UsedSlots == 0 && inv.Count(Logs) == 5, "an emptied bank slot is removed");

        info("Shop");
        var shop = new Shop(db.Shop(ShopsModule.GeneralStore));
        Check(shop.Items.Count == 7 && shop.Items[0].ItemId == FiremakingModule.Tinderbox && shop.Items[1].ItemId == Sword && shop.Items[2].ItemId == Shield, "stocks a tinderbox, a sword and a shield");
        inv.Clear();
        string why;
        Check(shop.Buy(inv, 1, 1, out why) == 0 && why == "You don't have enough coins.", "can't buy without coins");
        inv.Add(Coins, 60);
        Check(shop.Buy(inv, 1, 10, out why) == 2 && inv.Count(Sword) == 2 && inv.Count(Coins) == 8, "60 coins buy two 26-coin swords");
        Check(shop.Items[1].Stock == 3, "stock goes down");
        Check(shop.Sell(inv, Sword, 1, out why) == 1 && inv.Count(Coins) == 18 && shop.Items[1].Stock == 4, "selling a sword pays 10 (40%)");
        Check(shop.Sell(inv, Coins, 1, out why) == 0, "coins can't be sold");
        inv.Add(Logs, 3);
        Check(shop.Sell(inv, Logs, 5, out why) == 3 && shop.Items.Count == 8 && inv.Count(Coins) == 21, "the shop buys logs it doesn't stock (1 coin each)");
        for (int i = 0; i < 5; i++) shop.Restock();
        Check(shop.Items.Count == 7 && shop.Items[1].Stock == 5, "restocking brings stock back to normal and clears sold logs");
        inv.Clear();
        inv.SetPouch(0);
        inv.Add(Logs, 27);
        inv.Add(Coins, 26);
        Check(shop.Buy(inv, 1, 1, out why) == 1 && inv.IsFull && inv.Count(Coins) == 0, "buying with pouch coins into the last free slot");

        info("Money pouch");
        inv.Clear();
        inv.SetPouch(0);
        inv.Add(Logs, 28);
        Check(inv.Add(Coins, 500) == 500 && inv.PouchCoins == 500 && inv.CountInSlots(Coins) == 0, "coins go into the pouch, even with a full backpack");
        Check(inv.HasRoomFor(Coins), "there's always room for coins");
        inv.RemoveAt(0);
        Check(inv.WithdrawFromPouch(120) == 120 && inv.PouchCoins == 380 && inv[0].ItemId == Coins && inv[0].Count == 120, "withdraw 120 into the backpack as a stack");
        Check(inv.Count(Coins) == 500, "Count covers pouch and backpack");
        Check(inv.Remove(Coins, 400) == 400 && inv.PouchCoins == 0 && inv[0].Count == 100, "spending takes the pouch first, then the backpack");
        Check(inv.AddToPouch(0) == 100 && inv.PouchCoins == 100 && inv[0].IsEmpty, "a stack goes back into the pouch");
        inv.SetPouch(Inventory.MaxPouchCoins - 10);
        Check(inv.Add(Coins, 25) == 25 && inv.PouchCoins == Inventory.MaxPouchCoins && inv.CountInSlots(Coins) == 15, "past 2,147,483,647 the rest spills into the backpack");
        var pbank = new Bank();
        inv.SetPouch(1000);
        Check(pbank.DepositAll(inv) > 0 && inv.PouchCoins == 1000 && pbank.Count(Coins) == 15, "Deposit inventory leaves the pouch alone");
        Check(pbank.DepositPouch(inv) == 1000 && inv.PouchCoins == 0 && pbank.Count(Coins) == 1015, "Deposit money pouch");
        Check(pbank.Withdraw(inv, pbank.IndexOf(Coins), 300) == 300 && inv.PouchCoins == 300, "coins withdrawn from the bank land in the pouch");
        inv.Clear();
        inv.SetPouch(0);

        info("Tool belt");
        var belt = new ToolBelt();
        inv.Clear();
        inv.Add(FiremakingModule.Tinderbox);
        inv.Add(FiremakingModule.Tinderbox);
        inv.Add(Logs);
        Check(ToolBelt.IsTool(WoodcuttingModule.BronzeHatchet) && Item(WoodcuttingModule.BronzeHatchet).ToolType == "hatchet", "the bronze hatchet chops and goes on the belt");
        Check(ToolBelt.Find(belt, inv, "hatchet") == null && ToolBelt.Find(belt, inv, "tinderbox") == Item(FiremakingModule.Tinderbox), "finding a tool by type");
        Check(!ToolBelt.HasItem(belt, new Inventory(), FiremakingModule.Tinderbox) && ToolBelt.HasItem(belt, inv, FiremakingModule.Tinderbox), "a tinderbox in the backpack counts as having one");
        Check(belt.AddFromInventory(inv, 0, out string bwhy) && belt.Has(FiremakingModule.Tinderbox) && inv[0].IsEmpty, "add a tinderbox: it leaves the backpack");
        Check(!belt.AddFromInventory(inv, 1, out bwhy) && bwhy == "That is already on your tool belt." && !inv[1].IsEmpty, "a second one stays in the backpack");
        Check(!belt.AddFromInventory(inv, 2, out bwhy) && !inv[2].IsEmpty, "logs aren't a tool");
        Check(ToolBelt.HasItem(belt, new Inventory(), FiremakingModule.Tinderbox), "a tool on the belt counts with an empty backpack");
        inv.Clear();

        info("Equipment and combat");
        var fresh = new SkillSet();
        Check(fresh.GetLevel(SkillIds.Hitpoints) == 10 && fresh.GetLevel(SkillIds.Attack) == 1, "Hitpoints starts at 10, the rest at 1");
        Check(CombatRules.CombatLevel(fresh) == 3, "a new player is combat level 3");
        foreach (var id in new[] { SkillIds.Attack, SkillIds.Strength, SkillIds.Defence, SkillIds.Hitpoints }) fresh.SetLevel(id, 99);
        Check(CombatRules.CombatLevel(fresh) == 113, "99 in Attack, Strength, Defence and Hitpoints is combat level 113");
        var eq = new Equipment();
        inv.Clear();
        inv.Add(Sword);
        inv.Add(Shield);
        inv.Add(Logs);
        Check(eq.Equip(inv, 0) && eq.Equip(inv, 1) && !eq.Equip(inv, 2), "wield the sword and shield, but not logs");
        Check(eq.IsWearing(EquipSlot.Weapon) && eq.IsWearing(EquipSlot.Shield) && inv.FreeSlots == 27, "both worn, out of the inventory");
        var bon = eq.Bonuses;
        Check(bon.StabAttack == 4 && bon.SlashAttack == 3 && bon.CrushAttack == -2 && bon.Strength == 5 && bon.StabDefence == 4 && bon.SlashDefence == 7,
            "bonuses add up per attack type (sword + shield)");
        Check(eq.WeaponKind == CombatModule.Sword && db.WeaponKind(eq.WeaponKind).Styles[0].Type == AttackType.Stab, "a sword stabs first");
        inv.Add(Sword);
        int swordSlot = 0;
        for (int i = 0; i < Inventory.Size; i++) if (!inv[i].IsEmpty && inv[i].ItemId == Sword) swordSlot = i;
        eq.Equip(inv, swordSlot);
        Check(inv[swordSlot].ItemId == Sword && eq.IsWearing(EquipSlot.Weapon), "wielding over a weapon swaps it into the same slot");
        Check(eq.Unequip(EquipSlot.Shield, inv) && !eq.IsWearing(EquipSlot.Shield) && inv.Count(Shield) == 1, "remove the shield");
        inv.Add(Logs, 28);
        Check(!eq.Unequip(EquipSlot.Weapon, inv) && eq.IsWearing(EquipSlot.Weapon), "can't remove with a full inventory");
        Check(CombatRules.MaxHit(1, 5, CombatStyle.Accurate) == 1 && CombatRules.MaxHit(99, 0, CombatStyle.Aggressive) == 11, "max hits (1 at level 1 with a bronze sword, 11 at 99 unarmed)");
        double hc = CombatRules.HitChance(1, 4, CombatStyle.Accurate, 1, -2);
        Check(hc > 0.5 && hc < 0.7, "level 1 hits a goblin about 6 times in 10 (" + hc.ToString("0.00") + ")");
        var cs = new SkillSet();
        CombatRules.AwardXp(cs, CombatStyle.Aggressive, 3);
        Check(cs.GetXp(SkillIds.Strength) == 12 && cs.GetXpTenths(SkillIds.Hitpoints) == Experience.XpForLevel(10) * 10 + 40, "3 damage: 12 Strength xp, 4 Hitpoints xp");
        var rng = new Random(1);
        bool allCoins = true;
        for (int i = 0; i < 200; i++)
            if (!db.Npc(CombatModule.Goblin).Drops.Roll(rng, out string id, out int n) || id != Coins || n < 1 || n > 30) allCoins = false;
        Check(allCoins, "goblins always drop 1-30 coins");

        // Two-handed weapons and shields.
        var eq2 = new Equipment();
        var inv2 = new Inventory();
        inv2.Add(Shield);
        inv2.Add(SmithingModule.Bronze2h);
        eq2.Equip(inv2, 0);
        Check(eq2.Equip(inv2, 1) && !eq2.IsWearing(EquipSlot.Shield) && inv2.Count(Shield) == 1 && eq2.AttackTicks == 7,
            "a two-handed sword takes the shield off (and is slow: 7 ticks)");
        int shieldSlot = 0;
        for (int i = 0; i < Inventory.Size; i++) if (inv2[i].ItemId == Shield) shieldSlot = i;
        Check(eq2.Equip(inv2, shieldSlot) && !eq2.IsWearing(EquipSlot.Weapon) && inv2.Count(SmithingModule.Bronze2h) == 1, "...and a shield takes it off");
        inv2.Clear();
        eq2.Clear();
        inv2.Add(SmithingModule.Bronze2h);
        eq2.Set(EquipSlot.Shield, new ItemStack(Shield, 1));
        eq2.Set(EquipSlot.Weapon, new ItemStack(Sword, 1));
        inv2.Add(Logs, 27);
        Check(!eq2.Equip(inv2, 0, out string why2) && why2 != null && eq2.IsWearing(EquipSlot.Shield), "no room for the shield: the two-handed sword stays put");
        Check(db.WeaponKind(WeaponKindIds.Unarmed).Styles.Length == 3 && new Equipment().WeaponKind == WeaponKindIds.Unarmed, "unarmed: punch, kick, block");
        double crushOnGob = CombatRules.HitChance(1, Item(SmithingModule.BronzeMace).Bonuses.CrushAttack, CombatStyle.Accurate, 1, -2);
        double crushWithSword = CombatRules.HitChance(1, Item(Sword).Bonuses.CrushAttack, CombatStyle.Accurate, 1, -2);
        Check(crushOnGob > crushWithSword, "a mace crushes better than a sword");

        info("Death");
        var dinv = new Inventory();
        var deq = new Equipment();
        dinv.Add(Logs, 3);
        dinv.Add(Coins, 0);
        dinv.Add(SmithingModule.BronzePlatebody);
        deq.Set(EquipSlot.Weapon, new ItemStack(SmithingModule.Bronze2h, 1));
        deq.Set(EquipSlot.Head, new ItemStack(SmithingModule.BronzeFullHelm, 1));
        dinv.WithdrawFromPouch(0);
        var kept = new System.Collections.Generic.List<ItemStack>();
        var lost = new System.Collections.Generic.List<ItemStack>();
        Death.Split(dinv, deq, Death.KeptItems, kept, lost);
        bool keptRight = kept.Count == 3 && kept.Exists(k => k.ItemId == SmithingModule.BronzePlatebody) && kept.Exists(k => k.ItemId == SmithingModule.Bronze2h)
            && kept.Exists(k => k.ItemId == SmithingModule.BronzeFullHelm);
        info("  kept: " + string.Join(", ", kept.ConvertAll(k => k.ItemId + " x" + k.Count).ToArray()) + "; lost: " + string.Join(", ", lost.ConvertAll(k => k.ItemId + " x" + k.Count).ToArray()));
        Check(keptRight && lost.TrueForAll(l => l.ItemId == Logs) && lost.Count == 3, "dying keeps the 3 most valuable items (platebody, 2h, helm); logs are lost");
        Check(dinv.FreeSlots == Inventory.Size && !deq.IsWearing(EquipSlot.Weapon), "...and everything comes off");
        var sinv2 = new Inventory();
        sinv2.Add(SmithingModule.BronzeBar, 5);
        kept.Clear();
        lost.Clear();
        Death.Split(sinv2, new Equipment(), 3, kept, lost);
        int keptBars = 0, lostBars = 0;
        foreach (var k in kept) keptBars += k.Count;
        foreach (var l in lost) lostBars += l.Count;
        Check(keptBars == 3 && lostBars == 2, "it's 3 items, not 3 stacks or slots");

        info("Abilities");
        Check(Abilities.All.Length == 9, "nine basic melee abilities for a 9-slot bar");
        bool ordered = true;
        for (int i = 0; i < Abilities.All.Length; i++) if ((int)Abilities.All[i].Id != i) ordered = false;
        Check(ordered, "the bar order matches AbilityId");
        var cd = new AbilityCooldowns();
        cd.Use(AbilityId.Slice, 10f);
        Check(!cd.IsReady(AbilityId.Havoc, 11f) && cd.IsReady(AbilityId.Havoc, 11.8f), "1.8 s global cooldown");
        Check(!cd.IsReady(AbilityId.Slice, 12.9f) && cd.IsReady(AbilityId.Slice, 13f), "Slice's own 3 s cooldown");
        Check(Math.Abs(cd.Remaining(AbilityId.Havoc, 11f) - 0.8f) < 1e-4, "remaining shows the global cooldown");
        var arng = new Random(5);
        bool min1 = true, maxOk = true;
        for (int i = 0; i < 500; i++)
        {
            int d = Abilities.RollDamage(arng, Abilities.Get(AbilityId.Sever), 1.0, 10, 1.0);
            if (d < 1) min1 = false;
            if (d > 19) maxOk = false;
        }
        Check(min1 && maxOk, "Sever always lands 1..19 at max hit 10 when it hits");
        Check(Abilities.RollDamage(new Random(1), Abilities.Get(AbilityId.Slice), 0.0, 10, 1.0) == 0, "a miss does 0");
        int low = 0, high = 0;
        for (int i = 0; i < 300; i++)
        {
            low += Abilities.RollDamage(new Random(i), Abilities.Get(AbilityId.Punish), 1.0, 10, 0.3);
            high += Abilities.RollDamage(new Random(i), Abilities.Get(AbilityId.Punish), 1.0, 10, 0.9);
        }
        Check(low > high * 2.5, "Punish hits about 3x harder below half health");
        var bleed = Abilities.SplitBleed(7);
        Check(bleed.Length == 3 && bleed[0] + bleed[1] + bleed[2] == 7 && bleed[0] == 3, "a bleed of 7 lands as 3, 2, 2");

        info("Firemaking and fishing");
        var burn = db.Burnable(Logs);
        Check(burn != null && burn.Xp == 40 && db.Burnable(Coins) == null, "logs burn for 40 xp; coins don't burn");
        double l1 = Firemaking.LightChance(burn, 1);
        Check(l1 > 0.2 && l1 < 0.3 && Firemaking.LightChance(burn, 43) == 1.0, "about 1 in 4 to light at level 1, certain at 43");
        var spot = db.Node(FishingModule.NetSpot);
        var frng = new Random(3);
        bool onlyShrimp = true;
        for (int i = 0; i < 300; i++) { var f = Gathering.Roll(spot, 14, frng); if (f != null && f.ItemId != FishingModule.RawShrimps) onlyShrimp = false; }
        Check(onlyShrimp, "below level 15 a net spot only gives shrimps");
        int anch = 0, shr = 0, none = 0;
        for (int i = 0; i < 2000; i++) { var f = Gathering.Roll(spot, 30, frng); if (f == null) none++; else if (f.ItemId == FishingModule.RawAnchovies) anch++; else shr++; }
        Check(anch > 100 && shr > 100, "at 30 both anchovies (" + anch + ") and shrimps (" + shr + ") bite");
        Check(spot.MinLevel == 1 && Gathering.RollLives(spot, frng) == 0, "net fishing starts at level 1 and never runs out");
        Check(new SkillSet().GetLevel(FishingModule.Skill) == 1 && db.Skill(FiremakingModule.Skill).Name == "Firemaking", "Firemaking and Fishing skills exist");

        info("Cooking");
        var cook = db.RecipeUsing(FiremakingModule.Fire, FishingModule.RawShrimps);
        Check(cook != null && cook.Product == CookingModule.Shrimps && cook.Xp == 30 && db.RecipeUsing(FiremakingModule.Fire, Logs) == null, "raw shrimps cook into shrimps for 30 xp; logs don't cook");
        Check(db.RecipeUsing(FiremakingModule.Fire, FishingModule.RawAnchovies).FailProduct == CookingModule.BurntFish, "anchovies burn into burnt fish");
        double cookChance = Making.SuccessChance(cook, 1);
        Check(cookChance > 0.25 && cookChance < 0.4, "about 1 in 3 cooked at level 1 (" + cookChance.ToString("0.00") + ")");
        Check(Making.SuccessChance(cook, 33) < 1.0 && Making.SuccessChance(cook, 34) == 1.0, "stops burning at level 34");
        var cinv = new Inventory();
        cinv.Add(Logs);
        cinv.Add(FishingModule.RawShrimps);
        bool failed;
        Check(Making.Make(cook, cinv, 99, new Random(1), out failed) == CookingModule.Shrimps && !failed && cinv[1].ItemId == CookingModule.Shrimps, "cooked fish stay in their slot");

        info("Health and eating");
        var hp = new Health(10);
        Check(hp.Current == 10 && hp.IsFull, "start at full health");
        Check(hp.Damage(7) == 7 && hp.Current == 3 && hp.Damage(50) == 3 && hp.IsDead, "damage stops at 0");
        hp.Set(4);
        inv.Clear();
        inv.Add(CookingModule.Shrimps);
        inv.Add(CookingModule.BurntShrimp);
        Check(Foods.Eat(inv, 0, hp, out int healed) != null && healed == 3 && hp.Current == 7 && inv[0].IsEmpty, "shrimps heal 3 and are eaten");
        Check(Foods.Eat(inv, 1, hp, out healed) == null && !inv[1].IsEmpty, "burnt shrimp can't be eaten");
        inv.Add(CookingModule.Shrimps);
        Foods.Eat(inv, 0, hp, out healed);
        Check(healed == 3 && hp.Current == 10, "healing stops at the maximum");
        hp.Set(6);
        hp.SetMax(12);
        Check(hp.Max == 12 && hp.Current == 8, "a Hitpoints level gained adds to current health");
        inv.Clear();

        info("Prayer and monsters fighting back");
        var pb = new PrayerBook(10);
        Check(pb.Toggle(PrayerId.ThickSkin, 10) == null && pb.IsActive(PrayerId.ThickSkin) && Math.Abs(pb.DefenceMultiplier - 1.05) < 1e-9, "Thick Skin: +5% Defence");
        Check(pb.Toggle(PrayerId.RockSkin, 10) == null && !pb.IsActive(PrayerId.ThickSkin) && Math.Abs(pb.DefenceMultiplier - 1.10) < 1e-9, "Rock Skin replaces Thick Skin");
        Check(pb.Toggle(PrayerId.ProtectFromMelee, 10) != null && !pb.ProtectsFromMelee, "Protect from Melee needs level 43");
        pb.Toggle(PrayerId.RockSkin, 10);
        pb.Toggle(PrayerId.ThickSkin, 10);
        for (int i = 0; i < 59; i++) pb.Tick();
        Check(pb.Points == 10, "Thick Skin hasn't drained a point after 59 ticks");
        pb.Tick();
        Check(pb.Points == 9, "...and has after 60 ticks (36 s)");
        var pb2 = new PrayerBook(43);
        pb2.Toggle(PrayerId.ProtectFromMelee, 43);
        for (int i = 0; i < 5; i++) pb2.Tick();
        Check(pb2.Points == 42 && pb2.ProtectsFromMelee, "Protect from Melee drains a point every 5 ticks");
        pb2.SetPoints(1);
        for (int i = 0; i < 5; i++) pb2.Tick();
        Check(pb2.Points == 0 && !pb2.AnyActive, "out of points: prayers switch off");
        pb2.Recharge();
        Check(pb2.Points == 43, "an altar recharges points");
        Check(Item(PrayerModule.Bones).BuryXp == 4.5 && Item(Logs).BuryXp == 0, "bones bury for 4.5 Prayer xp");
        Check(db.Npc(CombatModule.Goblin).AlwaysDrops.Count == 1 && db.Npc(CombatModule.Goblin).AlwaysDrops[0] == PrayerModule.Bones, "goblins always drop bones");
        double gob = CombatRules.MonsterHitChance(db.Npc(CombatModule.Goblin), 1, 0, CombatStyle.Accurate);
        double gobDef = CombatRules.MonsterHitChance(db.Npc(CombatModule.Goblin), 20, 5, CombatStyle.Defensive, 1.1);
        Check(gob > 0.4 && gob < 0.6 && gobDef < gob / 2, "a goblin hits a new player about half the time (" + gob.ToString("0.00") + "), far less with Defence");
        Check(CombatRules.MaxHit(10, 0, CombatStyle.Accurate, 1.1) == CombatRules.MaxHit(11, 0, CombatStyle.Accurate), "Strength prayers boost the level used for max hit");
        var ps = new SkillSet();
        foreach (var id in new[] { SkillIds.Attack, SkillIds.Strength, SkillIds.Defence, SkillIds.Hitpoints, SkillIds.Prayer }) ps.SetLevel(id, 99);
        Check(CombatRules.CombatLevel(ps) == 126, "99 in every melee skill and Prayer is combat level 126");

        info("Woodcutting");
        var treeYield = db.Node(WoodcuttingModule.Tree).Yields[0];
        double c1 = Gathering.Chance(treeYield, 1), c99 = Gathering.Chance(treeYield, 99);
        Check(Math.Abs(c1 - 65 / 256.0) < 1e-9 && Math.Abs(c99 - 201 / 256.0) < 1e-9, "chance 25% at level 1, 79% at 99");
        Check(Gathering.Chance(treeYield, 999) == 1.0, "certain at high levels");
        int lives = Gathering.RollLives(db.Node(WoodcuttingModule.Tree), new Random(2));
        Check(lives >= 3 && lives <= 6, "a tree gives 3-6 logs");

        info("Mining and smithing");
        var copper = db.Node(MiningModule.CopperRock);
        Check(copper.Yields[0].ItemId == MiningModule.CopperOre && db.Node(MiningModule.TinRock).Yields[0].Xp == 17.5, "copper and tin rocks give their ore for 17.5 xp");
        double m1 = Gathering.Chance(copper.Yields[0], 1);
        Check(m1 > 0.35 && m1 < 0.45, "about 2 in 5 swings get ore at level 1 (" + m1.ToString("0.00") + ")");
        var bar = db.Recipe("smelt_bronze_bar");
        var plate = db.Recipe("smith_bronze_platebody");
        var helm = db.Recipe("smith_bronze_full_helm");
        var sinv = new Inventory();
        var srng = new Random(1);
        sinv.Add(MiningModule.CopperOre, 6);
        sinv.Add(MiningModule.TinOre, 5);
        Check(Making.CanMake(bar, sinv) == 5, "6 copper + 5 tin make 5 bronze bars");
        Check(Making.Problem(bar, sinv, 1, false) == null, "level 1 can smelt bronze");
        for (int i = 0; i < 5; i++) Making.Make(bar, sinv, 1, srng, out failed);
        Check(sinv.Count(SmithingModule.BronzeBar) == 5 && sinv.Count(MiningModule.TinOre) == 0 && sinv.Count(MiningModule.CopperOre) == 1, "smelted: 5 bars, 1 copper left");
        Check(Making.Make(bar, sinv, 1, srng, out failed) == null, "no tin, no bar");
        Check(Making.Problem(plate, sinv, 17, true) != null && Making.Problem(plate, sinv, 18, true) == null, "platebody needs level 18");
        Check(Making.Problem(plate, sinv, 18, false) == "You need a hammer to do that.", "smithing needs a hammer");
        Check(Making.Make(plate, sinv, 18, srng, out failed) == SmithingModule.BronzePlatebody && sinv.Count(SmithingModule.BronzeBar) == 0 && sinv.Count(SmithingModule.BronzePlatebody) == 1, "a platebody takes 5 bars");
        Check(Making.Problem(helm, sinv, 99, true) == "You need 2 bronze bars to make a bronze full helm.", "says what's missing");
        Check(db.RecipesAt(SmithingModule.Anvil).Count == 8 && db.RecipesAt(SmithingModule.Furnace).Count == 1, "1 furnace recipe, 8 anvil recipes (5 weapons, 3 armour)");
        Check(Item(SmithingModule.BronzeFullHelm).Slot == EquipSlot.Head && Item(SmithingModule.BronzePlatebody).Slot == EquipSlot.Body && Item(SmithingModule.BronzePlatelegs).Slot == EquipSlot.Legs, "armour goes on head, body and legs");
        Check(ToolBelt.IsTool(MiningModule.BronzePickaxe) && ToolBelt.IsTool(SmithingModule.Hammer), "pickaxe and hammer go on the tool belt");

        info("World generation");
        foreach (int seed in new[] { 2024, 1, 77, 31337 })
        {
            var ow = new Overworld(seed);
            var again = new Overworld(seed);
            const int lo = -96, hi = 160, size = hi - lo;  // a 256 x 256 window round the start
            bool same = true;
            var blocked = new bool[size, size];
            int trees = 0, water = 0, rocks = 0, towns = 0, camps = 0;
            for (int x = lo; x < hi; x++)
                for (int z = lo; z < hi; z++)
                {
                    var t = new Tile(x, z);
                    var k = ow.At(t);
                    if (k != again.At(t)) same = false;
                    if (k == Terrain.Tree) trees++;
                    if (k == Terrain.Water) water++;
                    if (k == Terrain.Rock) rocks++;
                    blocked[x - lo, z - lo] = ow.IsBlocked(t);
                }
            for (int rx = -1; rx <= 1; rx++)
                for (int rz = -1; rz <= 1; rz++)
                {
                    var kind = ow.Plan(rx, rz).Kind;
                    if (kind == RegionKind.Town) towns++;
                    if (kind == RegionKind.Camp) camps++;
                }
            // Everything walkable should be reachable from the start (away from the window's edge,
            // where the way round may lie outside it).
            var reach = new bool[size, size];
            var queue = new System.Collections.Generic.Queue<Tile>();
            var start = ow.Spawn;
            queue.Enqueue(start);
            reach[start.X - lo, start.Z - lo] = true;
            int[] ddx = { 1, -1, 0, 0 }, ddz = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                var t = queue.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int nx = t.X + ddx[i] - lo, nz = t.Z + ddz[i] - lo;
                    if (nx < 0 || nz < 0 || nx >= size || nz >= size || blocked[nx, nz] || reach[nx, nz]) continue;
                    reach[nx, nz] = true;
                    queue.Enqueue(new Tile(t.X + ddx[i], t.Z + ddz[i]));
                }
            }
            int stranded = 0;
            for (int x = 24; x < size - 24; x++)
                for (int z = 24; z < size - 24; z++)
                    if (!blocked[x, z] && !reach[x, z]) stranded++;
            var home = ow.Plan(0, 0);
            bool kinds = home.Kind == RegionKind.Town && home.Buildings.Exists(b => b.Kind == BuildingKind.Bank)
                && home.Buildings.Exists(b => b.Kind == BuildingKind.Shop) && home.Buildings.Exists(b => b.Kind == BuildingKind.Chapel)
                && home.Buildings.Exists(b => b.Kind == BuildingKind.House);
            bool doors = home.Buildings.TrueForAll(b =>
            {
                var o = WorldGen.OutsideDoor(b);
                if (!reach[b.Door.X - lo, b.Door.Z - lo] || !reach[o.X - lo, o.Z - lo]) return false;
                foreach (var n in new[] { new Tile(b.Feature.X + 1, b.Feature.Z), new Tile(b.Feature.X - 1, b.Feature.Z), new Tile(b.Feature.X, b.Feature.Z + 1), new Tile(b.Feature.X, b.Feature.Z - 1) })
                    if (reach[n.X - lo, n.Z - lo]) return true;
                return false;
            });
            Check(same && kinds && doors && stranded == 0 && trees > 1500 && water > 100 && rocks > 20,
                "seed " + seed + ": same every time, a home town with bank + shop + chapel + houses, doors and counters reachable, "
                + towns + " towns and " + camps + " camps nearby, " + trees + " trees, " + rocks + " rocks, " + water + " water tiles, "
                + stranded + " walled-in tiles");
        }
        var far = new Overworld(5);
        Check(far.At(new Tile(-1000000, 2000000)) == far.At(new Tile(-1000000, 2000000)) && far.Plan(-15625, 31250) != null, "it goes on for ever (a million tiles out works)");
        var cg = new ChunkedGrid(16);
        cg.Load(-1, 0, t => t.X == -3);
        Check(cg.ChunkOf(-1) == -1 && cg.ChunkOf(-16) == -1 && cg.ChunkOf(-17) == -2 && cg.ChunkOf(15) == 0, "negative tiles fall in negative chunks");
        Check(cg.IsWalkable(new Tile(-1, 5)) && !cg.IsWalkable(new Tile(-3, 5)) && !cg.IsWalkable(new Tile(1, 5)), "a chunked grid: loaded tiles, blocked tiles, and nothing outside");

        info("Pathfinding");
        var grid = new TileGrid(10, 10);
        for (int z = 0; z < 9; z++)
            grid.SetBlocked(new Tile(5, z), true);  // wall with a gap at the top
        var path = Pathfinder.PathTo(grid, new Tile(2, 2), new Tile(8, 2));
        Check(path.Count > 0 && path[path.Count - 1] == new Tile(8, 2), "walks round a wall");
        bool valid = true;
        var prev = new Tile(2, 2);
        foreach (var t in path) { if (!grid.CanStep(prev, t.X - prev.X, t.Z - prev.Z)) valid = false; prev = t; }
        Check(valid, "every step is legal (no corner cutting)");
        var toBlocked = Pathfinder.PathTo(grid, new Tile(2, 2), new Tile(5, 2));
        Check(toBlocked.Count > 0 && toBlocked[toBlocked.Count - 1] == new Tile(4, 2), "clicking a wall walks next to it");
        var tree = Pathfinder.PathToInteract(grid, new Tile(0, 0), new Tile(5, 4));
        Check(tree != null && tree[tree.Count - 1].IsCardinalNeighbour(new Tile(5, 4)) && grid.IsWalkable(tree[tree.Count - 1]), "walks beside a tree");
        var boxed = new TileGrid(5, 5);
        foreach (var t in new[] { new Tile(1, 2), new Tile(3, 2), new Tile(2, 1), new Tile(2, 3) }) boxed.SetBlocked(t, true);
        Check(Pathfinder.PathToInteract(boxed, new Tile(0, 0), new Tile(2, 2)) == null, "a surrounded tree can't be reached");

    }
}
}
