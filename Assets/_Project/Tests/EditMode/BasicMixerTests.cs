using CozyFoodFactory.Buildings;
using CozyFoodFactory.Food;
using CozyFoodFactory.Logistics;
using NUnit.Framework;
using UnityEngine;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class BasicMixerTests
    {
        private static readonly FoodItemData Tomato =
            new("tomato", FoodItemKind.RawIngredient);
        private static readonly FoodItemData Onion =
            new("onion", FoodItemKind.RawIngredient);
        private static readonly FoodItemData VegetableBase =
            new("Vegetable Base", FoodItemKind.ProcessedFood);

        [TestCase(BuildingRotation.Degrees0, 0, 0, 0, 1, 2, 1,
            GridDirection.East, GridDirection.East)]
        [TestCase(BuildingRotation.Degrees90, 0, 1, 1, 1, 1, -1,
            GridDirection.South, GridDirection.South)]
        [TestCase(BuildingRotation.Degrees180, 1, 1, 1, 0, -1, 0,
            GridDirection.West, GridDirection.West)]
        [TestCase(BuildingRotation.Degrees270, 1, 0, 0, 0, 0, 2,
            GridDirection.North, GridDirection.North)]
        public void PortsRotateWithFootprint(BuildingRotation rotation,
            int ax, int ay, int bx, int by, int ox, int oy,
            GridDirection incoming, GridDirection outgoing)
        {
            Vector2Int anchor = new(4, 5);
            Assert.That(BasicMixerPortLayout.GetInputCell(anchor, rotation, 0),
                Is.EqualTo(anchor + new Vector2Int(ax, ay)));
            Assert.That(BasicMixerPortLayout.GetInputCell(anchor, rotation, 1),
                Is.EqualTo(anchor + new Vector2Int(bx, by)));
            Assert.That(BasicMixerPortLayout.GetOutputOutsideCell(anchor, rotation),
                Is.EqualTo(anchor + new Vector2Int(ox, oy)));
            Assert.That(BasicMixerPortLayout.GetIncomingDirection(rotation),
                Is.EqualTo(incoming));
            Assert.That(BasicMixerPortLayout.GetOutputFacing(rotation),
                Is.EqualTo(outgoing));
        }

        [Test]
        public void UniqueRecipeIsUnorderedAndInvalidOrAmbiguousPairsStayUnconsumed()
        {
            var recipe = new MixingRecipe(Tomato, Onion, VegetableBase);
            var process = new BasicMixerProcess(new MixingRecipeCatalog(new[] { recipe }));
            Assert.That(process.TryAccept(0,
                new FoodItemData("apple", FoodItemKind.RawIngredient)), Is.False);
            Assert.That(process.TryAccept(1, Onion), Is.True);
            Assert.That(process.TryAccept(0,
                new FoodItemData("apple", FoodItemKind.RawIngredient)), Is.False);
            Assert.That(process.InputB, Is.EqualTo(Onion));
            Assert.That(process.TryAccept(0, Tomato), Is.True);
            Assert.That(process.HasOutput, Is.True);
            Assert.That(process.PeekOutput(), Is.EqualTo(VegetableBase));
            Assert.That(process.TryAccept(0, Tomato), Is.False);
            Assert.That(process.TryTakeOutput(out FoodItemData output), Is.True);
            Assert.That(output, Is.EqualTo(VegetableBase));

            var ambiguous = new BasicMixerProcess(new MixingRecipeCatalog(
                new[] { recipe, recipe }));
            Assert.That(ambiguous.TryAccept(0, Tomato), Is.True);
            Assert.That(ambiguous.TryAccept(1, Onion), Is.False);
            Assert.That(ambiguous.InputA, Is.EqualTo(Tomato));
            Assert.That(ambiguous.InputB, Is.Null);
            Assert.That(ambiguous.HasOutput, Is.False);
        }

        [Test]
        public void TwoPortsKeepOwnershipAndBlockedOutputReachesMarketAfterBeltIsAdded()
        {
            var coordinatorObject = new GameObject("Mixer coordinator");
            var mixerObject = new GameObject("Mixer");
            try
            {
                var coordinator = coordinatorObject.AddComponent<BeltTransportCoordinator>();
                var mixer = mixerObject.AddComponent<BasicMixer>();
                Vector2Int anchor = Vector2Int.zero;
                mixer.Initialize(new BuildingPlacement("BasicMixer", anchor,
                        BasicMixerPortLayout.Footprint, BuildingRotation.Degrees0),
                    coordinator, new MixingRecipeCatalog(new[]
                    {
                        new MixingRecipe(Tomato, Onion, VegetableBase),
                        new MixingRecipe(new FoodItemData("basil", FoodItemKind.RawIngredient),
                            Onion, new FoodItemData("Pesto", FoodItemKind.ProcessedFood))
                    }));
                Assert.That(mixer.InputAReceiver.InputCell, Is.EqualTo(Vector2Int.zero));
                Assert.That(mixer.InputBReceiver.InputCell, Is.EqualTo(Vector2Int.up));
                Assert.That(mixer.InputAReceiver.CanAcceptItem(Tomato,
                    GridDirection.North), Is.False);
                Assert.That(mixer.InputBReceiver.CanAcceptItem(Onion,
                    GridDirection.East), Is.True);

                var system = new BeltTransportSystem(1f);
                system.RegisterInputReceiver(mixer.InputAReceiver);
                system.RegisterInputReceiver(mixer.InputBReceiver);
                system.RegisterOutputSource(mixer);
                BeltCell beltA = system.AddBelt(new Vector2Int(-1, 0), GridDirection.East);
                BeltCell beltB = system.AddBelt(new Vector2Int(-1, 1), GridDirection.East);
                beltA.TryAccept(Tomato, GridDirection.East);
                beltB.TryAccept(Onion, GridDirection.East);

                system.Advance(1f);
                Assert.That(mixer.HasOutput, Is.False);
                Assert.That((mixer.SlotA != null) ^ (mixer.SlotB != null), Is.True);
                Assert.That(beltA.HasItem ^ beltB.HasItem, Is.True);
                system.Advance(1f);
                Assert.That(mixer.HasOutput, Is.True);
                Assert.That(beltA.HasItem || beltB.HasItem, Is.False);
                Assert.That(mixer.PeekOutput(), Is.EqualTo(VegetableBase));

                BeltCell outputBelt = system.AddBelt(mixer.OutputCell, GridDirection.East);
                var market = new MarketReceiver(mixer.OutputCell + Vector2Int.right - Vector2Int.up,
                    new MarketInventory());
                foreach (IItemInputReceiver input in market.InputReceivers)
                    system.RegisterInputReceiver(input);
                system.Advance(0f);
                Assert.That(mixer.HasOutput, Is.False);
                Assert.That(outputBelt.HasItem, Is.True);
                system.Advance(1f);
                Assert.That(market.Inventory.GetDeliveredCount(VegetableBase), Is.EqualTo(1));

                // Both foods can start some recipe, but they cannot form one together.
                beltA.TryAccept(Tomato, GridDirection.East);
                beltB.TryAccept(new FoodItemData("basil", FoodItemKind.RawIngredient),
                    GridDirection.East);
                system.Advance(1f);
                Assert.That(beltA.HasItem ^ beltB.HasItem, Is.True);
                system.Advance(1f);
                Assert.That(beltA.HasItem ^ beltB.HasItem, Is.True);
                Assert.That(mixer.HasOutput, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(mixerObject);
                Object.DestroyImmediate(coordinatorObject);
            }
        }
    }
}
