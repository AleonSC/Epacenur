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
        Check(shop.Items.Count == 3 && shop.Items[0].Def == ItemDefs.Tinderbox && shop.Items[1].Def == ItemDefs.BronzeSword && shop.Items[2].Def == ItemDefs.WoodenShield, "stocks a tinderbox, a sword and a shield");
        inv.Clear();
        string why;
        Check(shop.Buy(inv, 1, 1, out why) == 0 && why == "You don't have enough coins.", "can't buy without coins");
        inv.Add(ItemDefs.Coins.Id, 60);
        Check(shop.Buy(inv, 1, 10, out why) == 2 && inv.Count(ItemDefs.BronzeSword.Id) == 2 && inv.Count(ItemDefs.Coins.Id) == 8, "60 coins buy two 26-coin swords");
        Check(shop.Items[1].Stock == 3, "stock goes down");
        Check(shop.Sell(inv, ItemDefs.BronzeSword.Id, 1, out why) == 1 && inv.Count(ItemDefs.Coins.Id) == 18 && shop.Items[1].Stock == 4, "selling a sword pays 10 (40%)");
        Check(shop.Sell(inv, ItemDefs.Coins.Id, 1, out why) == 0, "coins can't be sold");
        inv.Add(ItemDefs.Logs.Id, 3);
        Check(shop.Sell(inv, ItemDefs.Logs.Id, 5, out why) == 3 && shop.Items.Count == 4 && inv.Count(ItemDefs.Coins.Id) == 21, "the shop buys logs it doesn't stock (1 coin each)");
        for (int i = 0; i < 5; i++) shop.Restock();
        Check(shop.Items.Count == 3 && shop.Items[1].Stock == 5, "restocking brings stock back to normal and clears sold logs");
        inv.Clear();
        inv.Add(ItemDefs.Logs.Id, 27);
        inv.Add(ItemDefs.Coins.Id, 26);
        Check(shop.Buy(inv, 1, 1, out why) == 1 && inv.IsFull && inv.Count(ItemDefs.Coins.Id) == 0, "spending your last coins frees their slot for the item");

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
