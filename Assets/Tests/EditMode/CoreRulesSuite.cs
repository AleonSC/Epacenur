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
    static int SlotOf(Inventory inv, string id)
    {
        for (int i = 0; i < Inventory.Size; i++)
            if (!inv[i].IsEmpty && inv[i].ItemId == id) return i;
        return -1;
    }
    const string Logs = WoodcuttingModule.Logs, Coins = ItemIds.Coins, Sword = CombatModule.BronzeSword, Shield = CombatModule.WoodenShield;

    /// `check(ok, description)` is called for every check, `info` for section headings and notes.
    public static void Run(Action<bool, string> checkCallback, Action<string> infoCallback)
    {
        check = checkCallback;
        info = infoCallback;
        info("Content");
        var db = ContentDb.Load(Modules.All);
        Check(db.Validate().Count == 0, "every id the content refers to exists");
        Check(db.Skills.Count == 20 && db.Skill(SkillIds.Hitpoints).StartLevel == 10, "20 skills; Hitpoints starts at 10");
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
        Check(shop.Items.Count == 24 && shop.Items[0].ItemId == FiremakingModule.Tinderbox && shop.Items[1].ItemId == Sword && shop.Items[2].ItemId == Shield, "stocks a tinderbox, a sword and a shield");
        inv.Clear();
        string why;
        Check(shop.Buy(inv, 1, 1, out why) == 0 && why == "You don't have enough coins.", "can't buy without coins");
        inv.Add(Coins, 60);
        Check(shop.Buy(inv, 1, 10, out why) == 2 && inv.Count(Sword) == 2 && inv.Count(Coins) == 8, "60 coins buy two 26-coin swords");
        Check(shop.Items[1].Stock == 3, "stock goes down");
        Check(shop.Sell(inv, Sword, 1, out why) == 1 && inv.Count(Coins) == 18 && shop.Items[1].Stock == 4, "selling a sword pays 10 (40%)");
        Check(shop.Sell(inv, Coins, 1, out why) == 0, "coins can't be sold");
        inv.Add(Logs, 3);
        Check(shop.Sell(inv, Logs, 5, out why) == 3 && shop.Items.Count == 25 && inv.Count(Coins) == 21, "the shop buys logs it doesn't stock (1 coin each)");
        for (int i = 0; i < 5; i++) shop.Restock();
        Check(shop.Items.Count == 24 && shop.Items[1].Stock == 5, "restocking brings stock back to normal and clears sold logs");
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
            if (!db.Npc(CombatModule.Goblin).Drops.Roll(rng, out string id, out int n) || (id == Coins ? n < 1 || n > 30 : !id.StartsWith("grimy_") && id != HerbloreModule.EyeOfNewt)) allCoins = false;
        Check(allCoins, "goblins always drop 1-30 coins, or a herb or an eye of newt");

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
        Check(!eq2.Equip(inv2, 0, null, out string why2) && why2 != null && eq2.IsWearing(EquipSlot.Shield), "no room for the shield: the two-handed sword stays put");
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
        Check(db.RecipesAt(SmithingModule.Anvil).Count == 57 && db.RecipesAt(SmithingModule.Furnace).Count == 5, "5 bars to smelt; 11 things to smith from each (arrowtips too), and nails from bronze or iron");
        Check(Item(SmithingModule.BronzeFullHelm).Slot == EquipSlot.Head && Item(SmithingModule.BronzePlatebody).Slot == EquipSlot.Body && Item(SmithingModule.BronzePlatelegs).Slot == EquipSlot.Legs, "armour goes on head, body and legs");
        Check(ToolBelt.IsTool(MiningModule.BronzePickaxe) && ToolBelt.IsTool(SmithingModule.Hammer), "pickaxe and hammer go on the tool belt");

        info("Tiers");
        var tsk = new SkillSet();
        var teq = new Equipment();
        var tinv = new Inventory();
        tinv.Add("mithril_scimitar");
        tsk.SetLevel(SkillIds.Attack, 19);
        Check(!teq.Equip(tinv, 0, tsk, out string twhy) && twhy == "You need an Attack level of 20 to wield the mithril scimitar.", "mithril needs Attack 20 (" + twhy + ")");
        tsk.SetLevel(SkillIds.Attack, 20);
        Check(teq.Equip(tinv, 0, tsk, out twhy), "...and is wielded at 20");
        Check(Item("adamant_platebody").Bonuses.StabDefence > Item("steel_platebody").Bonuses.StabDefence
            && Item("steel_platebody").Bonuses.StabDefence > Item(SmithingModule.BronzePlatebody).Bonuses.StabDefence, "better metals defend better");
        var tbelt = new ToolBelt();
        tbelt.Add(MiningModule.BronzePickaxe);
        tbelt.Add("mithril_pickaxe");
        var msk = new SkillSet();
        msk.SetLevel(MiningModule.Skill, 20);
        Check(ToolBelt.Find(tbelt, new Inventory(), "pickaxe", msk).Id == MiningModule.BronzePickaxe, "at Mining 20 the mithril pickaxe can't be used yet");
        msk.SetLevel(MiningModule.Skill, 21);
        Check(ToolBelt.Find(tbelt, new Inventory(), "pickaxe", msk).Id == "mithril_pickaxe", "...at 21 it's the one you use");
        var oakYield = db.Node(WoodcuttingModule.Oak).Yields[0];
        Check(Gathering.Chance(oakYield, 30, 4) > Gathering.Chance(oakYield, 30, 1) * 1.5, "a mithril hatchet chops much faster than bronze");
        var steelBar = db.Recipe("smelt_steel_bar");
        var cinv2 = new Inventory();
        cinv2.Add(MiningModule.IronOre, 2);
        cinv2.Add(MiningModule.Coal, 3);
        Check(Making.CanMake(steelBar, cinv2) == 1 && steelBar.Level == 30, "steel takes an iron ore and 2 coal, at Smithing 30");
        Check(db.Recipe("smith_iron_platebody").Level == 33 && db.Recipe("smith_adamant_platebody").Level == 88 && db.Recipe("smith_bronze_dagger").Level == 1,
            "smithing levels as in OSRS (iron platebody 33, adamant 88)");
        var lob = db.RecipeUsing(FiremakingModule.Fire, FishingModule.RawLobster);
        Check(lob.Level == 40 && Making.SuccessChance(lob, 73) < 1.0 && Making.SuccessChance(lob, 74) == 1.0, "lobsters stop burning at 74");
        Check(db.Node(FishingModule.LureSpot).BaitItemId == FishingModule.Feathers && db.Item(FishingModule.Feathers).Stackable, "fly fishing uses feathers");
        var noDrop = new DropTable().Add(null, 0, 0, 1);
        Check(!noDrop.Roll(new Random(1), out string nid, out int ncount), "a drop table can drop nothing");

        info("Ranged and fletching");
        var rsk = new SkillSet();
        Check(CombatRules.CombatLevel(rsk) == 3, "Ranged 1 leaves a new player at combat 3");
        rsk.SetLevel(SkillIds.Ranged, 99);
        Check(CombatRules.CombatLevel(rsk) == 50, "99 Ranged alone is combat level 50 (" + CombatRules.CombatLevel(rsk) + ")");
        Check(CombatRules.RangedMaxHit(1, 7, CombatStyle.Accurate) == 1 && CombatRules.RangedMaxHit(99, 31, CombatStyle.Rapid) == 16,
            "ranged max hits (1 at level 1 with bronze arrows, 16 at 99 with adamant)");
        var xpr = new SkillSet();
        CombatRules.AwardXp(xpr, CombatStyle.Longrange, 4, true);
        Check(xpr.GetXp(SkillIds.Ranged) == 8 && xpr.GetXp(SkillIds.Defence) == 8, "Longrange splits the xp between Ranged and Defence");
        var req = new Equipment();
        var rinv = new Inventory();
        rinv.Add("maple_shortbow");
        rinv.Add("adamant_arrow", 50);
        rinv.Add("adamant_arrow", 25);
        var rsk2 = new SkillSet();
        rsk2.SetLevel(SkillIds.Ranged, 30);
        Check(req.Equip(rinv, 0, rsk2, out string rwhy) && req.Equip(rinv, 1, rsk2, out rwhy) && req[EquipSlot.Ammo].Count == 75 && req.AmmoProblem() == null,
            "wield a maple shortbow and 75 adamant arrows (stacked in the quiver)");
        Check(req.UseAmmo() && req[EquipSlot.Ammo].Count == 74, "firing uses an arrow");
        var oldBow = new Equipment();
        oldBow.Set(EquipSlot.Weapon, new ItemStack(FletchingModule.Shortbow, 1));
        oldBow.Set(EquipSlot.Ammo, new ItemStack("steel_arrow", 10));
        Check(oldBow.AmmoProblem() == "You can't use steel arrows with a shortbow.", "a plain shortbow can't fire steel arrows (" + oldBow.AmmoProblem() + ")");
        oldBow.Set(EquipSlot.Ammo, default(ItemStack));
        Check(oldBow.AmmoProblem() == "There is no ammo left in your quiver.", "no arrows, no shooting");
        Check(db.WeaponKind(CombatModule.Shortbow).Ranged && Item("yew_longbow").Range == 9 && Item("yew_longbow").TwoHanded, "bows are ranged and two-handed; longbows reach 9 tiles");
        var finv = new Inventory();
        finv.Add(FletchingModule.Knife);
        finv.Add(WoodcuttingModule.OakLogs);
        var shafts = db.Recipe("fletch_oak_shafts");
        Check(Making.Make(shafts, finv, 15, new Random(1), out bool ff) == FletchingModule.ArrowShaft && finv.Count(FletchingModule.ArrowShaft) == 30, "an oak log makes 30 arrow shafts");
        finv.Add(FishingModule.Feathers, 20);
        var headlessR = db.Recipe("fletch_headless_arrows");
        Check(Making.Make(headlessR, finv, 1, new Random(1), out ff, out int times) != null && times == 15 && finv.Count(FletchingModule.HeadlessArrow) == 15
            && finv.Count(FishingModule.Feathers) == 5, "feathering makes 15 headless arrows at once");
        Making.Make(headlessR, finv, 1, new Random(1), out ff, out times);
        Check(times == 5, "...or as many as there are feathers for (5)");
        Check(db.Recipe("string_yew_longbow").Level == 70 && db.Recipe("fletch_yew_longbow_u").ToolType == "knife" && db.Recipe("smith_bronze_arrowtips").ProductCount == 15,
            "yew longbows need Fletching 70; a knife cuts bows; a bar makes 15 arrowtips");

        info("Spawning");
        var sow = new Overworld(2024);
        bool safeOnlyBasics = true, safeHasOak = false, deepHasAdamant = false, deepHasYew = false;
        int deepTiles = 0;
        for (int x = -1200; x < 1200; x += 3)
            for (int z = -1200; z < 1200; z += 7)
            {
                var t = new Tile(x, z);
                int danger = sow.Danger(t);
                var rock = Spawning.NodeAt(sow, t, Terrain.Rock);
                var treeHere = Spawning.NodeAt(sow, t, Terrain.Tree);
                if (danger == 0 && rock.Id != MiningModule.CopperRock && rock.Id != MiningModule.TinRock && rock.Id != ConstructionModule.LimestoneRock) safeOnlyBasics = false;
                if (danger == 0 && treeHere.Id == WoodcuttingModule.Oak) safeHasOak = true;
                if (danger >= 30)
                {
                    deepTiles++;
                    if (rock.Id == MiningModule.AdamantiteRock) deepHasAdamant = true;
                    if (treeHere.Id == WoodcuttingModule.Yew) deepHasYew = true;
                }
            }
        Check(safeOnlyBasics && safeHasOak && deepTiles > 0 && deepHasAdamant && deepHasYew, "copper, tin, limestone and oaks on the safe ground round towns; adamantite and yews deep in the wilderness");
        var homeGroups = Spawning.GroupsIn(sow, sow.Plan(0, 0));
        Check(homeGroups.Count == 3 && (homeGroups[0].Npc.Id == CombatModule.Chicken || homeGroups[0].Npc.Id == CombatModule.Cow), "the home city has chickens or cows (" + homeGroups[0].Npc.Name + ")");
        Check(homeGroups[1].Npc.Pickpocket != null && homeGroups[2].Npc.Pickpocket != null && homeGroups[1].Npc.Pickpocket.Level <= 10,
            "...and two groups of townsfolk to pickpocket (" + homeGroups[1].Npc.Name + ", " + homeGroups[2].Npc.Name + ")");
        int campCount = 0, goblinCamps = 0, wildDeep = 0, dangerous = 0, wildSafe = 0, safeButFierce = 0;
        for (int rx = -14; rx <= 14; rx++)
            for (int rz = -14; rz <= 14; rz++)
            {
                var plan = sow.Plan(rx, rz);
                var groups = Spawning.GroupsIn(sow, plan);
                int danger = sow.Danger(plan.Center);
                if (plan.Kind == RegionKind.Camp)
                {
                    campCount++;
                    if (groups[0].Npc.Id == CombatModule.Goblin) goblinCamps++;
                }
                if (plan.Kind == RegionKind.Wild && danger >= 20)
                {
                    wildDeep++;
                    if (groups.Exists(g => g.Npc.Aggressive)) dangerous++;
                }
                if (danger == 0 && plan.Kind != RegionKind.Camp)
                {
                    wildSafe++;
                    if (groups.Exists(g => g.Npc.Aggressive)) safeButFierce++;
                }
            }
        Check(goblinCamps > 0 && goblinCamps < campCount && wildDeep > 0 && dangerous > wildDeep / 2 && wildSafe > 0 && safeButFierce == 0,
            campCount + " camps (" + goblinCamps + " goblin, the rest fiercer); " + dangerous + " of " + wildDeep + " regions deep in the wilderness have aggressive monsters; none of the "
            + wildSafe + " safe ones do");
        Check(db.Npc(CombatModule.HillGiant).AlwaysDrops[0] == PrayerModule.BigBones && db.Item(PrayerModule.BigBones).BuryXp == 15, "hill giants drop big bones (15 xp)");

        info("Magic and runecrafting");
        var wind = db.Spell("wind_strike");
        var ms = new SkillSet();
        var minv = new Inventory();
        var meq = new Equipment();
        Check(db.Spells.Count == 12 && wind.Level == 1 && wind.MaxHit == 2 && db.Spell("fire_bolt").Level == 35, "12 spells; Wind Strike at level 1 hits up to 2; Fire Bolt at 35");
        Check(Magic.Problem(wind, ms, minv, meq) == "You do not have enough Air runes to cast this spell.", "no runes, no spell");
        minv.Add(RunecraftingModule.Air, 3);
        minv.Add(RunecraftingModule.Mind, 3);
        Check(Magic.Problem(wind, ms, minv, meq) == null, "an air and a mind rune cast Wind Strike");
        Magic.UseRunes(wind, minv, meq);
        Check(minv.Count(RunecraftingModule.Air) == 2 && minv.Count(RunecraftingModule.Mind) == 2, "casting uses the runes up");
        minv.Add(MagicModule.StaffOfAir);
        meq.Equip(minv, SlotOf(minv, MagicModule.StaffOfAir));
        minv.Remove(RunecraftingModule.Air, 2);
        Check(Magic.Problem(wind, ms, minv, meq) == null, "a staff of air stands in for air runes");
        Magic.UseRunes(wind, minv, meq);
        Check(minv.Count(RunecraftingModule.Mind) == 1 && meq.Bonuses.MagicAttack == 10, "...only the mind rune is used; the staff gives +10 magic attack");
        Check(Magic.Problem(db.Spell("fire_bolt"), ms, minv, meq).StartsWith("You need a Magic level of 35"), "Fire Bolt needs Magic 35");
        Check(CombatRules.MagicHitChance(1, 10, 1, 0) > CombatRules.MagicHitChance(1, 0, 1, 0), "magic attack bonus makes spells hit more often");
        BigInteger before = ms.GetXpTenths(SkillIds.Magic);
        CombatRules.AwardMagicXp(ms, wind, 2);
        Check(ms.GetXpTenths(SkillIds.Magic) - before == 95, "Wind Strike gives 5.5 xp plus 2 per damage");
        var mfresh = new SkillSet();
        mfresh.SetLevel(SkillIds.Magic, 99);
        Check(CombatRules.CombatLevel(mfresh) == 50, "99 Magic alone is combat level 50");

        var highAlch = db.Spell(MagicModule.HighAlchemy);
        ms.SetLevel(SkillIds.Magic, 55);
        var ainv = new Inventory();
        ainv.Add(RunecraftingModule.Fire, 5);
        ainv.Add(RunecraftingModule.Nature, 1);
        ainv.Add("mithril_platebody");
        int plateValue = db.Item("mithril_platebody").Value;
        int alched = Magic.Alch(highAlch, ms, ainv, new Equipment(), SlotOf(ainv, "mithril_platebody"), out string aw);
        Check(alched == (int)System.Math.Floor(plateValue * 0.6) && ainv.Count(ItemIds.Coins) == alched && ainv.Count("mithril_platebody") == 0
            && ainv.Count(RunecraftingModule.Fire) == 0 && ainv.Count(RunecraftingModule.Nature) == 0, "High Alchemy turns a platebody into 60% of its value");
        ainv.Add(RunecraftingModule.Nature, 1);
        Check(Magic.Alch(highAlch, ms, ainv, new Equipment(), SlotOf(ainv, RunecraftingModule.Nature), out aw) == -1 && ainv.Count(RunecraftingModule.Nature) == 1,
            "the last nature rune can't alch itself (and is given back)");

        var air = db.Recipe("craft_air_rune");
        var essInv = new Inventory();
        essInv.Add(RunecraftingModule.Essence, 10);
        var rc = new SkillSet();
        Check(Making.Make(air, essInv, 1, new Random(1), out bool rf, out int rtimes) == RunecraftingModule.Air && rtimes == 10 && essInv.Count(RunecraftingModule.Air) == 10
            && essInv.Count(RunecraftingModule.Essence) == 0, "the air altar crafts all the essence at once");
        Check(db.Recipe("craft_law_rune").Level == 54 && db.Station("air_altar").Kind == StationKind.Craft, "law runes need Runecrafting 54; altars craft straight away");
        Check(Gathering.RollLives(db.Node(RunecraftingModule.EssenceRock), new Random(1)) == 0, "rune essence never runs out");
        var rw = new Overworld(77);
        Check(rw.Plan(1, 0).Kind == RegionKind.Shrine && Spawning.ShrineAltar(rw, rw.Plan(1, 0)).Id == "air_altar" && !rw.IsBlocked(new Tile(rw.Plan(1, 0).Altar.X, rw.Plan(1, 0).Altar.Z - 1)),
            "just east of the start there's always an air altar, and you can walk up to it");
        bool nearFire = false, farLaw = false;
        for (int rx = -35; rx <= 35; rx++)
            for (int rz = -35; rz <= 35; rz++)
            {
                var plan = rw.Plan(rx, rz);
                if (plan.Kind != RegionKind.Shrine) continue;
                var altarDef = Spawning.ShrineAltar(rw, plan);
                if (altarDef.Id == "fire_altar" && rw.Danger(plan.Center) < 10) nearFire = true;
                if (altarDef.Id == "law_altar") farLaw = true;
            }
        Check(!nearFire && farLaw, "better altars lie deeper in the wilderness");
        var airShrine = rw.Plan(1, 0);
        int essenceRocks = 0;
        foreach (var e in airShrine.Essence)
            if (rw.At(e) == Terrain.Essence && Spawning.NodeAt(rw, e, Terrain.Essence).Id == RunecraftingModule.EssenceRock && e.StepsTo(airShrine.Altar) <= 5) essenceRocks++;
        Check(essenceRocks >= 2 && essenceRocks == airShrine.Essence.Count && Spawning.NodeAt(rw, new Tile(5, 5), Terrain.Rock).Id != RunecraftingModule.EssenceRock,
            "rune essence boulders ring the altar (" + essenceRocks + " at the air altar), not out in rocky ground");

        int quarries = 0, ownOre = 0, quarryRocks = 0;
        bool quarryNamed = true;
        for (int rx = -12; rx <= 12; rx++)
            for (int rz = -12; rz <= 12; rz++)
            {
                var plan = rw.Plan(rx, rz);
                if (plan.Kind != RegionKind.Quarry) continue;
                quarries++;
                var ore = Spawning.QuarryOre(rw, plan);
                if (ore == null || Spawning.GroupsIn(rw, plan).Count != 0) quarryNamed = false;
                foreach (var t in plan.Rocks)
                {
                    if (rw.At(t) != Terrain.Rock) quarryNamed = false;
                    quarryRocks++;
                    if (Spawning.NodeAt(rw, t, Terrain.Rock) == ore) ownOre++;
                }
                if (rw.At(plan.Cart) != Terrain.Quarry || !rw.IsBlocked(plan.Cart)) quarryNamed = false;
            }
        Check(quarries > 10 && quarryNamed && ownOre > quarryRocks / 2, quarries + " quarries nearby, each mostly one ore (" + ownOre + " of " + quarryRocks + " rocks), with no monsters");
        Check(db.Npc(MagicModule.DarkWizard).AttackType == AttackType.Magic && new Bonuses { MagicDefence = 3 }.Defence(AttackType.Magic) == 3,
            "dark wizards cast spells, which magic defence blocks");

                info("Agility, Thieving and Herblore");
        var energy = new RunEnergy();
        energy.Run(10);
        Check(System.Math.Abs(energy.Value - 91) < 1e-9 && energy.CanRun, "running uses run energy (0.9% a second)");
        energy.Run(1000);
        Check(!energy.CanRun, "...until there's none left");
        Check(RunEnergy.RestorePerSecond(99) > RunEnergy.RestorePerSecond(1) * 2.5, "Agility brings it back faster");
        var laps = new LapTracker();
        bool lapDone = false;
        for (int i = 0; i < 5; i++) lapDone = laps.Crossed("field_course", i);
        Check(lapDone, "five obstacles in order make a lap");
        laps.Crossed("field_course", 0);
        laps.Crossed("field_course", 2);
        lapDone = false;
        for (int i = 3; i < 5; i++) lapDone |= laps.Crossed("field_course", i);
        Check(!lapDone, "skipping one doesn't");
        var lay = AgilityCourse.Layout(new Tile(0, 0));
        bool chained = true;
        for (int i = 0; i < lay.Count; i++)
        {
            var o = lay[i];
            if (!AgilityCourse.OnLoop(new Tile(0, 0), o.Start) || !AgilityCourse.OnLoop(new Tile(0, 0), o.End)) chained = false;
            foreach (var t in o.Covered) if (!AgilityCourse.OnLoop(new Tile(0, 0), t)) chained = false;
        }
        Check(chained && lay[4].End.StepsTo(lay[0].Start) <= 3, "the five obstacles go round the loop and back to the start");
        var agw = new Overworld(13);
        var startCourse = agw.Plan(-1, 0);
        bool reachable = true;
        foreach (var o in startCourse.Obstacles)
            if (agw.IsBlocked(o.Start) || agw.IsBlocked(o.End) || !agw.IsBlocked(o.Covered[0])) reachable = false;
        Check(startCourse.Kind == RegionKind.Course && Spawning.CourseIn(agw, startCourse).Id == AgilityModule.FieldCourse && reachable,
            "just west of the start there's always the field course, its obstacles' ends clear and their middles blocked");
        Check(db.Course("sky_course").Obstacles[0].Level == 80 && db.Courses.Count == 5, "five courses, up to Agility 80");
        Check(Agility.SuccessChance(db.Course("field_course").Obstacles[1], 1) == 1 && Agility.SuccessChance(db.Course("hill_course").Obstacles[0], 20) < 0.8
            && Agility.SuccessChance(db.Course("hill_course").Obstacles[0], 50) == 1, "obstacles fail now and then at their level, never 30 levels on");

        var man = db.Npc(ThievingModule.Man).Pickpocket;
        Check(man.Level == 1 && man.Xp == 8 && Thieving.Chance(man, 40) > Thieving.Chance(man, 1), "a man's pocket: level 1, 8 xp, easier with level");
        var homeTown = agw.Plan(0, 0).Town;
        int stallCount = 0;
        for (int x = 0; x < 64; x++) for (int z = 0; z < 64; z++) if (homeTown.Map[x, z] == Terrain.Stall) stallCount++;
        Check(stallCount == 2, "every town square has two market stalls");
        bool lampsOk = homeTown.Lamps.Count >= 4;
        foreach (var l in homeTown.Lamps)
        {
            if (homeTown.Map[l.X, l.Z] != Terrain.Grass) lampsOk = false;
            foreach (var m in homeTown.Lamps) if (m != l && m.StepsTo(l) < WorldGen.LampSpacing) lampsOk = false;
        }
        Check(lampsOk && agw.IsBlocked(new Tile(homeTown.Lamps[0].X, homeTown.Lamps[0].Z)),
            "towns have lamp posts beside their roads, spread out (" + homeTown.Lamps.Count + " in Epacenur)");
        Check(Spawning.NodeAt(agw, agw.Spawn, Terrain.Stall).Id == ThievingModule.BakeryStall, "near the start they're bakery stalls");

        var boosts = new Boosts();
        var bs = new SkillSet();
        bs.SetLevel(SkillIds.Attack, 50);
        var binv = new Inventory();
        binv.Add("attack_potion_3");
        var bpray = new PrayerBook(1);
        var ben = new RunEnergy();
        Potions.Drink(binv, 0, bs, boosts, bpray, ben);
        Check(boosts.Level(bs, SkillIds.Attack) == 58 && binv[0].ItemId == "attack_potion_2", "an attack potion: Attack 50 to 58 (3 + 10%), two doses left");
        Potions.Drink(binv, 0, bs, boosts, bpray, ben);
        Check(boosts.Level(bs, SkillIds.Attack) == 58, "a second dose doesn't stack");
        boosts.Decay();
        Check(boosts.Level(bs, SkillIds.Attack) == 57, "boosts wear off a level a minute");
        Potions.Drink(binv, 0, bs, boosts, bpray, ben);
        Check(binv[0].ItemId == HerbloreModule.Vial, "the last dose leaves an empty vial");
        Check(db.Recipe("clean_ranarr").Level == 25 && Item("grimy_ranarr").ProcessVerb == "Clean"
            && db.Recipe("mix_prayer").Inputs[0].ItemId == "ranarr_potion_unf" && db.Recipe("mix_prayer").Inputs[1].ItemId == HerbloreModule.SnapeGrass,
            "clean a ranarr at 25, then vial of water, then snape grass: a prayer potion");
        var hinv = new Inventory();
        hinv.Add("guam");
        hinv.Add(HerbloreModule.VialOfWater);
        Check(Making.Make(db.Recipe("mix_guam_unf"), hinv, 3, new Random(1), out bool hf) == "guam_potion_unf" && hinv.Count(HerbloreModule.VialOfWater) == 0,
            "a guam leaf in a vial of water makes an unfinished potion");

                info("Crafting");
        var cinv3 = new Inventory();
        cinv3.Add(CombatModule.Cowhide, 3);
        cinv3.Add(Coins, 2);
        var tan = db.Recipe("tan_leather");
        Check(Making.Make(tan, cinv3, 1, new Random(1), out ff, out times) == CraftingModule.Leather && times == 2 && cinv3.Count(CraftingModule.Leather) == 2
            && cinv3.Count(Coins) == 0 && cinv3.Count(CombatModule.Cowhide) == 1, "tanning costs a coin a hide (2 coins tan 2 of 3 hides)");
        var spin = db.Recipe("spin_bowstring");
        Check(spin.Station == CraftingModule.SpinningWheel && spin.Product == FletchingModule.Bowstring && spin.Level == 10, "flax spins into bowstrings at Crafting 10");
        var body2 = db.Recipe("craft_" + CraftingModule.LeatherBody);
        Check(body2.ToolType == "needle" && body2.Level == 14 && body2.Xp == 25 && Item(CraftingModule.LeatherBody).Slot == EquipSlot.Body
            && Item(CraftingModule.LeatherVambraces).Bonuses.RangedAttack == 4, "a leather body needs a needle and Crafting 14; vambraces help rangers");
        int flaxTiles = 0;
        for (int x = -600; x < 600; x += 2)
            for (int z = -600; z < 600; z += 2)
                if (sow.At(new Tile(x, z)) == Terrain.Flax) flaxTiles++;
        Check(flaxTiles > 20 && Spawning.NodeAt(sow, new Tile(0, 0), Terrain.Flax).Id == CraftingModule.FlaxPlant, "flax fields in the meadows (" + flaxTiles + " plants in a quarter of the tiles round the start)");

        info("World generation");
        foreach (int seed in new[] { 2024, 1, 77, 31337 })
        {
            var ow = new Overworld(seed);
            var again = new Overworld(seed);
            const int lo = -96, hi = 160, size = hi - lo;  // a 256 x 256 window round the start
            bool same = true;
            var blocked = new bool[size, size];
            int trees = 0, water = 0, rocks = 0, wildRocks = 0, towns = 0, camps = 0;
            for (int x = lo; x < hi; x++)
                for (int z = lo; z < hi; z++)
                {
                    var t = new Tile(x, z);
                    var k = ow.At(t);
                    if (k != again.At(t)) same = false;
                    if (k == Terrain.Tree) trees++;
                    if (k == Terrain.Water) water++;
                    if (k == Terrain.Rock) rocks++;
                    if (k == Terrain.Rock && ow.PlanAt(t).Kind != RegionKind.Quarry) wildRocks++;
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
                && home.Buildings.Exists(b => b.Kind == BuildingKind.House) && home.Buildings.Exists(b => b.Kind == BuildingKind.Smithy)
                && home.Buildings.Exists(b => b.Kind == BuildingKind.Workshop);
            bool doors = home.Buildings.TrueForAll(b =>
            {
                var o = WorldGen.OutsideDoor(b);
                if (!reach[b.Door.X - lo, b.Door.Z - lo] || !reach[o.X - lo, o.Z - lo]) return false;
                foreach (var f in b.HasFeature2 ? new[] { b.Feature, b.Feature2 } : new[] { b.Feature })
                {
                    bool ok = false;
                    foreach (var n in new[] { new Tile(f.X + 1, f.Z), new Tile(f.X - 1, f.Z), new Tile(f.X, f.Z + 1), new Tile(f.X, f.Z - 1) })
                        if (reach[n.X - lo, n.Z - lo]) ok = true;
                    if (!ok) return false;
                }
                return true;
            });
            Check(same && kinds && doors && stranded == 0 && trees > 1500 && water > 100 && rocks > 10 && wildRocks == 0 && ow.Plan(0, 1).Kind == RegionKind.Quarry,
                "seed " + seed + ": same every time, a home town with bank, shop, chapel, smithy, workshop and houses, doors and counters reachable, "
                + towns + " towns and " + camps + " camps nearby, " + trees + " trees, " + rocks + " rocks (all in quarries, one just north of the start), " + water + " water tiles, "
                + stranded + " walled-in tiles");
        }
        var qw = new Overworld(2024);
        var starterOre = Spawning.QuarryOre(qw, qw.Plan(0, 1)).Id;
        Check(qw.Plan(0, 1).Kind == RegionKind.Quarry && (starterOre == MiningModule.CopperRock || starterOre == MiningModule.TinRock),
            "the quarry just north of the start is copper or tin (" + starterOre + ")");
        var hw = new Overworld(31);
        double highest = 0, steepest = 0;
        bool levelWhereBuilt = true, waterLevel = true;
        for (int x = -200; x < 200; x += 1)
            for (int z = -200; z < 200; z += 2)
            {
                var t = new Tile(x, z);
                double e = hw.Elevation(t);
                highest = Math.Max(highest, e);
                steepest = Math.Max(steepest, Math.Abs(hw.Elevation(x + 1, z) - e));
                var terr = hw.At(t);
                if ((terr == Terrain.Floor || terr == Terrain.Wall || terr == Terrain.Path || terr == Terrain.Camp || terr == Terrain.Stone) && e > 1e-6) levelWhereBuilt = false;
                if (terr == Terrain.Water && e > 0.05) waterLevel = false;
            }
        info("  hills: highest " + highest.ToString("0.0") + ", steepest step between tiles " + steepest.ToString("0.00"));
        Check(highest > 4 && hw.Elevation(hw.Spawn) == 0, "there are hills, and the town you start in is level");
        Check(levelWhereBuilt && waterLevel, "towns, camps and shrines are flat, and ponds lie level");
        Check(steepest < 2, "no cliffs: neighbouring tiles are less than 2 apart");
                var far = new Overworld(5);
        Check(far.At(new Tile(-1000000, 2000000)) == far.At(new Tile(-1000000, 2000000)) && far.Plan(-15625, 31250) != null, "it goes on for ever (a million tiles out works)");
        var cg = new ChunkedGrid(16);
        cg.Load(-1, 0, t => t.X == -3);
        Check(cg.ChunkOf(-1) == -1 && cg.ChunkOf(-16) == -1 && cg.ChunkOf(-17) == -2 && cg.ChunkOf(15) == 0, "negative tiles fall in negative chunks");
        Check(cg.IsWalkable(new Tile(-1, 5)) && !cg.IsWalkable(new Tile(-3, 5)) && !cg.IsWalkable(new Tile(1, 5)), "a chunked grid: loaded tiles, blocked tiles, and nothing outside");

        info("Towns, cities and the wilderness");
        var ww = new Overworld(2024);
        var homeCity = ww.SettlementOf(0, 0);
        Check(homeCity != null && homeCity.City && homeCity.RX == 0 && homeCity.RZ == 0 && ww.Plan(0, 0).City && ww.Danger(ww.Spawn) == 0,
            "Epacenur, where you start, is a city on safe ground");
        var places = new System.Collections.Generic.List<Settlement>();
        for (int cx = -6; cx <= 6; cx++)
            for (int cz = -6; cz <= 6; cz++)
            {
                var st = ww.SettlementInCell(cx, cz);
                if (st != null) places.Add(st);
            }
        bool apart = true, safeAtHome = true, plannedAsTowns = true;
        int cities = 0;
        foreach (var a1 in places)
        {
            if (a1.City) cities++;
            if (ww.Danger(a1.Center) != 0) safeAtHome = false;
            if (ww.Plan(a1.RX, a1.RZ).Kind != RegionKind.Town || ww.Plan(a1.RX, a1.RZ).City != a1.City) plannedAsTowns = false;
            foreach (var b1 in places)
                if (a1 != b1 && Math.Max(Math.Abs(a1.RX - b1.RX), Math.Abs(a1.RZ - b1.RZ)) < 3) apart = false;
        }
        int strayTowns = 0;
        for (int rx = -30; rx <= 30; rx++)
            for (int rz = -30; rz <= 30; rz++)
            {
                var pl = ww.Plan(rx, rz);
                var st = ww.SettlementOf(rx, rz);
                if (pl.Kind == RegionKind.Town && (st == null || st.RX != rx || st.RZ != rz)) strayTowns++;
            }
        Check(places.Count > 80 && places.Count < 169 && cities > 5 && cities < places.Count / 2 && apart && safeAtHome && plannedAsTowns && strayTowns == 0,
            places.Count + " towns and cities (" + cities + " cities) in 13 x 13 cells, at least 3 regions apart, each on safe ground, and no other towns");
        // Walking from one town to the next: safe, then more and more dangerous, then safe again.
        var from = homeCity;
        Settlement to = null;
        foreach (var p1 in places)
            if (p1 != from && p1.RZ == from.RZ && p1.RX > from.RX && (to == null || p1.RX < to.RX)) to = p1;
        if (to == null)
            foreach (var p1 in places)
                if (p1 != from && (to == null || p1.Center.StepsTo(from.Center) < to.Center.StepsTo(from.Center))) to = p1;
        int peak = 0, peakAt = 0, steps = 64;
        bool rises = true, falls = true;
        int[] along = new int[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            var t = new Tile(from.Center.X + (to.Center.X - from.Center.X) * i / steps, from.Center.Z + (to.Center.Z - from.Center.Z) * i / steps);
            along[i] = ww.Danger(t);
            if (along[i] > peak) { peak = along[i]; peakAt = i; }
        }
        for (int i = 1; i <= peakAt; i++) if (along[i] < along[i - 1] - 1) rises = false;
        for (int i = peakAt + 1; i <= steps; i++) if (along[i] > along[i - 1] + 1) falls = false;
        Check(along[0] == 0 && along[steps] == 0 && peak >= 3 && rises && falls,
            "from Epacenur to " + "the next town" + ": safe, up to wilderness level " + peak + ", then safe again");
        int maxDanger = 0;
        for (int x = -3000; x < 3000; x += 23)
            for (int z = -3000; z < 3000; z += 23)
                maxDanger = Math.Max(maxDanger, ww.Danger(new Tile(x, z)));
        var ns = ww.NearestSettlement(new Tile(to.Center.X + 3, to.Center.Z - 2));
        Check(maxDanger >= 40 && maxDanger <= Overworld.MaxDanger && ns == to, "the deepest wilderness reaches level " + maxDanger + "; the nearest town to a town's square is that town");

        info("Noise and biomes");
        var vn = new ValueNoise(3);
        int repeats = 0;
        for (int i = 0; i < 200; i++)
            if (Math.Abs(vn.Sample(i * 0.37, i * 0.11) - vn.Sample(i * 0.37 + 256, i * 0.11)) < 1e-9) repeats++;
        double fsum = 0, fsq = 0;
        for (int x = 0; x < 300; x++)
            for (int z = 0; z < 300; z++)
            {
                double v = vn.Fractal(x / 7.3, z / 7.3, 4);
                fsum += v; fsq += v * v;
            }
        double fmean = fsum / 90000, fsd = Math.Sqrt(fsq / 90000 - fmean * fmean);
        Check(repeats == 0 && Math.Abs(fmean - 0.5) < 0.03 && fsd > 0.2 && fsd < 0.25, "noise never repeats, and layered noise spreads like plain noise (sd " + fsd.ToString("0.000") + ")");
        var seen = new System.Collections.Generic.Dictionary<Biome, int>();
        bool wasteOnlyDeep = true;
        int flaxOutsideMeadow = 0;
        for (int x = -3000; x < 3000; x += 29)
            for (int z = -3000; z < 3000; z += 29)
            {
                var t = new Tile(x, z);
                var bi = ww.BiomeAt(t);
                seen[bi] = seen.TryGetValue(bi, out int nb) ? nb + 1 : 1;
                if (bi == Biome.Wasteland && ww.DangerAt(x, z) < Overworld.DeepWilderness) wasteOnlyDeep = false;
                if (ww.At(t) == Terrain.Flax && bi != Biome.Meadow) flaxOutsideMeadow++;
            }
        bool all = true;
        string mix = "";
        foreach (Biome bi in Enum.GetValues(typeof(Biome)))
        {
            int n1 = seen.TryGetValue(bi, out int got) ? got : 0;
            if (n1 < 1000) all = false;
            mix += (mix.Length > 0 ? ", " : "") + bi.ToString().ToLowerInvariant() + " " + (n1 * 100 / 42849) + "%";
        }
        Check(all && wasteOnlyDeep && flaxOutsideMeadow == 0, "every biome is common (" + mix + "); wasteland only deep in the wilderness; flax only in meadows");
        int marshWater = 0, marshTiles = 0, meadowWater = 0, meadowTiles = 0, highRock = 0, highTiles = 0, woodTrees = 0, woodTiles = 0, wasteTrees = 0, wasteTiles = 0;
        for (int x = -1500; x < 1500; x += 7)  // (not a multiple of 3: trees and rocks grow on a 3-tile lattice)
            for (int z = -1500; z < 1500; z += 7)
            {
                var t = new Tile(x, z);
                var bi = ww.BiomeAt(t);
                var k = ww.At(t);
                if (bi == Biome.Marsh) { marshTiles++; if (k == Terrain.Water) marshWater++; }
                if (bi == Biome.Meadow) { meadowTiles++; if (k == Terrain.Water) meadowWater++; }
                if (bi == Biome.Highlands) { highTiles++; if (k == Terrain.Rock) highRock++; }
                if (bi == Biome.Woodland) { woodTiles++; if (k == Terrain.Tree) woodTrees++; }
                if (bi == Biome.Wasteland) { wasteTiles++; if (k == Terrain.Tree) wasteTrees++; }
            }
        Check((double)marshWater / marshTiles > 2.0 * meadowWater / meadowTiles && (double)woodTrees / woodTiles > 2.0 * wasteTrees / wasteTiles && highRock > 0,
            "marshes are wetter than meadows (" + (marshWater * 1000 / marshTiles) + " to " + (meadowWater * 1000 / meadowTiles) + " water tiles in 1000), woodland thicker than wasteland ("
            + (woodTrees * 1000 / woodTiles) + " to " + (wasteTrees * 1000 / wasteTiles) + " trees in 1000), and highlands rocky");

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

        info("Sound effects");
        bool allSound = true;
        string worst = "";
        foreach (Sound snd in Enum.GetValues(typeof(Sound)))
        {
            var samples = Synth.Make(snd);
            float loudest = 0;
            bool finite = true;
            foreach (var v in samples) { loudest = Math.Max(loudest, Math.Abs(v)); if (float.IsNaN(v) || float.IsInfinity(v)) finite = false; }
            double seconds = (double)samples.Length / Synth.Rate;
            if (!finite || Math.Abs(loudest - Synth.Peak) > 1e-3 || seconds < 0.02 || seconds > 1.5 || Math.Abs(samples[samples.Length - 1]) > 1e-3)
            {
                allSound = false;
                worst = snd.ToString();
            }
        }
        var chop1 = Synth.Make(Sound.Chop);
        var chop2 = Synth.Make(Sound.Chop);
        bool alike = chop1.Length == chop2.Length;
        for (int i = 0; alike && i < chop1.Length; i++) if (chop1[i] != chop2[i]) alike = false;
        Check(allSound && alike, Enum.GetValues(typeof(Sound)).Length + " sound effects, each made the same every time, short, as loud as the rest and ending in silence" + (worst.Length > 0 ? " (not " + worst + ")" : ""));

        info("Encyclopedia");
        var enc = Encyclopedia.Build(db, Guides.All);
        bool everything = db.Items.TrueForAll(i => enc.Get(Encyclopedia.ItemId(i.Id)) != null) && db.Skills.TrueForAll(k => enc.Get(Encyclopedia.SkillId(k.Id)) != null)
            && db.Npcs.TrueForAll(n => enc.Get("npc:" + n.Id) != null) && db.Pieces.TrueForAll(pc => enc.Get("piece:" + pc.Id) != null);
        bool linksResolve = true, noneEmpty = true;
        foreach (var page in enc.Entries)
        {
            if (page.Lines.Count == 0) noneEmpty = false;
            foreach (var link in page.Links) if (enc.Get(link) == null) { linksResolve = false; info("  broken link " + page.Id + " -> " + link); }
        }
        Check(everything && linksResolve && noneEmpty, enc.Entries.Count + " pages: every item, skill, monster and building piece, each with something to say, and every link leads somewhere");
        var barHits = enc.Search("bronze bar");
        var hide = enc.Search("cowhide");
        var wolf = enc.Search("wolf");
        Check(barHits.Count > 0 && barHits[0].Entry.Id == Encyclopedia.ItemId(SmithingModule.BronzeBar) && wolf.Count > 0 && wolf[0].Entry.Id == "npc:" + CombatModule.Wolf,
            "a page's own title comes first (bronze bar, wolf)");
        Check(hide.Exists(h => h.Entry.Id == "npc:" + CombatModule.Cow) && hide.Exists(h => h.Entry.Id == Encyclopedia.ItemId(CraftingModule.Leather))
            && hide.Exists(h => h.Snippet.ToLowerInvariant().Contains("cowhide")), "searching finds every page that mentions a word (cowhide: the cow that drops it, the leather made from it)");
        var smithing = enc.Get(Encyclopedia.SkillId(SmithingModule.Skill));
        var leather = enc.Get(Encyclopedia.ItemId(CraftingModule.Leather));
        Check(smithing.Lines.Exists(l => l.StartsWith("Level 1:")) && leather.Lines.Contains("## Made by") && leather.Lines.Contains("## Used to make")
            && enc.Search("deep wilderness").Exists(h => h.Entry.Id == "guide:The wilderness") && enc.Search("").Count == 0 && enc.Search("zzqqxx").Count == 0,
            "skills list what each level unlocks; items say what they're made from and into; the guides are searchable too");

        info("Construction");
        Check(db.Piece("wooden_wall").Kind == PieceKind.Wall && db.Piece("stone_foundation").Level == 20 && db.Recipe("saw_plank").ToolType == "saw"
            && db.Piece(ConstructionModule.Chest).Use == FurnitureUse.Chest && db.Piece("build_furnace").Station == SmithingModule.Furnace,
            "wooden and stone pieces (stone from Construction 20), planks sawn from logs, chests, beds and stations to build");
        bool keysRoundTrip = true;
        foreach (int ki in new[] { -7, -1, 0, 3 })
            foreach (int kj in new[] { -5, 0, 9 })
                for (int side = 0; side < 2; side++)
                {
                    Structure.UnEdgeKey(Structure.EdgeKey(ki, kj, side), out int bi2, out int bj2, out int bside);
                    if (bi2 != ki || bj2 != kj || bside != side) keysRoundTrip = false;
                }
        // A 3 x 3 base turned 30 degrees on open, flat ground, walled all round but for a doorway on its south side.
        var open = new TileGrid(60, 60);
        var bases = new BaseSet(t => open.IsWalkable(t), t => open.IsWalkable(t), (x, z) => 0.0);
        Check(bases.NewBaseProblem(30.3, 30.6, 30, out double floor) == null && Math.Abs(floor - BaseSet.FloorAbove) < 1e-9, "a base can start anywhere on open ground, at any angle");
        var hut = bases.Create(30.3, 30.6, 30, floor);
        hut.Foundations[Structure.Key(0, 0)] = "wooden_foundation";
        Check(bases.FoundationProblem(hut, 2, 0) != null && bases.FoundationProblem(hut, 1, 0) == null && bases.FoundationProblem(hut, 0, 0) != null,
            "foundations go beside one another, one to a cell");
        for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
                hut.Foundations[Structure.Key(i, j)] = "wooden_foundation";
        for (int k = -1; k <= 1; k++)
        {
            hut.Edges[Structure.EdgeKey(k, 1, 0)] = "wooden_wall";                                  // north
            hut.Edges[Structure.EdgeKey(k, -2, 0)] = k == 0 ? "wooden_doorway" : "wooden_wall";    // south, with the door
            hut.Edges[Structure.EdgeKey(1, k, 1)] = "wooden_wall";                                  // east
            hut.Edges[Structure.EdgeKey(-2, k, 1)] = "wooden_wall";                                 // west
        }
        bases.Refresh();
        hut.ToMap(1, -1, out double mx, out double mz);
        hut.ToLocal(mx, mz, out double li, out double lj);
        var middle = BaseSet.CellTile(hut, 0, 0);
        bases.Centre(middle, out double cx0, out double cz0, out double cy0);
        Check(keysRoundTrip && Math.Abs(li - 1) < 1e-9 && Math.Abs(lj + 1) < 1e-9 && Math.Abs(cx0 - 30.3) < 1e-9 && Math.Abs(cy0 - floor) < 1e-9
            && bases.CellAt(30.3, 30.6, floor) == middle && bases.CellAt(30.3, 30.6, floor - 2) == null && bases.TryCell(middle, out _, out int mi, out int mj) && mi == 0 && mj == 0,
            "a base's cells have tiles of their own, and points on its floor find them");
        var walk = new BaseWalkGrid(open, bases);
        Check(!walk.IsWalkable(new Tile(30, 31)) && walk.IsWalkable(new Tile(20, 20)) && walk.IsWalkable(middle), "the ground under a base is its, not the world's");
        var inside = Pathfinder.PathTo(walk, new Tile(20, 20), middle);
        bool viaDoor = inside.Contains(BaseSet.CellTile(hut, 0, -2)) && inside.Contains(BaseSet.CellTile(hut, 0, -1));
        bool legal = true;
        var at = new Tile(20, 20);
        foreach (var t in inside)
        {
            bool linked = walk.Links(at).Contains(t);
            if (!linked && (Math.Abs(t.X - at.X) > 1 || Math.Abs(t.Z - at.Z) > 1 || !walk.CanStep(at, t.X - at.X, t.Z - at.Z))) legal = false;
            at = t;
        }
        Check(inside.Count > 0 && inside[inside.Count - 1] == middle && viaDoor && legal, "you walk in from outside through the doorway (" + inside.Count + " steps)");
        Check(!walk.CanStep(BaseSet.CellTile(hut, 0, 1), 0, 1) && !walk.CanStep(BaseSet.CellTile(hut, 1, 0), 1, 0) && walk.CanStep(BaseSet.CellTile(hut, 0, -1), 0, -1)
            && walk.CanStep(middle, 1, 1), "walls block, the doorway doesn't, and you can cross a room diagonally");
        hut.Edges[Structure.EdgeKey(0, -2, 0)] = "wooden_wall";
        bases.Refresh();
        var shut = Pathfinder.PathTo(walk, new Tile(20, 20), middle);
        Check(shut.Count == 0 || shut[shut.Count - 1] != middle, "wall the doorway up and there's no way in");
        hut.Edges[Structure.EdgeKey(0, -2, 0)] = "wooden_doorway";
        bases.Refresh();
        Check(BaseSet.RoofProblem(hut, 1, 1) == null && BaseSet.RoofProblem(hut, 0, 0) != null && BaseSet.RoofProblem(hut, 3, 3) != null,
            "a roof needs a wall under it or a roof beside it, and a foundation");
        hut.Roofs[Structure.Key(1, 1)] = "wooden_roof";
        Check(BaseSet.RoofProblem(hut, 0, 0) != null && BaseSet.RoofProblem(hut, 0, 1) == null && BaseSet.EdgeProblem(hut, 5, 5, 0) != null
            && BaseSet.EdgeProblem(hut, 0, 0, 0) == null && BaseSet.EdgeProblem(hut, 0, 1, 0) != null, "walls go on a foundation's edge, one to an edge");
        hut.Furniture[Structure.Key(0, 0)] = new Furnishing { Piece = ConstructionModule.Chest, Store = new Bank(ConstructionModule.ChestSlots) };
        Check(!walk.IsWalkable(middle) && BaseSet.FurnitureProblem(hut, 0, 0) != null && BaseSet.RemoveFoundationProblem(hut, 0, 0) != null
            && BaseSet.RemoveFoundationProblem(hut, 1, 1) != null && BaseSet.RemoveFoundationProblem(hut, 0, -1) != null,
            "furniture fills its cell; a foundation only comes away once nothing's on it or round its edge");
        Check(bases.NewBaseProblem(30.9, 31.2, 0, out _) != null && bases.NewBaseProblem(45, 45, 0, out _) == null, "a new base can't go on top of another");
        var hill = new BaseSet(t => true, t => true, (x, z) => x * 0.6);
        Check(hill.NewBaseProblem(10, 10, 0, out double hillFloor) == null && Math.Abs(hillFloor - (10.5 * 0.6 + BaseSet.FloorAbove)) < 1e-6
            && hill.NewBaseProblem(10, 10, 0, out _) == null, "on a slope, the floor is level with the highest ground under it");
        var steep = hill.Create(10, 10, 0, hillFloor);
        steep.Foundations[Structure.Key(0, 0)] = "wooden_foundation";
        hill.Refresh();
        Check(hill.FoundationProblem(steep, 1, 0) != null && hill.FoundationProblem(steep, -1, 0) == null && hill.FoundationProblem(steep, -4, 0) != null,
            "uphill the floor would be buried; downhill it stands on posts, until the ground falls away too far");
        var chestBank = new Bank(ConstructionModule.ChestSlots);
        for (int i = 0; i < 40; i++) chestBank.Add(db.Items[i].Id, 1);
        Check(chestBank.UsedSlots == ConstructionModule.ChestSlots, "a storage chest holds " + ConstructionModule.ChestSlots + " kinds of item");

    }
}
}
