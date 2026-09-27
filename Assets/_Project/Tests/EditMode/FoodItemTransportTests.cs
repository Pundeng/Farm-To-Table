using CozyFoodFactory.Food;
using CozyFoodFactory.Logistics;
using NUnit.Framework;
using UnityEngine;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class FoodItemTransportTests
    {
        [Test]
        public void FoodIdentity_DistinguishesRawAndProcessedItems()
        {
            var raw = new FoodItemData("apple", FoodItemKind.RawIngredient);
            var processed = new FoodItemData("apple", FoodItemKind.ProcessedFood);

            Assert.That(raw, Is.EqualTo(new FoodItemData("apple", FoodItemKind.RawIngredient)));
            Assert.That(raw, Is.Not.EqualTo(processed));
            Assert.That(raw.Id, Is.EqualTo("apple"));
        }

        [Test]
        public void FoodSource_TransfersThroughBeltsToReceiver()
        {
            var system = new BeltTransportSystem(1f);
            var food = new FoodItemData("apple", FoodItemKind.RawIngredient);
            var source = new FoodSource(food, Vector2Int.zero);
            var receiver = new FoodReceiver(new Vector2Int(2, 0), GridDirection.East);
            BeltCell first = system.AddBelt(Vector2Int.zero, GridDirection.East);
            BeltCell second = system.AddBelt(Vector2Int.right, GridDirection.East);
            system.RegisterOutputSource(source);
            system.RegisterInputReceiver(receiver);

            system.Advance(0f);
            Assert.That(first.Item.Item, Is.SameAs(food));
            Assert.That(source.HasOutput, Is.False);

            system.Advance(1f);
            Assert.That(first.HasItem, Is.False);
            Assert.That(second.Item.Item, Is.SameAs(food));
            Assert.That(second.Item.Progress, Is.Zero);

            system.Advance(1f);
            Assert.That(receiver.Received, Is.SameAs(food));
            Assert.That(second.HasItem, Is.False);
        }

        [Test]
        public void Food_WaitsForOccupiedBeltAndWrongDirection()
        {
            var system = new BeltTransportSystem(1f);
            var food = new FoodItemData("apple", FoodItemKind.RawIngredient);
            BeltCell first = system.AddBelt(Vector2Int.zero, GridDirection.East);
            BeltCell second = system.AddBelt(Vector2Int.right, GridDirection.East);
            var receiver = new FoodReceiver(new Vector2Int(2, 0), GridDirection.North);
            system.RegisterInputReceiver(receiver);
            first.TryAccept(food, GridDirection.East);
            second.TryAccept(new FoodItemData("bread", FoodItemKind.ProcessedFood), GridDirection.East);

            system.Advance(1f);
            Assert.That(first.Item.Item, Is.SameAs(food));
            Assert.That(first.Item.Progress, Is.EqualTo(1f));
            Assert.That(receiver.Received, Is.Null);

            system.Advance(1f);
            Assert.That(second.HasItem, Is.True);
            Assert.That(receiver.Received, Is.Null);
        }

        [Test]
        public void FoodSource_KeepsOutputWhileReceivingBeltIsOccupied()
        {
            var system = new BeltTransportSystem(1f);
            var food = new FoodItemData("apple", FoodItemKind.RawIngredient);
            var source = new FoodSource(food, Vector2Int.zero);
            BeltCell belt = system.AddBelt(Vector2Int.zero, GridDirection.East);
            belt.TryAccept(new FoodItemData("bread", FoodItemKind.ProcessedFood),
                GridDirection.East);
            system.RegisterOutputSource(source);

            system.Advance(0f);

            Assert.That(source.PeekOutput(), Is.SameAs(food));
            Assert.That(belt.Item.Item, Is.Not.SameAs(food));
        }

        [Test]
        public void FoodReceiver_ReservesOneInputPerStep()
        {
            var system = new BeltTransportSystem(1f);
            var receiver = new FoodReceiver(Vector2Int.zero, GridDirection.East);
            BeltCell west = system.AddBelt(Vector2Int.left, GridDirection.East);
            BeltCell east = system.AddBelt(Vector2Int.right, GridDirection.West);
            var food = new FoodItemData("apple", FoodItemKind.RawIngredient);
            west.TryAccept(food, GridDirection.East);
            east.TryAccept(new FoodItemData("bread", FoodItemKind.ProcessedFood), GridDirection.West);
            receiver.AcceptAnyDirection = true;
            system.RegisterInputReceiver(receiver);

            system.Advance(1f);

            Assert.That(receiver.Received, Is.SameAs(food));
            Assert.That(west.HasItem, Is.False);
            Assert.That(east.HasItem, Is.True);
        }

        [Test]
        public void RemovingBelt_DiscardsFoodAndFreesCell()
        {
            var system = new BeltTransportSystem(1f);
            BeltCell belt = system.AddBelt(Vector2Int.zero, GridDirection.East);
            belt.TryAccept(new FoodItemData("apple", FoodItemKind.RawIngredient), GridDirection.East);

            Assert.That(system.RemoveBelt(belt), Is.True);
            Assert.That(belt.HasItem, Is.False);
            Assert.That(system.AddBelt(Vector2Int.zero, GridDirection.North), Is.Not.Null);
        }

        private sealed class FoodSource : IItemOutputSource
        {
            private ITransportItem item;

            public FoodSource(FoodItemData item, Vector2Int outputCell)
            {
                this.item = item;
                OutputCell = outputCell;
            }

            public Vector2Int OutputCell { get; }
            public GridDirection OutputDirection => GridDirection.East;
            public bool HasOutput => item != null;
            public ITransportItem PeekOutput() => item;

            public bool TryTakeOutput(out ITransportItem taken)
            {
                taken = item;
                item = null;
                return taken != null;
            }
        }

        private sealed class FoodReceiver : IItemInputReceiver
        {
            private readonly GridDirection requiredDirection;

            public FoodReceiver(Vector2Int inputCell, GridDirection requiredDirection)
            {
                InputCell = inputCell;
                this.requiredDirection = requiredDirection;
            }

            public Vector2Int InputCell { get; }
            public bool AllowsConcurrentInput => false;
            public bool AcceptAnyDirection { get; set; }
            public ITransportItem Received { get; private set; }

            public bool CanAcceptItem(ITransportItem item, GridDirection direction)
            {
                return Received == null && item is FoodItemData &&
                    (AcceptAnyDirection || direction == requiredDirection);
            }

            public bool TryAcceptItem(ITransportItem item, GridDirection direction)
            {
                if (!CanAcceptItem(item, direction))
                {
                    return false;
                }

                Received = item;
                return true;
            }
        }
    }
}
