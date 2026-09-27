using CozyFoodFactory.Buildings;
using CozyFoodFactory.Food;
using CozyFoodFactory.Logistics;
using NUnit.Framework;
using UnityEngine;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class ProcessorPortLayoutTests
    {
        [TestCase(BuildingRotation.Degrees0, 0, 1, 1, 1, -1, 1, 0, 2, 1, 0,
            GridDirection.West, GridDirection.North, GridDirection.South)]
        [TestCase(BuildingRotation.Degrees90, 1, 1, 1, 0, 1, 2, 2, 1, 0, 0,
            GridDirection.North, GridDirection.East, GridDirection.West)]
        [TestCase(BuildingRotation.Degrees180, 1, 0, 0, 0, 2, 0, 1, -1, 0, 1,
            GridDirection.East, GridDirection.South, GridDirection.North)]
        [TestCase(BuildingRotation.Degrees270, 0, 0, 0, 1, 0, -1, -1, 0, 1, 1,
            GridDirection.South, GridDirection.West, GridDirection.East)]
        public void PortsAndCells_RotateTogether(BuildingRotation rotation,
            int chamberX, int chamberY, int propertyX, int propertyY,
            int foodInputX, int foodInputY, int foodOutputX, int foodOutputY,
            int propertyOutsideX, int propertyOutsideY,
            GridDirection foodFacing, GridDirection outputFacing,
            GridDirection propertyFacing)
        {
            Vector2Int anchor = new(7, 11);
            Assert.That(ProcessorPortLayout.GetChamberCell(anchor, rotation),
                Is.EqualTo(anchor + new Vector2Int(chamberX, chamberY)));
            Assert.That(ProcessorPortLayout.GetPropertyCell(anchor, rotation),
                Is.EqualTo(anchor + new Vector2Int(propertyX, propertyY)));
            Assert.That(ProcessorPortLayout.GetFoodInputOutsideCell(anchor, rotation),
                Is.EqualTo(anchor + new Vector2Int(foodInputX, foodInputY)));
            Assert.That(ProcessorPortLayout.GetFoodOutputOutsideCell(anchor, rotation),
                Is.EqualTo(anchor + new Vector2Int(foodOutputX, foodOutputY)));
            Assert.That(ProcessorPortLayout.GetPropertyOutsideCell(anchor, rotation),
                Is.EqualTo(anchor + new Vector2Int(propertyOutsideX, propertyOutsideY)));
            Assert.That(ProcessorPortLayout.GetPropertyOutsideCell(anchor, rotation),
                Is.EqualTo(anchor + rotation.RotateCell(Vector2Int.right,
                    ProcessorPortLayout.Footprint)));
            var placement = new BuildingPlacement("Processor", anchor,
                ProcessorPortLayout.Footprint, rotation,
                new[] { ProcessorPortLayout.Chamber,
                    ProcessorPortLayout.PropertyModule, ProcessorPortLayout.Support });
            CollectionAssert.DoesNotContain(placement.OccupiedCells,
                ProcessorPortLayout.GetPropertyOutsideCell(anchor, rotation));
            Assert.That(ProcessorPortLayout.GetFoodInputFacing(rotation),
                Is.EqualTo(foodFacing));
            Assert.That(ProcessorPortLayout.GetFoodIncomingDirection(rotation),
                Is.EqualTo((GridDirection)(((int)foodFacing + 2) % 4)));
            Assert.That(ProcessorPortLayout.GetFoodOutputFacing(rotation),
                Is.EqualTo(outputFacing));
            Assert.That(ProcessorPortLayout.GetPropertyInputFacing(rotation),
                Is.EqualTo(propertyFacing));
        }

        [TestCase(BuildingRotation.Degrees0)]
        [TestCase(BuildingRotation.Degrees90)]
        [TestCase(BuildingRotation.Degrees180)]
        [TestCase(BuildingRotation.Degrees270)]
        public void PlacementPreview_UsesTheSameRotatedPortDirections(
            BuildingRotation rotation)
        {
            var gameObject = new GameObject("Processor port test");
            try
            {
                var behavior = gameObject.AddComponent<ProcessorPlacementBehavior>();
                Assert.That(behavior.PortPreviews.Count, Is.EqualTo(3));
                Assert.That(behavior.PortPreviews[0].Kind, Is.EqualTo(BuildingPortKind.Input));
                Assert.That(behavior.PortPreviews[1].Kind, Is.EqualTo(BuildingPortKind.Output));
                Assert.That(behavior.PortPreviews[2].Kind,
                    Is.EqualTo(BuildingPortKind.PropertyInput));
                Assert.That(behavior.PortPreviews[0].LocalPosition,
                    Is.EqualTo(new Vector2(-0.85f, 0.5f)));
                Assert.That(behavior.PortPreviews[1].LocalPosition,
                    Is.EqualTo(new Vector2(-0.5f, 0.85f)));
                Assert.That(behavior.PortPreviews[2].LocalPosition,
                    Is.EqualTo(new Vector2(0.5f, 0.15f)));
                Assert.That(behavior.PortPreviews[0].ResolveDirection(rotation).ToGridDirection(),
                    Is.EqualTo(ProcessorPortLayout.GetFoodInputFacing(rotation)));
                Assert.That(behavior.PortPreviews[1].ResolveDirection(rotation).ToGridDirection(),
                    Is.EqualTo(ProcessorPortLayout.GetFoodOutputFacing(rotation)));
                Assert.That(behavior.PortPreviews[2].ResolveDirection(rotation).ToGridDirection(),
                    Is.EqualTo(ProcessorPortLayout.GetPropertyInputFacing(rotation)));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
