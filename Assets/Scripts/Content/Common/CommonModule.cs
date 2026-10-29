using Epacenur.Core;

namespace Epacenur.Content
{
    /// Things every part of the game uses: coins and the bank booth.
    public sealed class CommonModule : IContentModule
    {
        public const string BankBooth = "bank_booth";

        public void Register(ContentDb db)
        {
            var gold = new Rgb(0.95f, 0.8f, 0.25f);
            var coins = db.Item(ItemIds.Coins, "Coins", "Lovely money!", 1, gold);
            coins.Stackable = true;
            var coinIcon = new Icon();
            for (int k = 0; k < 4; k++)
                coinIcon.Rect(10f + (k % 2) * 6f, 26f - k * 5f, 16f, 5f, new Rgb(0.7f, 0.55f, 0.1f)).Rect(10f + (k % 2) * 6f, 26f - k * 5f, 16f, 3f, gold);
            coins.Icon = coinIcon;
            var coinModel = new Model();
            for (int i = 0; i < 4; i++)
                coinModel.Cylinder((i % 2) * 0.05f, 0.01f + i * 0.022f, (i / 2) * 0.03f, 0.14f, 0.01f, 0.14f, gold);
            coins.GroundModel = coinModel;

            // A bank booth: a counter with the teller's window above it, under a little roof, and a gold coin sign.
            var wood = new Rgb(0.45f, 0.3f, 0.18f);
            var darkWood = new Rgb(0.32f, 0.2f, 0.12f);
            var stone = new Rgb(0.62f, 0.6f, 0.56f);
            var signGold = new Rgb(0.95f, 0.78f, 0.25f);
            var booth = new Model()
                .Cube(0f, 0.5f, 0f, 0.96f, 1f, 0.96f, wood)
                .Cube(0f, 1.03f, -0.06f, 1f, 0.06f, 1.08f, darkWood)
                .Cube(-0.45f, 1.55f, 0f, 0.08f, 1f, 0.9f, stone)
                .Cube(0.45f, 1.55f, 0f, 0.08f, 1f, 0.9f, stone)
                .Cube(0f, 1.55f, 0.42f, 0.9f, 1f, 0.06f, stone)
                .Cube(-0.2f, 1.55f, -0.4f, 0.025f, 1f, 0.025f, signGold)
                .Cube(0f, 1.55f, -0.4f, 0.025f, 1f, 0.025f, signGold)
                .Cube(0.2f, 1.55f, -0.4f, 0.025f, 1f, 0.025f, signGold)
                .Cube(0f, 2.1f, -0.04f, 1.1f, 0.12f, 1.12f, darkWood)
                .Cylinder(0f, 0.55f, -0.49f, 0.4f, 0.02f, 0.4f, signGold).Turn(90f, 0f, 0f);
            db.Add(new StationDef
            {
                Id = BankBooth, Name = "Bank booth", Action = "Bank", Examine = "The bank teller will serve you here.",
                Kind = StationKind.Bank, Model = booth, Label = "Bank", LabelHeight = 2.6f,
            });
        }
    }
}
