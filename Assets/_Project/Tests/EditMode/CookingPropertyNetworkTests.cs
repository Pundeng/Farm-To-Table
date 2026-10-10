using CozyFoodFactory.Food;
using NUnit.Framework;
using UnityEngine;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class CookingPropertyNetworkTests
    {
        [TestCase(CookingProperty.Heat)]
        [TestCase(CookingProperty.Water)]
        [TestCase(CookingProperty.Time)]
        [TestCase(CookingProperty.Cold)]
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

        [TestCase(CookingProperty.Heat)]
        [TestCase(CookingProperty.Water)]
        [TestCase(CookingProperty.Time)]
        [TestCase(CookingProperty.Cold)]
        public void CollectorBetweenSamePropertySources_KeepsOneOwnerAndCapacity(CookingProperty property)
        {
            var network = new CookingPropertyNetwork();
            var otherSource = new Vector2Int(2, 0);
            Assert.That(network.TryAddSource(Vector2Int.zero, property, 1), Is.True);
            Assert.That(network.TryAddSource(otherSource, property, 3), Is.True);
            Assert.That(network.TryAddCollector(Vector2Int.right, Vector2Int.zero), Is.True);
            Assert.That(network.TryGetConnection(Vector2Int.right, out PropertyConnection collector), Is.True);
            Assert.That(collector.SourceCell, Is.EqualTo(Vector2Int.zero));
            Assert.That(network.TryAddDemand(Vector2Int.one, 2), Is.True);
            Assert.That(network.IsSupplied(Vector2Int.one), Is.False,
                "The neighboring source must not contribute its capacity.");
            Assert.That(network.TryGetStatus(Vector2Int.zero, out PropertySupplyStatus owner), Is.True);
            Assert.That(owner.ConnectedDemand, Is.EqualTo(2));
            Assert.That(network.TryGetStatus(otherSource, out PropertySupplyStatus other), Is.True);
            Assert.That(other.ConnectedDemand, Is.Zero);
            Assert.That(other.Capacity, Is.EqualTo(3));

            var restored = new CookingPropertyNetwork();
            Assert.That(restored.TryAddSource(Vector2Int.zero, property, 1), Is.True);
            Assert.That(restored.TryAddSource(otherSource, property, 3), Is.True);
            Assert.That(restored.TryRestoreConnection(collector), Is.True);
            Assert.That(restored.TryGetConnection(Vector2Int.right, out PropertyConnection saved), Is.True);
            Assert.That(saved.SourceCell, Is.EqualTo(Vector2Int.zero));
            Assert.That(restored.IsConnectedToSource(Vector2Int.right), Is.True);

            Assert.That(network.Remove(Vector2Int.right), Is.True);
            Assert.That(network.TryGetConnection(Vector2Int.right, out _), Is.False);
            Assert.That(network.IsSupplied(Vector2Int.one), Is.False);
            Assert.That(network.TryAddCollector(Vector2Int.right, otherSource), Is.True);
            Assert.That(network.TryGetConnection(Vector2Int.right, out PropertyConnection reinstalled), Is.True);
            Assert.That(reinstalled.SourceCell, Is.EqualTo(otherSource));
        }

        [Test]
        public void CollectorBetweenDifferentProperties_RemainsBlocked()
        {
            var network = new CookingPropertyNetwork();
            Assert.That(network.TryAddSource(Vector2Int.zero, CookingProperty.Heat, 1), Is.True);
            Assert.That(network.TryAddSource(new Vector2Int(2, 0), CookingProperty.Water, 1), Is.True);
            Assert.That(network.TryAddCollector(Vector2Int.right, Vector2Int.zero), Is.False);
            Assert.That(network.TryGetConnection(Vector2Int.right, out _), Is.False);
        }

        [TestCase(CookingProperty.Heat)]
        [TestCase(CookingProperty.Water)]
        [TestCase(CookingProperty.Time)]
        [TestCase(CookingProperty.Cold)]
        public void AdjacentCollectorsWithDifferentOwners_KeepSeparateSupply(CookingProperty property)
        {
            var network = new CookingPropertyNetwork();
            var otherSource = new Vector2Int(1, 2);
            Assert.That(network.TryAddSource(Vector2Int.zero, property, 1), Is.True);
            Assert.That(network.TryAddSource(otherSource, property, 3), Is.True);
            Assert.That(network.TryAddCollector(Vector2Int.right, Vector2Int.zero), Is.True);
            Assert.That(network.TryAddCollector(Vector2Int.one, otherSource), Is.True);
            Assert.That(network.TryGetConnection(Vector2Int.right, out PropertyConnection first), Is.True);
            Assert.That(network.TryGetConnection(Vector2Int.one, out PropertyConnection second), Is.True);
            Assert.That(first.SourceCell, Is.EqualTo(Vector2Int.zero));
            Assert.That(second.SourceCell, Is.EqualTo(otherSource));
            Assert.That(network.TryAddDemand(new Vector2Int(2, 0), 2), Is.True);
            Assert.That(network.TryAddDemand(new Vector2Int(2, 1), 1), Is.True);
            Assert.That(network.IsSupplied(new Vector2Int(2, 0)), Is.False);
            Assert.That(network.IsSupplied(new Vector2Int(2, 1)), Is.True);
            Assert.That(network.TryGetStatus(Vector2Int.zero, out PropertySupplyStatus firstStatus), Is.True);
            Assert.That(firstStatus.ConnectedDemand, Is.EqualTo(2));
            Assert.That(network.TryGetStatus(otherSource, out PropertySupplyStatus secondStatus), Is.True);
            Assert.That(secondStatus.ConnectedDemand, Is.EqualTo(1));
            Assert.That(network.Remove(Vector2Int.right), Is.True);
            Assert.That(network.IsConnectedToSource(Vector2Int.one), Is.True);
            Assert.That(network.IsSupplied(new Vector2Int(2, 1)), Is.True);
            Assert.That(network.TryAddCollector(Vector2Int.right, Vector2Int.zero), Is.True);
        }

        [Test]
        public void CollectorTouchingForeignPipe_RemainsBlocked()
        {
            var network = new CookingPropertyNetwork();
            var otherSource = new Vector2Int(1, 3);
            Assert.That(network.TryAddSource(Vector2Int.zero, CookingProperty.Heat, 1), Is.True);
            Assert.That(network.TryAddSource(otherSource, CookingProperty.Heat, 1), Is.True);
            Assert.That(network.TryAddCollector(new Vector2Int(1, 2), otherSource), Is.True);
            Assert.That(network.TryAddPipe(Vector2Int.one), Is.True);
            Assert.That(network.TryAddCollector(Vector2Int.right, Vector2Int.zero), Is.False);
        }

        [Test]
        public void OverlappingCollector_RejectsOccupiedAndNonAdjacentCells()
        {
            var network = new CookingPropertyNetwork();
            var otherSource = new Vector2Int(2, 0);
            Assert.That(network.TryAddSource(Vector2Int.zero, CookingProperty.Heat, 1), Is.True);
            Assert.That(network.TryAddSource(otherSource, CookingProperty.Heat, 1), Is.True);
            Assert.That(network.TryAddCollector(otherSource, Vector2Int.zero), Is.False);
            Assert.That(network.TryAddCollector(new Vector2Int(0, 2), Vector2Int.zero), Is.False);
            Assert.That(network.TryAddCollector(Vector2Int.right, Vector2Int.zero), Is.True);
            Assert.That(network.TryAddCollector(Vector2Int.right, otherSource), Is.False);
        }

        [TestCase(CookingProperty.Heat)]
        [TestCase(CookingProperty.Water)]
        [TestCase(CookingProperty.Time)]
        [TestCase(CookingProperty.Cold)]
        public void OwnedPipeCanPassForeignPipe_WithoutMerging(CookingProperty otherProperty)
        {
            var network = new CookingPropertyNetwork();
            var owner = Vector2Int.zero;
            var other = new Vector2Int(5, 2);
            Assert.That(network.TryAddSource(owner, CookingProperty.Heat, 1), Is.True);
            Assert.That(network.TryAddSource(other, otherProperty, 3), Is.True);
            Assert.That(network.TryAddCollector(Vector2Int.right, owner), Is.True);
            Assert.That(network.TryAddPipe(new Vector2Int(2, 0), owner), Is.True);
            Assert.That(network.TryAddCollector(new Vector2Int(4, 2), other), Is.True);
            Assert.That(network.TryAddPipe(new Vector2Int(3, 2), other), Is.True);
            Assert.That(network.TryAddPipe(new Vector2Int(3, 1), other), Is.True);
            Assert.That(network.TryAddPipe(new Vector2Int(3, 0)), Is.False,
                "An ambiguous starting cell needs an explicit drag owner.");
            Assert.That(network.TryAddPipe(new Vector2Int(3, 0), owner), Is.True);
            Assert.That(network.TryGetConnection(new Vector2Int(3, 0), out PropertyConnection pipe), Is.True);
            Assert.That(pipe.SourceCell, Is.EqualTo(owner));
            Assert.That(pipe.Property, Is.EqualTo(CookingProperty.Heat));
            Assert.That(network.TryAddPipe(new Vector2Int(4, 1), owner), Is.False,
                "Touching only the foreign network cannot extend the selected owner.");
            Assert.That(network.Remove(new Vector2Int(2, 0)), Is.True);
            Assert.That(network.IsConnectedToSource(new Vector2Int(3, 0)), Is.False);
            Assert.That(network.IsConnectedToSource(new Vector2Int(3, 1)), Is.True);
            Assert.That(network.TryAddPipe(new Vector2Int(2, 0), owner), Is.True);
            Assert.That(network.IsConnectedToSource(new Vector2Int(3, 0)), Is.True);
        }

        [Test]
        public void SeparateSources_CannotMergeEvenWhenTheyShareAProperty()
        {
            var network = new CookingPropertyNetwork();
            var otherSource = new Vector2Int(5, 0);
            Assert.That(network.TryAddSource(Vector2Int.zero, CookingProperty.Cold, 1), Is.True);
            Assert.That(network.TryAddSource(otherSource, CookingProperty.Cold, 3), Is.True);
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
            Assert.That(network.TryAddSource(Vector2Int.zero, CookingProperty.Water, 3), Is.True);
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
