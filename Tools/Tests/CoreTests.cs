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
