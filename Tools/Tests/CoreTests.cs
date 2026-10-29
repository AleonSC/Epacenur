using System;
using System.Numerics;
using Epacenur.Core;

/// Checks the game rules that don't need Unity. Run with Tools/Tests/run.sh.
static class CoreTests
{
    static int failures;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
        if (!ok)
            failures++;
    }

    static int Main()
    {
        Console.WriteLine("Experience curve");
        Check(Experience.XpForLevel(2) == 83, "level 2 = 83 xp");
        Check(Experience.XpForLevel(99) == 13034431, "level 99 = 13,034,431 xp");
        Check(Experience.XpForLevel(120) == 104273167, "level 120 = 104,273,167 xp");
        Check(Experience.LevelForXp(200000000) == 126, "200m xp is level 126");
        Check(Experience.LevelForXp(0) == 1 && Experience.LevelForXp(82) == 1 && Experience.LevelForXp(83) == 2, "level boundaries");
        Check(Experience.LevelForXp(Experience.MaxXp) == 999, "max xp is level 999");
        Check(Experience.LevelForXp(Experience.MaxXp * 2) == 999, "levels stop at 999");
        double ratio = (double)(Experience.XpForLevel(600) - Experience.XpForLevel(599)) / (double)(Experience.XpForLevel(599) - Experience.XpForLevel(598));
        Check(Math.Abs(ratio - Math.Pow(2, 1 / 7.0)) < 1e-6, "gap between levels grows by 2^(1/7) = " + ratio.ToString("0.00000"));
        Console.WriteLine("  level 999 needs " + Experience.Format(Experience.MaxXp) + " xp (" + Experience.FormatShort(Experience.MaxXp) + ")");
        var py = BigInteger.Parse("6590753875914258720611083014045249960396472940");
        Check(BigInteger.Abs(Experience.MaxXp - py) <= py / 1000000000000, "level 999 xp matches the formula computed independently");
        for (int l = 1; l < 999; l++)
            if (Experience.XpForLevel(l + 1) <= Experience.XpForLevel(l)) { Check(false, "table rises at level " + l); break; }
        Check(Experience.Format(13034431) == "13,034,431" && Experience.Format(83) == "83", "comma formatting");

        Console.WriteLine("Skills");
        var skills = new SkillSet();
        int ups = 0, lastLevel = 0;
        skills.LevelledUp += (id, lvl) => { ups++; lastLevel = lvl; };
        skills.AddXp(SkillId.Woodcutting, 25);
        Check(skills.GetXp(SkillId.Woodcutting) == 25 && skills.GetLevel(SkillId.Woodcutting) == 1, "25 xp is level 1");
        skills.AddXp(SkillId.Woodcutting, 67.5);
        skills.AddXp(SkillId.Woodcutting, 67.5);
        Check(skills.GetXpTenths(SkillId.Woodcutting) == 1600, "half xp adds up exactly (160.0)");
        Check(ups == 1 && lastLevel == 2, "levelled up once to 2");
        skills.AddXpTenths(SkillId.Woodcutting, Experience.MaxXp * 100);
        Check(skills.GetXp(SkillId.Woodcutting) == Experience.MaxXp && skills.GetLevel(SkillId.Woodcutting) == 999, "xp caps at level 999");
        skills.SetLevel(SkillId.Woodcutting, 99);
        Check(skills.GetLevel(SkillId.Woodcutting) == 99, "set level 99");

        Console.WriteLine("Inventory");
        var inv = new Inventory();
        Check(inv.Add(ItemDefs.Logs.Id, 30) == 28, "28 logs fit, one per slot");
        Check(inv.IsFull && !inv.HasRoomFor(ItemDefs.Logs.Id) && inv.Count(ItemDefs.Logs.Id) == 28, "inventory full of logs");
        inv.RemoveAt(5);
        Check(inv.FreeSlots == 1 && inv.Add(ItemDefs.Logs.Id) == 1 && !inv[5].IsEmpty, "a log goes in the freed slot");

        Console.WriteLine("Bank");
        var bank = new Bank();
        inv.Clear();
        inv.Add(ItemDefs.Logs.Id, 10);
        Check(bank.Deposit(inv, ItemDefs.Logs.Id, 3) == 3 && inv.Count(ItemDefs.Logs.Id) == 7, "deposit 3 of 10 logs");
        Check(bank.UsedSlots == 1 && bank[0].Count == 3, "logs stack in one bank slot");
        Check(bank.DepositAll(inv) == 7 && inv.FreeSlots == Inventory.Size && bank.UsedSlots == 1 && bank.Count(ItemDefs.Logs.Id) == 10, "deposit inventory: all 10 logs in one slot");
        bank.Add(ItemDefs.Logs.Id, 90);
        Check(bank.Withdraw(inv, 0, long.MaxValue) == 28 && bank.Count(ItemDefs.Logs.Id) == 72, "withdraw all: only 28 fit");
        Check(bank.Withdraw(inv, 0, 5) == 0, "nothing withdrawn into a full inventory");
        inv.Clear();
        bank.Clear();
        bank.Add(ItemDefs.Logs.Id, 5);
        Check(bank.Withdraw(inv, 0, long.MaxValue) == 5 && bank.UsedSlots == 0 && inv.Count(ItemDefs.Logs.Id) == 5, "an emptied bank slot is removed");

        Console.WriteLine("Shop");
        var shop = Shops.DevAreaStore();
        Check(shop.Items.Count == 4 && shop.Items[0].Def == ItemDefs.Tinderbox && shop.Items[1].Def == ItemDefs.BronzeSword && shop.Items[2].Def == ItemDefs.WoodenShield, "stocks a tinderbox, a sword and a shield");
        inv.Clear();
        string why;
        Check(shop.Buy(inv, 1, 1, out why) == 0 && why == "You don't have enough coins.", "can't buy without coins");
        inv.Add(ItemDefs.Coins.Id, 60);
        Check(shop.Buy(inv, 1, 10, out why) == 2 && inv.Count(ItemDefs.BronzeSword.Id) == 2 && inv.Count(ItemDefs.Coins.Id) == 8, "60 coins buy two 26-coin swords");
        Check(shop.Items[1].Stock == 3, "stock goes down");
        Check(shop.Sell(inv, ItemDefs.BronzeSword.Id, 1, out why) == 1 && inv.Count(ItemDefs.Coins.Id) == 18 && shop.Items[1].Stock == 4, "selling a sword pays 10 (40%)");
        Check(shop.Sell(inv, ItemDefs.Coins.Id, 1, out why) == 0, "coins can't be sold");
        inv.Add(ItemDefs.Logs.Id, 3);
        Check(shop.Sell(inv, ItemDefs.Logs.Id, 5, out why) == 3 && shop.Items.Count == 5 && inv.Count(ItemDefs.Coins.Id) == 21, "the shop buys logs it doesn't stock (1 coin each)");
        for (int i = 0; i < 5; i++) shop.Restock();
        Check(shop.Items.Count == 4 && shop.Items[1].Stock == 5, "restocking brings stock back to normal and clears sold logs");
        inv.Clear();
        inv.SetPouch(0);
        inv.Add(ItemDefs.Logs.Id, 27);
        inv.Add(ItemDefs.Coins.Id, 26);
        Check(shop.Buy(inv, 1, 1, out why) == 1 && inv.IsFull && inv.Count(ItemDefs.Coins.Id) == 0, "buying with pouch coins into the last free slot");

        Console.WriteLine("Money pouch");
        inv.Clear();
        inv.SetPouch(0);
        inv.Add(ItemDefs.Logs.Id, 28);
        Check(inv.Add(ItemDefs.Coins.Id, 500) == 500 && inv.PouchCoins == 500 && inv.CountInSlots(ItemDefs.Coins.Id) == 0, "coins go into the pouch, even with a full backpack");
        Check(inv.HasRoomFor(ItemDefs.Coins.Id), "there's always room for coins");
        inv.RemoveAt(0);
        Check(inv.WithdrawFromPouch(120) == 120 && inv.PouchCoins == 380 && inv[0].ItemId == ItemDefs.Coins.Id && inv[0].Count == 120, "withdraw 120 into the backpack as a stack");
        Check(inv.Count(ItemDefs.Coins.Id) == 500, "Count covers pouch and backpack");
        Check(inv.Remove(ItemDefs.Coins.Id, 400) == 400 && inv.PouchCoins == 0 && inv[0].Count == 100, "spending takes the pouch first, then the backpack");
        Check(inv.AddToPouch(0) == 100 && inv.PouchCoins == 100 && inv[0].IsEmpty, "a stack goes back into the pouch");
        inv.SetPouch(Inventory.MaxPouchCoins - 10);
        Check(inv.Add(ItemDefs.Coins.Id, 25) == 25 && inv.PouchCoins == Inventory.MaxPouchCoins && inv.CountInSlots(ItemDefs.Coins.Id) == 15, "past 2,147,483,647 the rest spills into the backpack");
        var pbank = new Bank();
        inv.SetPouch(1000);
        Check(pbank.DepositAll(inv) > 0 && inv.PouchCoins == 1000 && pbank.Count(ItemDefs.Coins.Id) == 15, "Deposit inventory leaves the pouch alone");
        Check(pbank.DepositPouch(inv) == 1000 && inv.PouchCoins == 0 && pbank.Count(ItemDefs.Coins.Id) == 1015, "Deposit money pouch");
        Check(pbank.Withdraw(inv, pbank.IndexOf(ItemDefs.Coins.Id), 300) == 300 && inv.PouchCoins == 300, "coins withdrawn from the bank land in the pouch");
        inv.Clear();
        inv.SetPouch(0);

        Console.WriteLine("Tool belt");
        var belt = new ToolBelt();
        inv.Clear();
        inv.Add(ItemDefs.Tinderbox.Id);
        inv.Add(ItemDefs.Tinderbox.Id);
        inv.Add(ItemDefs.Logs.Id);
        Check(!ToolBelt.HasTool(belt, new Inventory(), ItemDefs.Tinderbox.Id) && ToolBelt.HasTool(belt, inv, ItemDefs.Tinderbox.Id), "a tinderbox in the backpack counts as having one");
        Check(belt.AddFromInventory(inv, 0, out string bwhy) && belt.Has(ItemDefs.Tinderbox.Id) && inv[0].IsEmpty, "add a tinderbox: it leaves the backpack");
        Check(!belt.AddFromInventory(inv, 1, out bwhy) && bwhy == "That is already on your tool belt." && !inv[1].IsEmpty, "a second one stays in the backpack");
        Check(!belt.AddFromInventory(inv, 2, out bwhy) && !inv[2].IsEmpty, "logs aren't a tool");
        Check(ToolBelt.HasTool(belt, new Inventory(), ItemDefs.Tinderbox.Id), "a tool on the belt counts with an empty backpack");
        inv.Clear();

        Console.WriteLine("Equipment and combat");
        var fresh = new SkillSet();
        Check(fresh.GetLevel(SkillId.Hitpoints) == 10 && fresh.GetLevel(SkillId.Attack) == 1, "Hitpoints starts at 10, the rest at 1");
        Check(CombatRules.CombatLevel(fresh) == 3, "a new player is combat level 3");
        foreach (var id in new[] { SkillId.Attack, SkillId.Strength, SkillId.Defence, SkillId.Hitpoints }) fresh.SetLevel(id, 99);
        Check(CombatRules.CombatLevel(fresh) == 113, "99 in all melee skills is combat level 113 (no Prayer yet)");
        var eq = new Equipment();
        inv.Clear();
        inv.Add(ItemDefs.BronzeSword.Id);
        inv.Add(ItemDefs.WoodenShield.Id);
        inv.Add(ItemDefs.Logs.Id);
        Check(eq.Equip(inv, 0) && eq.Equip(inv, 1) && !eq.Equip(inv, 2), "wield the sword and shield, but not logs");
        Check(eq.IsWearing(EquipSlot.Weapon) && eq.IsWearing(EquipSlot.Shield) && inv.FreeSlots == 27, "both worn, out of the inventory");
        Check(eq.AttackBonus == 4 && eq.StrengthBonus == 5 && eq.DefenceBonus == 5, "bonuses add up");
        inv.Add(ItemDefs.BronzeSword.Id);
        int swordSlot = 0;
        for (int i = 0; i < Inventory.Size; i++) if (!inv[i].IsEmpty && inv[i].ItemId == ItemDefs.BronzeSword.Id) swordSlot = i;
        eq.Equip(inv, swordSlot);
        Check(inv[swordSlot].ItemId == ItemDefs.BronzeSword.Id && eq.IsWearing(EquipSlot.Weapon), "wielding over a weapon swaps it into the same slot");
        Check(eq.Unequip(EquipSlot.Shield, inv) && !eq.IsWearing(EquipSlot.Shield) && inv.Count(ItemDefs.WoodenShield.Id) == 1, "remove the shield");
        inv.Add(ItemDefs.Logs.Id, 28);
        Check(!eq.Unequip(EquipSlot.Weapon, inv) && eq.IsWearing(EquipSlot.Weapon), "can't remove with a full inventory");
        Check(CombatRules.MaxHit(1, 5, CombatStyle.Accurate) == 1 && CombatRules.MaxHit(99, 0, CombatStyle.Aggressive) == 11, "max hits (1 at level 1 with a bronze sword, 11 at 99 unarmed)");
        double hc = CombatRules.HitChance(1, 4, CombatStyle.Accurate, 1, -2);
        Check(hc > 0.5 && hc < 0.7, "level 1 hits a goblin about 6 times in 10 (" + hc.ToString("0.00") + ")");
        var cs = new SkillSet();
        CombatRules.AwardXp(cs, CombatStyle.Aggressive, 3);
        Check(cs.GetXp(SkillId.Strength) == 12 && cs.GetXpTenths(SkillId.Hitpoints) == Experience.XpForLevel(10) * 10 + 40, "3 damage: 12 Strength xp, 4 Hitpoints xp");
        var rng = new Random(1);
        bool allCoins = true;
        for (int i = 0; i < 200; i++)
            if (!NpcDefs.Goblin.Drops.Roll(rng, out int id, out int n) || id != ItemDefs.Coins.Id || n < 1 || n > 30) allCoins = false;
        Check(allCoins, "goblins always drop 1-30 coins");

        Console.WriteLine("Abilities");
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

        Console.WriteLine("Firemaking and fishing");
        var burn = FiremakingRules.For(ItemDefs.Logs.Id);
        Check(burn != null && burn.Xp == 40 && FiremakingRules.For(ItemDefs.Coins.Id) == null, "logs burn for 40 xp; coins don't burn");
        double l1 = FiremakingRules.LightChance(burn, 1);
        Check(l1 > 0.2 && l1 < 0.3 && FiremakingRules.LightChance(burn, 43) == 1.0, "about 1 in 4 to light at level 1, certain at 43");
        var frng = new Random(3);
        bool onlyShrimp = true;
        for (int i = 0; i < 300; i++) { var f = FishingRules.Roll(FishingRules.NetSpot, 14, frng); if (f != null && f != FishingRules.Shrimps) onlyShrimp = false; }
        Check(onlyShrimp, "below level 15 a net spot only gives shrimps");
        int anch = 0, shr = 0, none = 0;
        for (int i = 0; i < 2000; i++) { var f = FishingRules.Roll(FishingRules.NetSpot, 30, frng); if (f == FishingRules.Anchovies) anch++; else if (f == FishingRules.Shrimps) shr++; else none++; }
        Check(anch > 100 && shr > 100, "at 30 both anchovies (" + anch + ") and shrimps (" + shr + ") bite");
        Check(FishingRules.MinLevel(FishingRules.NetSpot) == 1, "net fishing starts at level 1");
        Check(new SkillSet().GetLevel(SkillId.Fishing) == 1 && SkillDefs.Get(SkillId.Firemaking).Name == "Firemaking", "Firemaking and Fishing skills exist");

        Console.WriteLine("Woodcutting");
        double c1 = WoodcuttingRules.SuccessChance(TreeDefs.Basic, 1), c99 = WoodcuttingRules.SuccessChance(TreeDefs.Basic, 99);
        Check(Math.Abs(c1 - 65 / 256.0) < 1e-9 && Math.Abs(c99 - 201 / 256.0) < 1e-9, "chance 25% at level 1, 79% at 99");
        Check(WoodcuttingRules.SuccessChance(TreeDefs.Basic, 999) == 1.0, "certain at high levels");

        Console.WriteLine("Pathfinding");
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

        Console.WriteLine(failures == 0 ? "All passed." : failures + " FAILED.");
        return failures == 0 ? 0 : 1;
    }
}
