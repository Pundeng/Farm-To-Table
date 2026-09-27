using CozyFoodFactory.Food;
using NUnit.Framework;
using UnityEngine;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class CookingPropertyNetworkTests
    {
        [TestCase(CookingProperty.Heat)]
        [TestCase(CookingProperty.Moisture)]
        [TestCase(CookingProperty.Time)]
        [TestCase(CookingProperty.Air)]
        public void EachProperty_ConnectsThroughCollectorAndPipe(CookingProperty property)
        {
            var network = new CookingPropertyNetwork();
            Assert.That(network.TryAddSource(Vector2Int.zero, property, 2), Is.True);
            Assert.That(network.TryAddPipe(Vector2Int.right), Is.False,
                "A collector is required at a source.");
            Assert.That(network.TryAddCollector(Vector2Int.right, Vector2Int.zero), Is.True);
            Assert.That(network.TryAddPipe(new Vector2Int(2, 0)), Is.True);
            Assert.That(network.TryAddDemand(new Vector2Int(3, 0)), Is.True);
            Assert.That(network.IsSupplied(new Vector2Int(3, 0)), Is.True);
            Assert.That(network.IsConnectedToSource(new Vector2Int(2, 0)), Is.True);
            Assert.That(network.TryGetConnection(new Vector2Int(2, 0),
                out PropertyConnection pipe), Is.True);
            Assert.That(pipe.Property, Is.EqualTo(property));
            Assert.That(pipe.SourceCell, Is.EqualTo(Vector2Int.zero));

            Assert.That(network.Remove(new Vector2Int(2, 0)), Is.True);
            Assert.That(network.IsSupplied(new Vector2Int(3, 0)), Is.False);
            Assert.That(network.IsConnectedToSource(new Vector2Int(3, 0)), Is.False);
            Assert.That(network.TryGetStatus(Vector2Int.zero,
                out PropertySupplyStatus status), Is.True);
            Assert.That(status.ConnectedDemand, Is.Zero);
        }

        [Test]
        public void DemandBeyondCapacity_IsReportedWithoutAllocatingConsumers()
        {
            var network = new CookingPropertyNetwork();
            Assert.That(network.TryAddSource(Vector2Int.zero, CookingProperty.Heat, 2), Is.True);
            Assert.That(network.TryAddCollector(Vector2Int.right, Vector2Int.zero), Is.True);
            Assert.That(network.TryAddPipe(new Vector2Int(2, 0)), Is.True);
            Assert.That(network.TryAddDemand(new Vector2Int(1, 1)), Is.True);
            Assert.That(network.TryAddDemand(new Vector2Int(2, 1)), Is.True);
            Assert.That(network.TryGetStatus(Vector2Int.zero,
                out PropertySupplyStatus atCapacity), Is.True);
            Assert.That(atCapacity.ConnectedDemand, Is.EqualTo(2));
            Assert.That(atCapacity.AvailableCapacity, Is.Zero);
            Assert.That(network.IsSupplied(new Vector2Int(1, 1)), Is.True);

            Assert.That(network.TryAddDemand(new Vector2Int(3, 0)), Is.True);
            Assert.That(network.TryGetStatus(Vector2Int.zero,
                out PropertySupplyStatus overloaded), Is.True);
            Assert.That(overloaded.ConnectedDemand, Is.EqualTo(3));
            Assert.That(overloaded.IsWithinCapacity, Is.False);
            Assert.That(network.IsSupplied(new Vector2Int(1, 1)), Is.False);
            Assert.That(network.IsSupplied(new Vector2Int(3, 0)), Is.False);
            Assert.That(network.Remove(new Vector2Int(3, 0)), Is.True);
            Assert.That(network.IsSupplied(new Vector2Int(1, 1)), Is.True);
        }

        [Test]
        public void SeparateSources_CannotMergeEvenWhenTheyShareAProperty()
        {
            var network = new CookingPropertyNetwork();
            var otherSource = new Vector2Int(5, 0);
            Assert.That(network.TryAddSource(Vector2Int.zero, CookingProperty.Air, 1), Is.True);
            Assert.That(network.TryAddSource(otherSource, CookingProperty.Air, 3), Is.True);
            Assert.That(network.TryAddCollector(Vector2Int.right, Vector2Int.zero), Is.True);
            Assert.That(network.TryAddCollector(new Vector2Int(4, 0), otherSource), Is.True);
            Assert.That(network.TryAddPipe(new Vector2Int(2, 0)), Is.True);
            Assert.That(network.TryAddPipe(new Vector2Int(3, 0)), Is.False);
            Assert.That(network.TryGetStatus(Vector2Int.zero,
                out PropertySupplyStatus first), Is.True);
            Assert.That(network.TryGetStatus(otherSource,
                out PropertySupplyStatus second), Is.True);
            Assert.That(first.Capacity, Is.EqualTo(1));
            Assert.That(second.Capacity, Is.EqualTo(3));
            Assert.That(network.TryGetConnection(new Vector2Int(3, 0), out _), Is.False);
        }

        [Test]
        public void DemandNextToSource_StillNeedsAConnectedPipe()
        {
            var network = new CookingPropertyNetwork();
            Assert.That(network.TryAddSource(Vector2Int.zero, CookingProperty.Time, 1), Is.True);
            Assert.That(network.TryAddCollector(Vector2Int.up, Vector2Int.zero), Is.True);
            Assert.That(network.TryAddPipe(Vector2Int.one), Is.True);
            Assert.That(network.TryAddDemand(Vector2Int.right), Is.True);
            Assert.That(network.IsSupplied(Vector2Int.right), Is.True);

            Assert.That(network.Remove(Vector2Int.one), Is.True);
            Assert.That(network.IsSupplied(Vector2Int.right), Is.False);
            Assert.That(network.TryGetStatus(Vector2Int.zero,
                out PropertySupplyStatus status), Is.True);
            Assert.That(status.ConnectedDemand, Is.Zero);
        }

        [Test]
        public void CapacityCountsDemandUnitsRatherThanConnectionCount()
        {
            var network = new CookingPropertyNetwork();
            Assert.That(network.TryAddSource(Vector2Int.zero, CookingProperty.Moisture, 3), Is.True);
            Assert.That(network.TryAddCollector(Vector2Int.right, Vector2Int.zero), Is.True);
            Assert.That(network.TryAddDemand(new Vector2Int(2, 0), 2), Is.True);
            Assert.That(network.TryGetStatus(Vector2Int.zero,
                out PropertySupplyStatus status), Is.True);
            Assert.That(status.ConnectedConsumers, Is.EqualTo(1));
            Assert.That(status.ConnectedDemand, Is.EqualTo(2));
            Assert.That(status.AvailableCapacity, Is.EqualTo(1));
            Assert.That(network.TryAddDemand(new Vector2Int(1, 1), 0), Is.False);
        }

        [Test]
        public void ProcessorDemand_OnlyConnectsAtItsFacingPipeCell()
        {
            var network = new CookingPropertyNetwork();
            var source = new Vector2Int(0, -3);
            var port = Vector2Int.zero;
            var outside = Vector2Int.right;
            Assert.That(network.TryAddSource(source, CookingProperty.Heat, 1), Is.True);
            Assert.That(network.TryAddCollector(new Vector2Int(0, -2), source), Is.True);
            Assert.That(network.TryAddPipe(new Vector2Int(0, -1)), Is.True);
            Assert.That(network.TryAddDemand(port, 1, outside), Is.False);
            Assert.That(network.TryAddPipe(new Vector2Int(1, -1)), Is.True);
            Assert.That(network.TryAddPipe(outside), Is.True);
            Assert.That(network.TryAddDemand(port, 1, outside), Is.True);
            Assert.That(network.IsSupplied(port), Is.True);
            Assert.That(network.Remove(new Vector2Int(1, -1)), Is.True);
            Assert.That(network.IsSupplied(port), Is.False,
                "A connected pipe on another side cannot supply the facing port.");
        }
    }
}
