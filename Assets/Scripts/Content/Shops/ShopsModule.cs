using Epacenur.Core;

namespace Epacenur.Content
{
    /// Shops. To add one: add a ShopDef here and give a shop keeper its id where the world places it.
    public sealed class ShopsModule : IContentModule
    {
        public const string GeneralStore = "general_store";

        public void Register(ContentDb db)
        {
            db.Add(new ShopDef
            {
                Id = GeneralStore, Name = "General Store", BuysAnything = true, SellRate = 0.4,
                KeeperExamine = "I can buy swords and shields from him.",
                Stock = new[]
                {
                    new ShopStock(FiremakingModule.Tinderbox, 5),
                    new ShopStock(CombatModule.BronzeSword, 5),
                    new ShopStock(CombatModule.WoodenShield, 5),
                    new ShopStock(FishingModule.SmallFishingNet, 5),
                    new ShopStock(WoodcuttingModule.BronzeHatchet, 5),
                    new ShopStock(MiningModule.BronzePickaxe, 5),
                    new ShopStock(SmithingModule.Hammer, 5),
                    new ShopStock(FishingModule.Feathers, 1000),
                    new ShopStock(FishingModule.FlyFishingRod, 5),
                    new ShopStock(FishingModule.LobsterPot, 5),
                    new ShopStock(FishingModule.Harpoon, 5),
                },
            });
        }
    }
}
