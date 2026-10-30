using Epacenur.Core;

namespace Epacenur.Content
{
    /// Every content module in the game. To add a skill (or shop, monster...): write a module in its own
    /// folder next to the others and add it here. Order only matters for display: skills appear in the
    /// Skills tab, and tools on the tool belt, in the order they're registered.
    public static class Modules
    {
        public static IContentModule[] All => new IContentModule[]
        {
            new CommonModule(),
            new CombatModule(),
            new PrayerModule(),
            new WoodcuttingModule(),
            new FiremakingModule(),
            new FishingModule(),
            new CookingModule(),
            new MiningModule(),
            new SmithingModule(),
            new FletchingModule(),
            new CraftingModule(),
            new RunecraftingModule(),
            new MagicModule(),
            new AgilityModule(),
            new ThievingModule(),
            new HerbloreModule(),
            new SlayerModule(),
            new HunterModule(),
            new FarmingModule(),
            new ConstructionModule(),
            new ShopsModule(),
        };

        /// Loads every module into ContentDb.Current (once is enough).
        public static ContentDb Load() => ContentDb.Current ?? ContentDb.Load(All);
    }
}
