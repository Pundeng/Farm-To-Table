using System.Collections.Generic;
using FantasyShapez.Buildings;
using FantasyShapez.Logistics;
using FantasyShapez.Food;
using NUnit.Framework;
using UnityEngine;

namespace FantasyShapez.Tests.EditMode
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

        private static FoodItemData CreateFood()
        {
            return new FoodItemData("apple", FoodItemKind.RawIngredient);
        }

    }
}
