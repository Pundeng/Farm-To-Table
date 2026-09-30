using System.Collections.Generic;
using System.Linq;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Logistics;
using CozyFoodFactory.Food;
using NUnit.Framework;
using UnityEngine;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class BeltTransportTests
    {
        [TestCase(BuildingRotation.Degrees0, GridDirection.North, 0, 1)]
        [TestCase(BuildingRotation.Degrees90, GridDirection.East, 1, 0)]
        [TestCase(BuildingRotation.Degrees180, GridDirection.South, 0, -1)]
        [TestCase(BuildingRotation.Degrees270, GridDirection.West, -1, 0)]
        public void BuildingRotation_MapsToExpectedOutputCell(
            BuildingRotation rotation,
            GridDirection expectedDirection,
            int outputX,
            int outputY)
        {
            GridDirection direction = rotation.ToGridDirection();
            var belt = new BeltCell(Vector2Int.zero, direction);

            Assert.That(direction, Is.EqualTo(expectedDirection));
            Assert.That(belt.OutputCell, Is.EqualTo(new Vector2Int(outputX, outputY)));
        }

        [Test]
        public void Belt_AcceptsOnlyOneFoodAtATime()
        {
            var belt = new BeltCell(Vector2Int.zero, GridDirection.East);

            bool acceptedFirst = belt.TryAccept(CreateFood(), GridDirection.East);
            bool acceptedSecond = belt.TryAccept(CreateFood(), GridDirection.East);

            Assert.That(acceptedFirst, Is.True);
            Assert.That(acceptedSecond, Is.False);
            Assert.That(belt.HasItem, Is.True);
        }

        [Test]
        public void ReadyFood_TransfersToAvailableNextBeltWithoutChangingFoodItemData()
        {
            var system = new BeltTransportSystem(1f);
            BeltCell first = system.AddBelt(Vector2Int.zero, GridDirection.East);
            BeltCell second = system.AddBelt(Vector2Int.right, GridDirection.North);
            FoodItemData food = CreateFood();
            first.TryAccept(food, GridDirection.East);

            system.Advance(1f);

            Assert.That(first.HasItem, Is.False);
            Assert.That(second.HasItem, Is.True);
            Assert.That(second.Item.Item, Is.SameAs(food));
            Assert.That(second.Item.Progress, Is.Zero);
        }

        [Test]
        public void ReadyFood_WaitsWhenNextBeltIsOccupied()
        {
            var system = new BeltTransportSystem(1f);
            BeltCell first = system.AddBelt(Vector2Int.zero, GridDirection.East);
            BeltCell second = system.AddBelt(Vector2Int.right, GridDirection.East);
            FoodItemData waitingFood = CreateFood();
            first.TryAccept(waitingFood, GridDirection.East);
            second.TryAccept(CreateFood(), GridDirection.East);

            system.Advance(1f);

            Assert.That(first.Item.Item, Is.SameAs(waitingFood));
            Assert.That(first.Item.Progress, Is.EqualTo(1f));
        }

        [Test]
        public void Food_StopsAtOutputEndWhenNoNextBeltExists()
        {
            var system = new BeltTransportSystem(1f);
            BeltCell belt = system.AddBelt(Vector2Int.zero, GridDirection.East);
            FoodItemData food = CreateFood();
            belt.TryAccept(food, GridDirection.East);

            system.Advance(2f);

            Assert.That(belt.Item.Item, Is.SameAs(food));
            Assert.That(belt.Item.Progress, Is.EqualTo(1f));
        }

        [TestCase(GridDirection.North, 0, 1, GridDirection.South)]
        [TestCase(GridDirection.East, 1, 0, GridDirection.West)]
        [TestCase(GridDirection.South, 0, -1, GridDirection.North)]
        [TestCase(GridDirection.West, -1, 0, GridDirection.East)]
        public void OppositeFacingBelts_BlockFoodWithoutBounceBack(
            GridDirection sourceDirection,
            int destinationX,
            int destinationY,
            GridDirection destinationDirection)
        {
            var system = new BeltTransportSystem(1f);
            BeltCell source = system.AddBelt(Vector2Int.zero, sourceDirection);
            BeltCell destination = system.AddBelt(
                new Vector2Int(destinationX, destinationY),
                destinationDirection);
            FoodItemData food = CreateFood();
            source.TryAccept(food, sourceDirection);

            system.Advance(1f);
            system.Advance(1f);
            system.Advance(1f);

            Assert.That(source.Item.Item, Is.SameAs(food));
            Assert.That(source.Item.Progress, Is.EqualTo(1f));
            Assert.That(destination.HasItem, Is.False);
        }

        [Test]
        public void OccupiedOppositeFacingBelts_PreserveBothFoodsInPlace()
        {
            var system = new BeltTransportSystem(1f);
            BeltCell left = system.AddBelt(Vector2Int.zero, GridDirection.East);
            BeltCell right = system.AddBelt(Vector2Int.right, GridDirection.West);
            FoodItemData leftFood = CreateFood();
            FoodItemData rightFood = CreateFood();
            left.TryAccept(leftFood, GridDirection.East);
            right.TryAccept(rightFood, GridDirection.West);

            system.Advance(1f);
            system.Advance(1f);

            Assert.That(left.Item.Item, Is.SameAs(leftFood));
            Assert.That(right.Item.Item, Is.SameAs(rightFood));
            Assert.That(left.Item.Progress, Is.EqualTo(1f));
            Assert.That(right.Item.Progress, Is.EqualTo(1f));
        }

        [Test]
        public void ThreeBeltChain_MovesFoodToFinalBelt()
        {
            var system = new BeltTransportSystem(1f);
            BeltCell first = system.AddBelt(new Vector2Int(0, 0), GridDirection.East);
            BeltCell second = system.AddBelt(new Vector2Int(1, 0), GridDirection.East);
            BeltCell third = system.AddBelt(new Vector2Int(2, 0), GridDirection.North);
            FoodItemData food = CreateFood();
            first.TryAccept(food, GridDirection.East);

            system.Advance(1f);
            system.Advance(1f);

            Assert.That(first.HasItem, Is.False);
            Assert.That(second.HasItem, Is.False);
            Assert.That(third.Item.Item, Is.SameAs(food));
        }

        [Test]
        public void DragPlannedTurn_ProducesConnectedTransportDirections()
        {
            var planner = new BeltDragPlacementPlanner();
            var plannedSteps = new List<BeltPlacementStep>(planner.Continue(new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(1, 1)
            }));
            planner.TryComplete(BuildingRotation.Degrees180, out BeltPlacementStep finalStep);
            plannedSteps.Add(finalStep);
            var system = new BeltTransportSystem(1f);
            var belts = new List<BeltCell>();
            foreach (BeltPlacementStep step in plannedSteps)
            {
                belts.Add(system.AddBelt(step.Cell, step.Rotation.ToGridDirection()));
            }

            FoodItemData food = CreateFood();
            belts[0].TryAccept(food, GridDirection.East);
            system.Advance(1f);
            system.Advance(1f);

            Assert.That(belts[0].OutputCell, Is.EqualTo(belts[1].Cell));
            Assert.That(belts[1].OutputCell, Is.EqualTo(belts[2].Cell));
            Assert.That(belts[2].Item.Item, Is.SameAs(food));
        }

        [Test]
        public void EmptyBelt_CanBeRemoved()
        {
            var system = new BeltTransportSystem(1f);
            BeltCell belt = system.AddBelt(Vector2Int.zero, GridDirection.East);

            bool removed = system.RemoveBelt(belt);

            Assert.That(removed, Is.True);
            Assert.That(system.TryGetBelt(Vector2Int.zero, out _), Is.False);
        }

        [Test]
        public void OccupiedBelt_CanBeRemovedAndDiscardsFood()
        {
            var system = new BeltTransportSystem(1f);
            BeltCell belt = system.AddBelt(Vector2Int.zero, GridDirection.East);
            belt.TryAccept(CreateFood(), GridDirection.East);

            bool removed = system.RemoveBelt(belt);

            Assert.That(removed, Is.True);
            Assert.That(belt.HasItem, Is.False);
        }

        [Test]
        public void RemovedBeltCell_CanBeReused()
        {
            var system = new BeltTransportSystem(1f);
            BeltCell removedBelt = system.AddBelt(Vector2Int.zero, GridDirection.East);
            removedBelt.TryAccept(CreateFood(), GridDirection.East);
            system.RemoveBelt(removedBelt);

            BeltCell replacement = system.AddBelt(Vector2Int.zero, GridDirection.North);

            Assert.That(replacement, Is.Not.SameAs(removedBelt));
            Assert.That(replacement.Cell, Is.EqualTo(Vector2Int.zero));
        }

        [Test]
        public void RemovingDownstreamBelt_LeavesUpstreamFoodBlockedSafely()
        {
            var system = new BeltTransportSystem(1f);
            BeltCell upstream = system.AddBelt(Vector2Int.zero, GridDirection.East);
            BeltCell downstream = system.AddBelt(Vector2Int.right, GridDirection.East);
            FoodItemData food = CreateFood();
            upstream.TryAccept(food, GridDirection.East);
            system.RemoveBelt(downstream);

            Assert.DoesNotThrow(() => system.Advance(2f));
            Assert.That(upstream.Item.Item, Is.SameAs(food));
            Assert.That(upstream.Item.Progress, Is.EqualTo(1f));
        }

        [Test]
        public void Split_AlternatesAvailableOutputs_AndUsesUnblockedFallback()
        {
            var system = new BeltTransportSystem(1f);
            BeltCell junction = system.AddBelt(Vector2Int.zero, GridDirection.North);
            junction.SetOutputs(BeltCell.Bit(GridDirection.North) |
                BeltCell.Bit(GridDirection.East));
            var north = new TestReceiver(Vector2Int.up);
            var east = new TestReceiver(Vector2Int.right);
            system.RegisterInputReceiver(north);
            system.RegisterInputReceiver(east);

            for (int index = 0; index < 6; index++)
            {
                FoodItemData food = new($"food-{index}", FoodItemKind.RawIngredient);
                Assert.That(junction.TryAccept(food, GridDirection.West), Is.True);
                system.Advance(1f);
                Assert.That(junction.HasItem, Is.False);
            }
            Assert.That(north.Items.Count, Is.EqualTo(3));
            Assert.That(east.Items.Count, Is.EqualTo(3));

            north.Blocked = true;
            FoodItemData fallback = CreateFood();
            junction.TryAccept(fallback, GridDirection.West);
            system.Advance(1f);
            Assert.That(east.Items[^1], Is.SameAs(fallback));

            east.Blocked = true;
            FoodItemData waiting = CreateFood();
            junction.TryAccept(waiting, GridDirection.West);
            system.Advance(1f);
            Assert.That(junction.Item.Item, Is.SameAs(waiting));
            east.Blocked = false;
            system.Advance(1f);
            Assert.That(junction.HasItem, Is.False);
            Assert.That(east.Items[^1], Is.SameAs(waiting));
        }

        [Test]
        public void Merge_ChoosesOneInputAndAlternatesAfterDestinationReopens()
        {
            var system = new BeltTransportSystem(1f);
            BeltCell west = system.AddBelt(Vector2Int.left, GridDirection.East);
            BeltCell south = system.AddBelt(Vector2Int.down, GridDirection.North);
            BeltCell merge = system.AddBelt(Vector2Int.zero, GridDirection.East);
            var sink = new TestReceiver(Vector2Int.right);
            system.RegisterInputReceiver(sink);
            FoodItemData first = new("west", FoodItemKind.RawIngredient);
            FoodItemData second = new("south", FoodItemKind.RawIngredient);
            west.TryAccept(first, GridDirection.East);
            south.TryAccept(second, GridDirection.North);

            system.Advance(1f);
            Assert.That(merge.HasItem, Is.True);
            Assert.That(west.HasItem ^ south.HasItem, Is.True);
            FoodItemData firstWinner = (FoodItemData)merge.Item.Item;
            system.Advance(1f);
            system.Advance(1f);
            Assert.That(merge.Item.Item, Is.Not.SameAs(firstWinner));
            Assert.That(sink.Items, Does.Contain(firstWinner));
            Assert.That(west.HasItem || south.HasItem, Is.False);
        }

        [Test]
        public void Merge_WinnerDoesNotDependOnBeltRegistrationOrder()
        {
            static string Winner(bool reverse)
            {
                var system = new BeltTransportSystem(1f);
                BeltCell west;
                BeltCell south;
                if (reverse)
                {
                    south = system.AddBelt(Vector2Int.down, GridDirection.North);
                    west = system.AddBelt(Vector2Int.left, GridDirection.East);
                }
                else
                {
                    west = system.AddBelt(Vector2Int.left, GridDirection.East);
                    south = system.AddBelt(Vector2Int.down, GridDirection.North);
                }
                BeltCell merge = system.AddBelt(Vector2Int.zero, GridDirection.East);
                west.TryAccept(new FoodItemData("west", FoodItemKind.RawIngredient),
                    GridDirection.East);
                south.TryAccept(new FoodItemData("south", FoodItemKind.RawIngredient),
                    GridDirection.North);
                system.Advance(1f);
                Assert.That(west.HasItem ^ south.HasItem, Is.True);
                return ((FoodItemData)merge.Item.Item).Id;
            }

            Assert.That(Winner(false), Is.EqualTo(Winner(true)));
        }

        [Test]
        public void JunctionMask_RoundTripsThroughWorldJson()
        {
            var world = new FactoryWorldData
            {
                buildings = new[] { new SavedBuilding
                {
                    definitionId = nameof(Belt), rotation = BuildingRotation.Degrees90,
                    belt = new SavedBelt
                    {
                        outputMask = BeltCell.Bit(GridDirection.East) |
                            BeltCell.Bit(GridDirection.North),
                        nextOutputIndex = 2,
                        nextInputIndex = 3
                    }
                } }
            };
            string json = JsonUtility.ToJson(world);
            FactoryWorldData loaded = JsonUtility.FromJson<FactoryWorldData>(json);
            FactoryWorldSnapshotValidator.RestoreSerializedNulls(loaded);
            Assert.DoesNotThrow(() => FactoryWorldSnapshotValidator.Validate(loaded));
            Assert.That(loaded.buildings[0].belt.outputMask,
                Is.EqualTo(world.buildings[0].belt.outputMask));
            Assert.That(loaded.buildings[0].belt.nextOutputIndex, Is.EqualTo(2));
            Assert.That(loaded.buildings[0].belt.nextInputIndex, Is.EqualTo(3));
        }

        [Test]
        public void ExplicitBranch_AddsOutputWithoutReversingExistingFlow()
        {
            int straight = BeltCell.Bit(GridDirection.East);
            int adjacentOnly = BeltConnectionPlanner.ExtendExisting(straight,
                GridDirection.East, GridDirection.North, false);
            int draggedBranch = BeltConnectionPlanner.ExtendExisting(straight,
                GridDirection.East, GridDirection.North, true);
            Assert.That(adjacentOnly, Is.EqualTo(straight));
            Assert.That(draggedBranch, Is.EqualTo(straight |
                BeltCell.Bit(GridDirection.North)));
            Assert.That((draggedBranch & BeltCell.Bit(GridDirection.East)) != 0,
                Is.True);
        }

        [Test]
        public void SinglePlacement_PrefersUniqueStraightInputButKeepsAmbiguousFallback()
        {
            Assert.That(BeltConnectionPlanner.PreferStraight(
                BeltCell.Bit(GridDirection.East), GridDirection.North),
                Is.EqualTo(GridDirection.East));
            Assert.That(BeltConnectionPlanner.PreferStraight(
                BeltCell.Bit(GridDirection.East) |
                    BeltCell.Bit(GridDirection.North), GridDirection.South),
                Is.EqualTo(GridDirection.South));
            Assert.That(BeltConnectionPlanner.PreferStraight(0, GridDirection.West),
                Is.EqualTo(GridDirection.West));
        }

        [Test]
        public void Merge_SustainedTrafficDoesNotStarveEitherInput()
        {
            var system = new BeltTransportSystem(1f);
            BeltCell west = system.AddBelt(Vector2Int.left, GridDirection.East);
            BeltCell south = system.AddBelt(Vector2Int.down, GridDirection.North);
            system.AddBelt(Vector2Int.zero, GridDirection.East);
            var sink = new TestReceiver(Vector2Int.right);
            system.RegisterInputReceiver(sink);
            for (int tick = 0; tick < 60; tick++)
            {
                if (!west.HasItem)
                    west.TryAccept(new FoodItemData("west", FoodItemKind.RawIngredient),
                        GridDirection.East);
                if (!south.HasItem)
                    south.TryAccept(new FoodItemData("south", FoodItemKind.RawIngredient),
                        GridDirection.North);
                system.Advance(1f);
            }
            int westCount = sink.Items.Count(item => ((FoodItemData)item).Id == "west");
            int southCount = sink.Items.Count(item => ((FoodItemData)item).Id == "south");
            Assert.That(westCount, Is.GreaterThan(5));
            Assert.That(southCount, Is.GreaterThan(5));
            Assert.That(System.Math.Abs(westCount - southCount), Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void Merge_BlockedOutputKeepsBothInputsUntilItReopens()
        {
            var system = new BeltTransportSystem(1f);
            BeltCell west = system.AddBelt(Vector2Int.left, GridDirection.East);
            BeltCell south = system.AddBelt(Vector2Int.down, GridDirection.North);
            BeltCell merge = system.AddBelt(Vector2Int.zero, GridDirection.East);
            var sink = new TestReceiver(Vector2Int.right) { Blocked = true };
            system.RegisterInputReceiver(sink);
            merge.TryAccept(new FoodItemData("held", FoodItemKind.RawIngredient),
                GridDirection.East);
            west.TryAccept(new FoodItemData("west", FoodItemKind.RawIngredient),
                GridDirection.East);
            south.TryAccept(new FoodItemData("south", FoodItemKind.RawIngredient),
                GridDirection.North);
            system.Advance(2f);
            Assert.That(merge.HasItem && west.HasItem && south.HasItem, Is.True);
            sink.Blocked = false;
            for (int tick = 0; tick < 5; tick++) system.Advance(1f);
            Assert.That(sink.Items.Count, Is.EqualTo(3));
            Assert.That(merge.HasItem || west.HasItem || south.HasItem, Is.False);
        }

        private sealed class TestReceiver : IItemInputReceiver
        {
            public TestReceiver(Vector2Int cell) => InputCell = cell;
            public Vector2Int InputCell { get; }
            public bool AllowsConcurrentInput => false;
            public bool Blocked { get; set; }
            public List<ITransportItem> Items { get; } = new();
            public bool CanAcceptItem(ITransportItem item,
                GridDirection incomingDirection) => !Blocked;
            public bool TryAcceptItem(ITransportItem item,
                GridDirection incomingDirection)
            {
                if (Blocked) return false;
                Items.Add(item);
                return true;
            }
        }

        private static FoodItemData CreateFood()
        {
            return new FoodItemData("apple", FoodItemKind.RawIngredient);
        }

    }
}
