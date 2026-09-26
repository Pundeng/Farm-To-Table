using FantasyShapez.Buildings;
using FantasyShapez.Food;
using FantasyShapez.Logistics;
using NUnit.Framework;
using UnityEngine;

namespace FantasyShapez.Tests.EditMode
{
    public sealed class CutterTests
    {
        private static readonly FoodItemData Potato =
            new("potato", FoodItemKind.RawIngredient);
        private static readonly FoodItemData CutPotato =
            new("Cut Potato", FoodItemKind.ProcessedFood, 2);

        [Test]
        public void CutterPausesAtCompletionAndEmitsExactlyTwoItems()
        {
            var recipe = new CuttingRecipe(Potato, CutPotato);
            var process = new CutterProcess(new CuttingRecipeCatalog(new[] { recipe }), 1f);
            Assert.That(process.TryAccept(Potato), Is.True);
            Assert.That(process.State, Is.EqualTo(CutterState.WaitingForOutputs));
            Assert.That(process.Advance(0.5f, false), Is.False);
            Assert.That(process.Elapsed, Is.Zero);
            Assert.That(process.Advance(0.5f, true), Is.False);
            Assert.That(process.Advance(2f, false), Is.False);
            Assert.That(process.State, Is.EqualTo(CutterState.Processing));
            Assert.That(process.Elapsed, Is.EqualTo(0.5f));
            Assert.That(process.Advance(0.5f, true), Is.True);
            Assert.That(process.TryTakePair(out FoodItemData a, out FoodItemData b), Is.True);
            Assert.That(a, Is.EqualTo(CutPotato));
            Assert.That(b, Is.EqualTo(CutPotato));
            Assert.That(process.TryTakePair(out _, out _), Is.False);
        }

        [Test]
        public void CutterAcceptsProcessedRecipesAndRejectsAmbiguity()
        {
            var input = new FoodItemData("Vegetable Base", FoodItemKind.ProcessedFood);
            var recipe = new CuttingRecipe(input, CutPotato);
            var process = new CutterProcess(new CuttingRecipeCatalog(new[] { recipe }), 1f);
            Assert.That(process.TryAccept(input), Is.True);
            var ambiguous = new CutterProcess(new CuttingRecipeCatalog(
                new[] { recipe, recipe }), 1f);
            Assert.That(ambiguous.TryAccept(input), Is.False);
        }

        [Test]
        public void SavedWaitingPairResumesWithoutProducingAnotherPair()
        {
            var recipe = new CuttingRecipe(Potato, CutPotato);
            var catalog = new CuttingRecipeCatalog(new[] { recipe });
            var restored = new CutterProcess(catalog, 1f);
            restored.Restore(CutterState.WaitingForOutput, Potato, CutPotato, 1f);
            Assert.That(restored.CanAccept(Potato), Is.False);
            Assert.That(restored.TryTakePair(out FoodItemData a,
                out FoodItemData b), Is.True);
            Assert.That(a, Is.EqualTo(CutPotato));
            Assert.That(b, Is.EqualTo(CutPotato));
            Assert.That(restored.TryTakePair(out _, out _), Is.False);
        }

        [Test]
        public void CutterSnapshotRoundTripsWithoutCreatingExtraOutputs()
        {
            var world = new FactoryWorldData
            {
                buildings = new[]
                {
                    new SavedBuilding
                    {
                        definitionId = nameof(Cutter),
                        cutter = new SavedCutter
                        {
                            state = CutterState.WaitingForOutput,
                            input = SavedFood.From(Potato),
                            output = SavedFood.From(CutPotato),
                            elapsedSeconds = 1f
                        }
                    }
                }
            };
            string json = JsonUtility.ToJson(world);
            FactoryWorldData parsed = JsonUtility.FromJson<FactoryWorldData>(json);
            FactoryWorldSnapshotValidator.RestoreSerializedNulls(parsed);
            Assert.DoesNotThrow(() => FactoryWorldSnapshotValidator.Validate(parsed));
            Assert.That(parsed.buildings[0].cutter.state,
                Is.EqualTo(CutterState.WaitingForOutput));
            Assert.That(parsed.buildings[0].processor, Is.Null);
            Assert.That(parsed.buildings[0].mixer, Is.Null);
        }

        [TestCase(BuildingRotation.Degrees0)]
        [TestCase(BuildingRotation.Degrees90)]
        [TestCase(BuildingRotation.Degrees180)]
        [TestCase(BuildingRotation.Degrees270)]
        public void PortsRotateWithFootprint(BuildingRotation rotation)
        {
            Vector2Int anchor = new(4, 5);
            Vector2Int rear = CutterPortLayout.InputCell(anchor, rotation);
            Vector2Int front = anchor + rotation.RotateCell(Vector2Int.up,
                CutterPortLayout.Footprint);
            Assert.That(rear, Is.EqualTo(anchor + rotation.RotateCell(
                Vector2Int.zero, CutterPortLayout.Footprint)));
            Assert.That(CutterPortLayout.OutputCell(anchor, rotation, 0),
                Is.EqualTo(front + CutterPortLayout.OutputDirection(rotation, 0).ToOffset()));
            Assert.That(CutterPortLayout.OutputCell(anchor, rotation, 1),
                Is.EqualTo(front + CutterPortLayout.OutputDirection(rotation, 1).ToOffset()));
            Assert.That(CutterPortLayout.OutputCell(anchor, rotation, 0),
                Is.Not.EqualTo(CutterPortLayout.OutputCell(anchor, rotation, 1)));
        }

        [Test]
        public void PairedTransportWaitsForBothBeltsAndTransfersTogether()
        {
            var system = new BeltTransportSystem(1f);
            var source = new PairSource(CutPotato);
            system.RegisterOutputPair(source);
            BeltCell left = system.AddBelt(source.OutputACell, GridDirection.West);
            system.Advance(0f);
            Assert.That(source.HasOutputPair, Is.True);
            Assert.That(left.HasItem, Is.False);

            BeltCell right = system.AddBelt(source.OutputBCell, GridDirection.East);
            right.TryAccept(Potato, GridDirection.East);
            system.Advance(0f);
            Assert.That(source.HasOutputPair, Is.True);
            Assert.That(left.HasItem, Is.False);
            system.RemoveBelt(right);
            right = system.AddBelt(source.OutputBCell, GridDirection.East);

            system.Advance(0f);
            Assert.That(source.HasOutputPair, Is.False);
            Assert.That(source.TakeCount, Is.EqualTo(1));
            Assert.That(left.Item.Item, Is.EqualTo(CutPotato));
            Assert.That(right.Item.Item, Is.EqualTo(CutPotato));
        }

        private sealed class PairSource : IItemOutputPairSource
        {
            private readonly ITransportItem food;
            public PairSource(ITransportItem food) => this.food = food;
            public Vector2Int OutputACell => new(-1, 1);
            public Vector2Int OutputBCell => new(1, 1);
            public GridDirection OutputADirection => GridDirection.West;
            public GridDirection OutputBDirection => GridDirection.East;
            public bool HasOutputPair { get; private set; } = true;
            public int TakeCount { get; private set; }
            public ITransportItem PeekOutputA() => HasOutputPair ? food : null;
            public ITransportItem PeekOutputB() => HasOutputPair ? food : null;
            public bool TryTakeOutputPair(out ITransportItem a, out ITransportItem b)
            {
                a = b = HasOutputPair ? food : null;
                if (!HasOutputPair) return false;
                HasOutputPair = false;
                TakeCount++;
                return true;
            }
        }
    }
}
