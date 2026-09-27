using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.Logistics;
using CozyFoodFactory.Food;
using NUnit.Framework;
using UnityEngine;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class GridOccupancyTests
    {
        [Test]
        public void RectangleSelection_IncludesFarmPlotBelowHarvesterOverlay()
        {
            var occupancy = new GridOccupancy();
            Assert.That(occupancy.TryRegister(nameof(FarmPlot), Vector2Int.zero,
                Vector2Int.one, BuildingRotation.Degrees0,
                out BuildingPlacement plot), Is.True);
            Assert.That(occupancy.TryRegisterOver(nameof(Harvester), Vector2Int.zero,
                new Vector2Int(1, 2), BuildingRotation.Degrees0,
                Vector2Int.zero, plot, out BuildingPlacement harvester), Is.True);
            var selection = new BuildingSelection();
            selection.SelectRectangle(occupancy, Vector2Int.zero, Vector2Int.zero,
                _ => true, includeUnderlying: true);
            CollectionAssert.AreEquivalent(new[] { plot, harvester },
                selection.SelectedPlacements);
        }

        [Test]
        public void PropertyClipboard_RotationKeepsCropAndRelativeCell()
        {
            var option = new BuildingPlacementOption();
            // This assertion describes a one-cell building. The option's
            // default prototype footprint is 2x1 and shifts its rotated anchor.
            typeof(BuildingDefinition).GetField("footprint", BindingFlags.NonPublic |
                BindingFlags.Instance)?.SetValue(option.Definition, Vector2Int.one);
            var connection = new PropertyConnection(new Vector2Int(3, 1),
                Vector2Int.zero, CookingProperty.Heat,
                PropertyConnectionKind.Pipe);
            var group = new BuildingGroupCopy(new[]
            {
                new BuildingGroupCopyItem(option, new Vector2Int(1, 1),
                    BuildingRotation.Degrees0, cropId: "Apple")
            }, new[] { connection });
            BuildingGroupCopy rotated = group.RotateClockwise();
            Assert.That(rotated.Items[0].CropId, Is.EqualTo("Apple"));
            Assert.That(rotated.PropertyItems[0].Offset - rotated.Items[0].Offset,
                Is.EqualTo(new Vector2Int(0, -2)));
        }
        [Test]
        public void GroupMove_AllowsOverlapWithItsOwnSourceFootprints()
        {
            var occupancy = new GridOccupancy();
            occupancy.TryRegister("First", Vector2Int.zero, new Vector2Int(2, 1),
                BuildingRotation.Degrees0, out BuildingPlacement first);
            occupancy.TryRegister("Second", new Vector2Int(3, 0),
                new Vector2Int(2, 1), BuildingRotation.Degrees0,
                out BuildingPlacement second);
            var option = new BuildingPlacementOption();
            var group = new BuildingGroupCopy(new[]
            {
                new BuildingGroupCopyItem(option, first.AnchorCell, first.Rotation),
                new BuildingGroupCopyItem(option, second.AnchorCell, second.Rotation)
            });
            var sources = new HashSet<BuildingPlacement> { first, second };

            Assert.That(group.CanPlace(Vector2Int.right, occupancy, sources), Is.True);
            Assert.That(group.CanPlace(Vector2Int.right, occupancy), Is.False);
        }

        [Test]
        public void GroupMove_RejectsUnrelatedCollisionWithoutChangingSources()
        {
            var occupancy = new GridOccupancy();
            occupancy.TryRegister("Source", Vector2Int.zero,
                new Vector2Int(2, 1), BuildingRotation.Degrees0,
                out BuildingPlacement source);
            occupancy.TryRegister("Blocker", new Vector2Int(2, 0), Vector2Int.one,
                BuildingRotation.Degrees0, out BuildingPlacement blocker);
            var option = new BuildingPlacementOption();
            var group = new BuildingGroupCopy(new[]
            {
                new BuildingGroupCopyItem(option, source.AnchorCell, source.Rotation)
            });

            Assert.That(group.CanPlace(Vector2Int.right, occupancy,
                new HashSet<BuildingPlacement> { source }), Is.False);
            Assert.That(occupancy.TryGetBuilding(Vector2Int.zero, out BuildingPlacement current),
                Is.True);
            Assert.That(current, Is.SameAs(source));
            Assert.That(occupancy.TryGetBuilding(new Vector2Int(2, 0), out current), Is.True);
            Assert.That(current, Is.SameAs(blocker));
        }

        [Test]
        public void GroupMove_ConfirmationRelocatesFootprintAndCancellationLeavesIt()
        {
            var occupancy = new GridOccupancy();
            occupancy.TryRegister("Source", Vector2Int.zero,
                new Vector2Int(2, 1), BuildingRotation.Degrees0,
                out BuildingPlacement source);
            var option = new BuildingPlacementOption();
            var group = new BuildingGroupCopy(new[]
            {
                new BuildingGroupCopyItem(option, source.AnchorCell, source.Rotation)
            });
            var sources = new HashSet<BuildingPlacement> { source };

            Assert.That(group.CanPlace(Vector2Int.right, occupancy, sources), Is.True);
            Assert.That(occupancy.TryGetBuilding(Vector2Int.zero, out BuildingPlacement current),
                Is.True, "Preview and cancellation leave the source registered.");
            Assert.That(current, Is.SameAs(source));

            occupancy.Remove(source);
            Assert.That(occupancy.TryRegister("Moved", Vector2Int.right,
                option.Definition.Footprint, group.Items[0].Rotation,
                out BuildingPlacement moved), Is.True);
            Assert.That(occupancy.TryGetBuilding(Vector2Int.zero, out _), Is.False);
            Assert.That(occupancy.TryGetBuilding(Vector2Int.right, out current), Is.True);
            Assert.That(current, Is.SameAs(moved));
        }

        [Test]
        public void GroupMove_FailedPlacementRestoresOriginalPlacementIdentity()
        {
            var occupancy = new GridOccupancy();
            occupancy.TryRegister("Source", Vector2Int.zero, Vector2Int.one,
                BuildingRotation.Degrees90, out BuildingPlacement source);

            Assert.That(occupancy.Remove(source), Is.True);
            Assert.That(occupancy.TryRestore(source), Is.True);
            Assert.That(occupancy.TryGetBuilding(Vector2Int.zero,
                out BuildingPlacement restored), Is.True);
            Assert.That(restored, Is.SameAs(source));
            Assert.That(restored.Rotation, Is.EqualTo(BuildingRotation.Degrees90));
        }

        [Test]
        public void GroupCopy_NormalizesPositionsAndKeepsOrientationAndCrop()
        {
            var option = new BuildingPlacementOption();
            var group = new BuildingGroupCopy(new[]
            {
                new BuildingGroupCopyItem(option, new Vector2Int(8, 4),
                    BuildingRotation.Degrees90, "Apple"),
                new BuildingGroupCopyItem(option, new Vector2Int(5, 3),
                    BuildingRotation.Degrees180)
            });

            Assert.That(group.Items[0].Offset, Is.EqualTo(new Vector2Int(3, 1)));
            Assert.That(group.Items[1].Offset, Is.EqualTo(Vector2Int.zero));
            Assert.That(group.Items[0].Rotation, Is.EqualTo(BuildingRotation.Degrees90));
            Assert.That(group.Items[1].Rotation, Is.EqualTo(BuildingRotation.Degrees180));
            Assert.That(group.Items[0].CropId, Is.EqualTo("Apple"));
        }

        [Test]
        public void GroupRotation_RotatesMultiCellAnchorsAndOrientationsThroughFourTurns()
        {
            var option = new BuildingPlacementOption();
            var group = new BuildingGroupCopy(new[]
            {
                new BuildingGroupCopyItem(option, Vector2Int.zero,
                    BuildingRotation.Degrees0, "Apple"),
                new BuildingGroupCopyItem(option, new Vector2Int(3, 1),
                    BuildingRotation.Degrees90)
            });

            BuildingGroupCopy quarterTurn = group.RotateClockwise();

            Assert.That(quarterTurn.Items[0].Offset, Is.EqualTo(new Vector2Int(0, 2)));
            Assert.That(quarterTurn.Items[0].Rotation, Is.EqualTo(BuildingRotation.Degrees90));
            Assert.That(quarterTurn.Items[1].Offset, Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(quarterTurn.Items[1].Rotation, Is.EqualTo(BuildingRotation.Degrees180));
            Assert.That(quarterTurn.Items[0].CropId, Is.EqualTo("Apple"));

            BuildingGroupCopy halfTurn = quarterTurn.RotateClockwise();
            Assert.That(halfTurn.Items[0].Offset, Is.EqualTo(new Vector2Int(2, 2)));
            Assert.That(halfTurn.Items[0].Rotation, Is.EqualTo(BuildingRotation.Degrees180));
            Assert.That(halfTurn.Items[1].Offset, Is.EqualTo(Vector2Int.zero));
            Assert.That(halfTurn.Items[1].Rotation, Is.EqualTo(BuildingRotation.Degrees270));

            BuildingGroupCopy threeQuarterTurn = halfTurn.RotateClockwise();
            Assert.That(threeQuarterTurn.Items[0].Offset,
                Is.EqualTo(new Vector2Int(2, 0)));
            Assert.That(threeQuarterTurn.Items[0].Rotation,
                Is.EqualTo(BuildingRotation.Degrees270));
            Assert.That(threeQuarterTurn.Items[1].Offset,
                Is.EqualTo(new Vector2Int(0, 3)));
            Assert.That(threeQuarterTurn.Items[1].Rotation,
                Is.EqualTo(BuildingRotation.Degrees0));

            BuildingGroupCopy fullTurn = threeQuarterTurn.RotateClockwise();
            for (int index = 0; index < group.Items.Count; index++)
            {
                Assert.That(fullTurn.Items[index].Offset,
                    Is.EqualTo(group.Items[index].Offset));
                Assert.That(fullTurn.Items[index].Rotation,
                    Is.EqualTo(group.Items[index].Rotation));
            }
        }

        [Test]
        public void GroupRotation_ValidatesRotatedFootprintsAndUnrelatedCollision()
        {
            var option = new BuildingPlacementOption();
            var group = new BuildingGroupCopy(new[]
            {
                new BuildingGroupCopyItem(option, Vector2Int.zero,
                    BuildingRotation.Degrees0),
                new BuildingGroupCopyItem(option, new Vector2Int(3, 0),
                    BuildingRotation.Degrees0)
            });
            var occupancy = new GridOccupancy();
            occupancy.TryRegister("Source A", Vector2Int.zero,
                option.Definition.Footprint, BuildingRotation.Degrees0,
                out BuildingPlacement first);
            occupancy.TryRegister("Source B", new Vector2Int(3, 0),
                option.Definition.Footprint, BuildingRotation.Degrees0,
                out BuildingPlacement second);
            var sources = new HashSet<BuildingPlacement> { first, second };
            BuildingGroupCopy rotated = group.RotateClockwise();

            Assert.That(rotated.CanPlace(Vector2Int.zero, occupancy, sources), Is.True);
            Assert.That(rotated.CanPlace(Vector2Int.zero, occupancy), Is.False);

            occupancy.TryRegister("Blocker", new Vector2Int(0, 4),
                Vector2Int.one, BuildingRotation.Degrees0, out _);
            Assert.That(rotated.CanPlace(Vector2Int.zero, occupancy, sources), Is.False);
            Assert.That(occupancy.TryGetBuilding(Vector2Int.zero,
                out BuildingPlacement original), Is.True);
            Assert.That(original, Is.SameAs(first));
        }

        [Test]
        public void GroupMirror_ReflectsBeltPositionsAndDirections()
        {
            var behaviorObject = new GameObject("Belt placement");
            try
            {
                BuildingPlacementOption option = CreateMirrorOption(
                    behaviorObject.AddComponent<BeltPlacementBehavior>(), Vector2Int.one);
                var group = new BuildingGroupCopy(new[]
                {
                    new BuildingGroupCopyItem(option, Vector2Int.zero,
                        BuildingRotation.Degrees0),
                    new BuildingGroupCopyItem(option, new Vector2Int(2, 1),
                        BuildingRotation.Degrees90),
                    new BuildingGroupCopyItem(option, new Vector2Int(3, 0),
                        BuildingRotation.Degrees270)
                });

                Assert.That(group.TryMirrorHorizontal(out BuildingGroupCopy horizontal), Is.True);
                Assert.That(horizontal.Items.Select(item => item.Offset), Is.EqualTo(new[]
                {
                    new Vector2Int(3, 0), new Vector2Int(1, 1), Vector2Int.zero
                }));
                Assert.That(horizontal.Items.Select(item => item.Rotation.ToGridDirection()),
                    Is.EqualTo(new[]
                    {
                        GridDirection.North, GridDirection.West, GridDirection.East
                    }));

                Assert.That(group.TryMirrorVertical(out BuildingGroupCopy vertical), Is.True);
                Assert.That(vertical.Items.Select(item => item.Offset), Is.EqualTo(new[]
                {
                    new Vector2Int(0, 1), new Vector2Int(2, 0), new Vector2Int(3, 1)
                }));
                Assert.That(vertical.Items.Select(item => item.Rotation.ToGridDirection()),
                    Is.EqualTo(new[]
                    {
                        GridDirection.South, GridDirection.East, GridDirection.West
                    }));
            }
            finally
            {
                Object.DestroyImmediate(behaviorObject);
            }
        }

        [Test]
        public void GroupMirror_RotationCompositionAndUnsupportedOption()
        {
            var behaviorObject = new GameObject("Belt placement");
            try
            {
                BuildingPlacementOption option = CreateMirrorOption(
                    behaviorObject.AddComponent<BeltPlacementBehavior>(), Vector2Int.one);
                var group = new BuildingGroupCopy(new[]
                {
                    new BuildingGroupCopyItem(option, Vector2Int.zero,
                        BuildingRotation.Degrees90),
                    new BuildingGroupCopyItem(option, new Vector2Int(2, 1),
                        BuildingRotation.Degrees180)
                });

                Assert.That(group.TryMirrorHorizontal(out BuildingGroupCopy horizontal), Is.True);
                BuildingGroupCopy horizontalThenRotated = horizontal.RotateClockwise();
                Assert.That(group.RotateClockwise().TryMirrorVertical(
                    out BuildingGroupCopy rotatedThenVertical), Is.True);
                for (int index = 0; index < group.Items.Count; index++)
                {
                    Assert.That(horizontalThenRotated.Items[index].Offset,
                        Is.EqualTo(rotatedThenVertical.Items[index].Offset));
                    Assert.That(horizontalThenRotated.Items[index].Rotation,
                        Is.EqualTo(rotatedThenVertical.Items[index].Rotation));
                }

                var unsupported = new BuildingGroupCopy(new[]
                {
                    new BuildingGroupCopyItem(new BuildingPlacementOption(),
                        Vector2Int.zero, BuildingRotation.Degrees0)
                });
                Assert.That(unsupported.TryMirrorHorizontal(out _), Is.False);
                Assert.That(unsupported.Items[0].Rotation,
                    Is.EqualTo(BuildingRotation.Degrees0));
            }
            finally
            {
                Object.DestroyImmediate(behaviorObject);
            }
        }

        [Test]
        public void GroupMirror_RejectsAsymmetricPortLayout()
        {
            var behaviorObject = new GameObject("Asymmetric placement");
            try
            {
                BuildingPlacementOption option = CreateMirrorOption(
                    behaviorObject.AddComponent<AsymmetricMirrorTestPlacementBehavior>(),
                    new Vector2Int(2, 1));
                var group = new BuildingGroupCopy(new[]
                {
                    new BuildingGroupCopyItem(option, Vector2Int.zero,
                        BuildingRotation.Degrees0)
                });

                Assert.That(group.TryMirrorHorizontal(out _), Is.False);
                Assert.That(group.TryMirrorVertical(out _), Is.False);
                Assert.That(group.Items[0].Rotation, Is.EqualTo(BuildingRotation.Degrees0));
            }
            finally
            {
                Object.DestroyImmediate(behaviorObject);
            }
        }

        private static BuildingPlacementOption CreateMirrorOption(
            MonoBehaviour behavior, Vector2Int footprint)
        {
            var option = new BuildingPlacementOption();
            SetPrivateField(option, "placementBehavior", behavior);
            SetPrivateField(option.Definition, "footprint", footprint);
            return option;
        }

        private static void SetPrivateField<TTarget, TValue>(
            TTarget target, string fieldName, TValue value)
        {
            FieldInfo field = typeof(TTarget).GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        [Test]
        public void GroupPaste_ValidatesEveryFootprintBeforePlacement()
        {
            var option = new BuildingPlacementOption();
            var group = new BuildingGroupCopy(new[]
            {
                new BuildingGroupCopyItem(option, Vector2Int.zero,
                    BuildingRotation.Degrees0),
                new BuildingGroupCopyItem(option, new Vector2Int(3, 0),
                    BuildingRotation.Degrees90)
            });
            var occupancy = new GridOccupancy();
            Vector2Int destination = new(5, 5);

            Assert.That(group.CanPlace(destination, occupancy), Is.True);
            foreach (BuildingGroupCopyItem item in group.Items)
            {
                Assert.That(occupancy.TryRegister("Copied", destination + item.Offset,
                    item.Option.Definition.Footprint, item.Rotation, out _), Is.True);
            }

            Assert.That(occupancy.OccupiedCellCount, Is.EqualTo(4));
        }

        [Test]
        public void GroupPaste_RejectsBlockedOrInternallyOverlappingLocations()
        {
            var option = new BuildingPlacementOption();
            var group = new BuildingGroupCopy(new[]
            {
                new BuildingGroupCopyItem(option, Vector2Int.zero,
                    BuildingRotation.Degrees0),
                new BuildingGroupCopyItem(option, new Vector2Int(3, 0),
                    BuildingRotation.Degrees0)
            });
            var occupancy = new GridOccupancy();
            occupancy.TryRegister("Blocker", new Vector2Int(8, 5),
                Vector2Int.one, BuildingRotation.Degrees0, out _);

            Assert.That(group.CanPlace(new Vector2Int(5, 5), occupancy), Is.False);
            Assert.That(occupancy.OccupiedCellCount, Is.EqualTo(1));

            var overlappingGroup = new BuildingGroupCopy(new[]
            {
                new BuildingGroupCopyItem(option, Vector2Int.zero,
                    BuildingRotation.Degrees0),
                new BuildingGroupCopyItem(option, Vector2Int.right,
                    BuildingRotation.Degrees0)
            });
            Assert.That(overlappingGroup.CanPlace(Vector2Int.zero,
                new GridOccupancy()), Is.False);
        }

        [Test]
        public void RectangleSelection_DeduplicatesFootprintsAndExcludesReservedCells()
        {
            var occupancy = new GridOccupancy();
            occupancy.TryRegister("Market", new Vector2Int(0, 0), Vector2Int.one,
                BuildingRotation.Degrees0, out BuildingPlacement market);
            occupancy.TryRegister("Machine", new Vector2Int(1, 0),
                new Vector2Int(2, 1), BuildingRotation.Degrees0,
                out BuildingPlacement machine);
            occupancy.TryRegister("Belt", new Vector2Int(2, 1), Vector2Int.one,
                BuildingRotation.Degrees0, out BuildingPlacement belt);
            var selection = new BuildingSelection();

            selection.SelectRectangle(occupancy, new Vector2Int(2, 1),
                Vector2Int.zero, placement => placement != market);

            CollectionAssert.AreEquivalent(new[] { machine, belt },
                selection.SelectedPlacements);
            Assert.That(selection.SelectedPlacements.Count, Is.EqualTo(2));
        }

        [Test]
        public void RectangleSelection_ReplacesPriorSelectionAndTracksRemoval()
        {
            var occupancy = new GridOccupancy();
            occupancy.TryRegister("First", Vector2Int.zero, Vector2Int.one,
                BuildingRotation.Degrees0, out BuildingPlacement first);
            occupancy.TryRegister("Second", Vector2Int.right, Vector2Int.one,
                BuildingRotation.Degrees0, out BuildingPlacement second);
            var selection = new BuildingSelection();

            selection.SelectRectangle(occupancy, Vector2Int.zero,
                Vector2Int.right, _ => true);
            selection.SelectRectangle(occupancy, Vector2Int.right,
                Vector2Int.right, _ => true);
            Assert.That(selection.Contains(first), Is.False);
            Assert.That(selection.Contains(second), Is.True);

            occupancy.Remove(second);
            Assert.That(selection.Remove(second), Is.True);
            Assert.That(selection.SelectedPlacements, Is.Empty);
        }

        [Test]
        public void RegisteringFootprint_OccupiesEveryCoveredCell()
        {
            var occupancy = new GridOccupancy();

            bool registered = occupancy.TryRegister(
                "Machine",
                new Vector2Int(3, 4),
                new Vector2Int(2, 2),
                BuildingRotation.Degrees0,
                out BuildingPlacement placement);

            Assert.That(registered, Is.True);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    new Vector2Int(3, 4),
                    new Vector2Int(4, 4),
                    new Vector2Int(3, 5),
                    new Vector2Int(4, 5)
                },
                placement.OccupiedCells.ToArray());
            Assert.That(occupancy.OccupiedCellCount, Is.EqualTo(4));
        }

        [Test]
        public void OverlappingPlacement_IsRejectedWithoutChangingOccupancy()
        {
            var occupancy = new GridOccupancy();
            occupancy.TryRegister(
                "First",
                Vector2Int.zero,
                new Vector2Int(2, 1),
                BuildingRotation.Degrees0,
                out _);

            bool registered = occupancy.TryRegister(
                "Overlapping",
                new Vector2Int(1, 0),
                Vector2Int.one,
                BuildingRotation.Degrees0,
                out BuildingPlacement placement);

            Assert.That(registered, Is.False);
            Assert.That(placement, Is.Null);
            Assert.That(occupancy.OccupiedCellCount, Is.EqualTo(2));
        }

        [Test]
        public void RemovingBuilding_FreesAllOfItsCells()
        {
            var occupancy = new GridOccupancy();
            occupancy.TryRegister(
                "Machine",
                Vector2Int.zero,
                new Vector2Int(2, 1),
                BuildingRotation.Degrees0,
                out BuildingPlacement placement);

            bool removed = occupancy.Remove(placement);

            Assert.That(removed, Is.True);
            Assert.That(occupancy.OccupiedCellCount, Is.Zero);
            Assert.That(
                occupancy.CanPlace(Vector2Int.zero, new Vector2Int(2, 1), BuildingRotation.Degrees0),
                Is.True);
        }

        [Test]
        public void RemovedCell_CanRegisterNewBuilding()
        {
            var occupancy = new GridOccupancy();
            occupancy.TryRegister(
                "Belt",
                Vector2Int.zero,
                Vector2Int.one,
                BuildingRotation.Degrees0,
                out BuildingPlacement removedPlacement);
            occupancy.Remove(removedPlacement);

            bool registered = occupancy.TryRegister(
                "Replacement",
                Vector2Int.zero,
                Vector2Int.one,
                BuildingRotation.Degrees90,
                out BuildingPlacement replacement);

            Assert.That(registered, Is.True);
            Assert.That(replacement.AnchorCell, Is.EqualTo(Vector2Int.zero));
        }

        [Test]
        public void RectangularFootprint_AtNinetyDegrees_SwapsDimensions()
        {
            Vector2Int rotated = BuildingRotation.Degrees90.GetRotatedFootprint(new Vector2Int(2, 1));

            Assert.That(rotated, Is.EqualTo(new Vector2Int(1, 2)));
        }

        [TestCase(BuildingRotation.Degrees0, 1, 0)]
        [TestCase(BuildingRotation.Degrees90, 0, 0)]
        [TestCase(BuildingRotation.Degrees180, 0, 1)]
        [TestCase(BuildingRotation.Degrees270, 1, 1)]
        public void LFootprint_RotatesOccupiedCellsAndLeavesOneOpenCorner(
            BuildingRotation rotation, int emptyX, int emptyY)
        {
            BuildingDefinition definition = CreateLDefinition();
            var occupancy = new GridOccupancy();
            Vector2Int anchor = new(3, 4);

            Assert.That(occupancy.TryRegister(definition, anchor, rotation,
                out BuildingPlacement processor), Is.True);
            Assert.That(processor.OccupiedCells.Count, Is.EqualTo(3));
            Assert.That(occupancy.OccupiedCellCount, Is.EqualTo(3));
            Vector2Int emptyCell = anchor + new Vector2Int(emptyX, emptyY);
            Assert.That(occupancy.TryGetBuilding(emptyCell, out _), Is.False);
            Assert.That(occupancy.TryRegister("Belt", emptyCell, Vector2Int.one,
                BuildingRotation.Degrees0, out BuildingPlacement belt), Is.True);
            foreach (Vector2Int occupied in processor.OccupiedCells)
            {
                Assert.That(occupancy.CanPlace(occupied, Vector2Int.one,
                    BuildingRotation.Degrees0), Is.False);
                Assert.That(occupancy.TryGetBuilding(occupied, out BuildingPlacement found),
                    Is.True);
                Assert.That(found, Is.SameAs(processor));
            }

            Assert.That(occupancy.Remove(processor), Is.True);
            Assert.That(occupancy.TryGetBuilding(emptyCell, out BuildingPlacement remaining),
                Is.True);
            Assert.That(remaining, Is.SameAs(belt));
            Assert.That(occupancy.OccupiedCellCount, Is.EqualTo(1));
            Assert.That(occupancy.TryRestore(processor), Is.True);
        }

        [Test]
        public void LFootprint_SelectionIgnoresOpenCorner()
        {
            var occupancy = new GridOccupancy();
            BuildingDefinition definition = CreateLDefinition();
            occupancy.TryRegister(definition, Vector2Int.zero, BuildingRotation.Degrees0,
                out BuildingPlacement processor);
            var selection = new BuildingSelection();

            selection.SelectRectangle(occupancy, Vector2Int.right, Vector2Int.right,
                _ => true);
            Assert.That(selection.SelectedPlacements, Is.Empty);
            selection.SelectRectangle(occupancy, Vector2Int.up, Vector2Int.up,
                _ => true);
            Assert.That(selection.SelectedPlacements, Is.EquivalentTo(new[] { processor }));
        }

        [Test]
        public void LFootprint_RejectsCollisionOnArmButAcceptsOccupiedOpenCorner()
        {
            var occupancy = new GridOccupancy();
            BuildingDefinition definition = CreateLDefinition();
            occupancy.TryRegister("Blocker", Vector2Int.up, Vector2Int.one,
                BuildingRotation.Degrees0, out _);

            Assert.That(occupancy.CanPlace(definition, Vector2Int.zero,
                BuildingRotation.Degrees0), Is.False);
            Assert.That(occupancy.TryRegister(definition, Vector2Int.zero,
                BuildingRotation.Degrees0, out _), Is.False);
            Assert.That(occupancy.CanPlace(definition, Vector2Int.zero,
                BuildingRotation.Degrees180), Is.True);
        }

        [TestCase(BuildingRotation.Degrees0)]
        [TestCase(BuildingRotation.Degrees90)]
        [TestCase(BuildingRotation.Degrees180)]
        [TestCase(BuildingRotation.Degrees270)]
        public void LFootprint_CellRotationMatchesPortPreviewTransform(
            BuildingRotation rotation)
        {
            Vector2Int bounds = new(2, 2);
            Vector2Int localCell = Vector2Int.up;
            Vector2Int rotatedCell = rotation.RotateCell(localCell, bounds);
            Vector2 localCenter = (Vector2)localCell - new Vector2(0.5f, 0.5f);
            Vector2 rotatedCenter = Quaternion.Euler(0f, 0f, -(int)rotation) * localCenter;
            Vector2 expected = (Vector2)rotatedCell - new Vector2(0.5f, 0.5f);
            Assert.That((rotatedCenter - expected).sqrMagnitude, Is.LessThan(0.000001f));

            var port = new BuildingPortPreview(BuildingPortKind.Input, localCenter,
                BuildingRotation.Degrees0);
            Assert.That(port.ResolveDirection(rotation), Is.EqualTo(rotation));
        }

        private static BuildingDefinition CreateLDefinition()
        {
            var definition = new BuildingDefinition();
            SetPrivateField(definition, "id", "Processor");
            SetPrivateField(definition, "footprint", new Vector2Int(2, 2));
            SetPrivateField(definition, "occupiedCells", new[]
            {
                Vector2Int.zero, Vector2Int.up, Vector2Int.one
            });
            definition.Validate();
            return definition;
        }

        [Test]
        public void DragTracker_FillsSkippedCellsWithContinuousOrthogonalPath()
        {
            var tracker = new GridDragTracker();

            CollectionAssert.AreEqual(
                new[] { Vector2Int.zero },
                tracker.Continue(Vector2Int.zero));
            CollectionAssert.AreEqual(
                new[]
                {
                    new Vector2Int(1, 0),
                    new Vector2Int(2, 0),
                    new Vector2Int(2, 1),
                    new Vector2Int(2, 2)
                },
                tracker.Continue(new Vector2Int(2, 2)));
        }

        [Test]
        public void DragTracker_DoesNotReturnVisitedCellTwice()
        {
            var tracker = new GridDragTracker();
            tracker.Continue(Vector2Int.zero);
            tracker.Continue(new Vector2Int(2, 0));

            Assert.That(tracker.Continue(Vector2Int.zero), Is.Empty);
        }

        [Test]
        public void BeltDragPlanner_FollowsStraightPathAndNinetyDegreeTurn()
        {
            var planner = new BeltDragPlacementPlanner();

            var steps = planner.Continue(new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(1, 1)
            });

            Assert.That(steps.Count, Is.EqualTo(2));
            Assert.That(steps[0].Cell, Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(steps[0].Rotation, Is.EqualTo(BuildingRotation.Degrees90));
            Assert.That(steps[1].Cell, Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(steps[1].Rotation, Is.EqualTo(BuildingRotation.Degrees0));
            Assert.That(
                planner.TryComplete(
                    BuildingRotation.Degrees180,
                    out BeltPlacementStep finalStep),
                Is.True);
            Assert.That(finalStep.Cell, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(finalStep.Rotation, Is.EqualTo(BuildingRotation.Degrees0));
        }

        [Test]
        public void BeltDragPlanner_SingleCellUsesManualRotation()
        {
            var planner = new BeltDragPlacementPlanner();
            planner.Continue(new[] { Vector2Int.zero });

            bool completed = planner.TryComplete(
                BuildingRotation.Degrees270,
                out BeltPlacementStep step);

            Assert.That(completed, Is.True);
            Assert.That(step.Cell, Is.EqualTo(Vector2Int.zero));
            Assert.That(step.Rotation, Is.EqualTo(BuildingRotation.Degrees270));
        }

        [TestCase(BuildingRotation.Degrees0)]
        [TestCase(BuildingRotation.Degrees90)]
        [TestCase(BuildingRotation.Degrees180)]
        [TestCase(BuildingRotation.Degrees270)]
        public void DirectionalPortPreview_ResolvesToBuildingRotation(
            BuildingRotation buildingRotation)
        {
            var ports = BuildingPortPreviewLayouts.DirectionalProcessor;

            Assert.That(ports.Count, Is.EqualTo(2));
            Assert.That(ports[0].Kind, Is.EqualTo(BuildingPortKind.Input));
            Assert.That(ports[0].LocalPosition.y, Is.LessThan(0f));
            Assert.That(ports[0].ResolveDirection(buildingRotation), Is.EqualTo(buildingRotation));
            Assert.That(ports[1].Kind, Is.EqualTo(BuildingPortKind.Output));
            Assert.That(ports[1].LocalPosition.y, Is.GreaterThan(0f));
            Assert.That(ports[1].ResolveDirection(buildingRotation), Is.EqualTo(buildingRotation));
        }

        [Test]
        public void DragTracker_ResetAllowsCellOnNextDrag()
        {
            var tracker = new GridDragTracker();
            tracker.Continue(Vector2Int.zero);

            tracker.Reset();

            CollectionAssert.AreEqual(
                new[] { Vector2Int.zero },
                tracker.Continue(Vector2Int.zero));
        }

        [Test]
        public void ContinuousBeltDrag_RejectsMarketCellButKeepsAdjacentCellsAvailable()
        {
            var occupancy = new GridOccupancy();
            var tracker = new GridDragTracker();
            occupancy.TryRegister(
                "Market",
                new Vector2Int(1, 0),
                Vector2Int.one,
                BuildingRotation.Degrees0,
                out _);

            foreach (Vector2Int cell in tracker.Continue(Vector2Int.zero))
            {
                occupancy.TryRegister(
                    "Belt",
                    cell,
                    Vector2Int.one,
                    BuildingRotation.Degrees0,
                    out _);
            }

            foreach (Vector2Int cell in tracker.Continue(new Vector2Int(2, 0)))
            {
                occupancy.TryRegister(
                    "Belt",
                    cell,
                    Vector2Int.one,
                    BuildingRotation.Degrees0,
                    out _);
            }

            Assert.That(occupancy.OccupiedCellCount, Is.EqualTo(3));
            Assert.That(
                occupancy.TryGetBuilding(new Vector2Int(1, 0), out BuildingPlacement market),
                Is.True);
            Assert.That(market.DefinitionId, Is.EqualTo("Market"));
            Assert.That(
                occupancy.TryGetBuilding(Vector2Int.zero, out BuildingPlacement firstBelt),
                Is.True);
            Assert.That(firstBelt.DefinitionId, Is.EqualTo("Belt"));
            Assert.That(
                occupancy.TryGetBuilding(new Vector2Int(2, 0), out BuildingPlacement lastBelt),
                Is.True);
            Assert.That(lastBelt.DefinitionId, Is.EqualTo("Belt"));
            Assert.That(tracker.Continue(Vector2Int.zero), Is.Empty);
        }

        [Test]
        public void MarketFootprint_RejectsOverlappingBuildingWithoutBlockingAdjacentInputBelt()
        {
            var occupancy = new GridOccupancy();
            var marketCell = new Vector2Int(10, 1);
            occupancy.TryRegister(
                "Market",
                marketCell,
                Vector2Int.one,
                BuildingRotation.Degrees0,
                out _);

            bool overlappingBuildingRegistered = occupancy.TryRegister(
                "Machine",
                marketCell + Vector2Int.left,
                new Vector2Int(2, 1),
                BuildingRotation.Degrees0,
                out _);
            bool adjacentInputBeltRegistered = occupancy.TryRegister(
                "Belt",
                marketCell + Vector2Int.left,
                Vector2Int.one,
                BuildingRotation.Degrees90,
                out _);

            Assert.That(overlappingBuildingRegistered, Is.False);
            Assert.That(adjacentInputBeltRegistered, Is.True);
            Assert.That(occupancy.OccupiedCellCount, Is.EqualTo(2));
        }

        [Test]
        public void Occupancy_SupportsNegativeCoordinates()
        {
            var occupancy = new GridOccupancy();

            bool registered = occupancy.TryRegister(
                "Machine",
                new Vector2Int(-2, -3),
                new Vector2Int(2, 1),
                BuildingRotation.Degrees0,
                out BuildingPlacement placement);

            Assert.That(registered, Is.True);
            Assert.That(placement.OccupiedCells, Does.Contain(new Vector2Int(-2, -3)));
            Assert.That(placement.OccupiedCells, Does.Contain(new Vector2Int(-1, -3)));
            Assert.That(occupancy.CanPlace(new Vector2Int(-1, -3), Vector2Int.one, BuildingRotation.Degrees0), Is.False);
        }
    }

    public sealed class BuildingToolRotationMemoryTests
    {
        [Test]
        public void SwitchingTools_RestoresEachToolsLastRotation()
        {
            var memory = new BuildingToolRotationMemory();
            Assert.That(memory.Get(nameof(CozyFoodFactory.Food.Processor)),
                Is.EqualTo(BuildingRotation.Degrees0));
            memory.Set(nameof(CozyFoodFactory.Food.Processor),
                BuildingRotation.Degrees90);
            memory.Set(nameof(CozyFoodFactory.Food.BasicMixer),
                BuildingRotation.Degrees270);
            Assert.That(memory.Get(nameof(CozyFoodFactory.Food.Processor)),
                Is.EqualTo(BuildingRotation.Degrees90));
            Assert.That(memory.Get(nameof(CozyFoodFactory.Food.BasicMixer)),
                Is.EqualTo(BuildingRotation.Degrees270));
        }
    }

    public sealed class VisualFoundationTests
    {
        [Test]
        public void BuildingVisual_UsesSpriteOrFootprintFallbackWithoutChangingFootprint()
        {
            var parent = new GameObject("Visual test parent");
            var texture = new Texture2D(2, 2);
            var artwork = Sprite.Create(texture, new Rect(0, 0, 2, 2),
                new Vector2(0.5f, 0.5f));
            try
            {
                var definition = new BuildingDefinition();
                Vector2Int footprint = definition.Footprint;
                System.Type factory = typeof(BuildingDefinition).Assembly.GetType(
                    "CozyFoodFactory.Buildings.BuildingVisualFactory");
                MethodInfo create = factory.GetMethod("Create",
                    BindingFlags.Public | BindingFlags.Static);
                var fallback = (GameObject)create.Invoke(null,
                    new object[] { definition, parent.transform, 1f, 10 });
                Assert.That(definition.UsesPlaceholderVisual, Is.True);
                Assert.That(fallback.GetComponentInChildren<SpriteRenderer>().sprite,
                    Is.Not.Null);
                UnityEngine.Object.DestroyImmediate(fallback);

                typeof(BuildingDefinition).GetField("visualSprite",
                    BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(
                        definition, artwork);
                var visual = (GameObject)create.Invoke(null,
                    new object[] { definition, parent.transform, 1f, 10 });
                Assert.That(definition.UsesPlaceholderVisual, Is.False);
                Assert.That(visual.GetComponentInChildren<SpriteRenderer>().sprite,
                    Is.SameAs(artwork));
                Assert.That(definition.Footprint, Is.EqualTo(footprint));
                typeof(BuildingDefinition).GetField("keepVisualUpright",
                    BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(
                        definition, true);
                factory.GetMethod("ApplyRotation", BindingFlags.Public |
                    BindingFlags.Static)?.Invoke(null,
                        new object[] { visual, definition, BuildingRotation.Degrees90 });
                Assert.That(visual.transform.localEulerAngles.z,
                    Is.EqualTo(90f).Within(0.01f));
                UnityEngine.Object.DestroyImmediate(visual);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(artwork);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void FoodVisual_UsesStableIdAndLeavesUnknownFoodForFallback()
        {
            var host = new GameObject("Food visual test");
            var texture = new Texture2D(2, 2);
            var artwork = Sprite.Create(texture, new Rect(0, 0, 2, 2),
                new Vector2(0.5f, 0.5f));
            try
            {
                var coordinator = host.AddComponent<BeltTransportCoordinator>();
                var visual = new FoodVisualDefinition();
                typeof(FoodVisualDefinition).GetField("foodId",
                    BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(
                        visual, "apple");
                typeof(FoodVisualDefinition).GetField("sprite",
                    BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(
                        visual, artwork);
                typeof(BeltTransportCoordinator).GetField("foodVisuals",
                    BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(
                        coordinator, new[] { visual });
                Assert.That(coordinator.GetFoodSprite(new FoodItemData("apple",
                    FoodItemKind.RawIngredient)), Is.SameAs(artwork));
                Assert.That(coordinator.GetFoodSprite(new FoodItemData("new food",
                    FoodItemKind.ProcessedFood)), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(artwork);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }

    public sealed class MachineFeedbackTests
    {
        [Test]
        public void Processor_ResolvesOutputBeforeRecipePropertyAndInput()
        {
            MachineFeedback feedback = MachineFeedbackResolver.Processor(
                true, true, true, true);
            Assert.That(feedback.State, Is.EqualTo(MachineFeedbackState.OutputBlocked));
            Assert.That(feedback.Ports, Is.EqualTo(MachineFeedbackPort.OutputA));
            Assert.That(MachineFeedbackResolver.Processor(false, true, true, true).State,
                Is.EqualTo(MachineFeedbackState.InvalidRecipe));
            Assert.That(MachineFeedbackResolver.Processor(false, false, true, true).State,
                Is.EqualTo(MachineFeedbackState.NeedsProperty));
            Assert.That(MachineFeedbackResolver.Processor(false, false, false, false).HasProblem,
                Is.False);
        }

        [Test]
        public void Mixer_TargetsMissingBAndCombinationSeparately()
        {
            MachineFeedback missing = MachineFeedbackResolver.Mixer(false, false,
                false, true);
            Assert.That(missing.Ports, Is.EqualTo(MachineFeedbackPort.InputB));
            MachineFeedback invalid = MachineFeedbackResolver.Mixer(false, true,
                false, true);
            Assert.That(invalid.Ports, Is.EqualTo(MachineFeedbackPort.Combination));
        }

        [Test]
        public void Cutter_TargetsBlockedOutputsIndividuallyOrTogether()
        {
            Assert.That(MachineFeedbackResolver.Cutter(false, true, false, false).Ports,
                Is.EqualTo(MachineFeedbackPort.OutputB));
            Assert.That(MachineFeedbackResolver.Cutter(true, true, false, false).Ports,
                Is.EqualTo(MachineFeedbackPort.OutputA | MachineFeedbackPort.OutputB));
        }

        [Test]
        public void Harvester_GrowingCropIsNotAProblem()
        {
            Assert.That(MachineFeedbackResolver.Harvester(false, false).HasProblem,
                Is.False);
            Assert.That(MachineFeedbackResolver.Harvester(false, true).Ports,
                Is.EqualTo(MachineFeedbackPort.Crop));
        }

        [Test]
        public void Toasts_DeduplicateActiveEventsAndAggregateCurrency()
        {
            var queue = new EventToastQueue();
            Assert.That(queue.Enqueue("Order complete", 0f), Is.True);
            Assert.That(queue.Enqueue("Order complete", 0.5f), Is.False);
            queue.AddCurrency(2, 0.5f);
            queue.AddCurrency(3, 0.8f);
            Assert.That(queue.Active.Single(entry => entry.Text.EndsWith("coins")).Text,
                Is.EqualTo("+5 coins"));
            queue.Prune(4f);
            Assert.That(queue.Active, Is.Empty);
            queue.Enqueue("One", 4f);
            queue.Enqueue("Two", 4f);
            queue.Enqueue("Three", 4f);
            queue.Enqueue("Four", 4f);
            Assert.That(queue.Active.Count, Is.EqualTo(3));
            queue.Prune(8f);
            Assert.That(queue.Active.Single().Text, Is.EqualTo("Four"));
        }
    }

    public sealed class DemoEscapePriorityTests
    {
        [Test]
        public void Escape_UsesOnePriorityAcrossModalPanelToolAndSelection()
        {
            Assert.That(DemoEscapePriority.Choose(true, false, true, true, true),
                Is.EqualTo(DemoEscapeAction.DismissModal));
            Assert.That(DemoEscapePriority.Choose(false, true, true, true, true),
                Is.EqualTo(DemoEscapeAction.CloseSystem));
            Assert.That(DemoEscapePriority.Choose(false, false, true, true, true),
                Is.EqualTo(DemoEscapeAction.ClosePanel));
            Assert.That(DemoEscapePriority.Choose(false, false, false, true, true),
                Is.EqualTo(DemoEscapeAction.CancelTool));
            Assert.That(DemoEscapePriority.Choose(false, false, false, false, true),
                Is.EqualTo(DemoEscapeAction.ClearSelection));
            Assert.That(DemoEscapePriority.Choose(false, false, false, false, false),
                Is.EqualTo(DemoEscapeAction.OpenSystem));
        }
    }

    public sealed class AsymmetricMirrorTestPlacementBehavior :
        MonoBehaviour, IBuildingPlacementBehavior, IBuildingPortPreviewProvider
    {
        private static readonly IReadOnlyList<BuildingPortPreview> Ports =
            new[]
            {
                new BuildingPortPreview(BuildingPortKind.Output,
                    new Vector2(0.2f, 0.36f), BuildingRotation.Degrees0)
            };

        public IReadOnlyList<BuildingPortPreview> PortPreviews => Ports;

        public bool CanPlace(Vector2Int anchorCell, Vector2Int footprint,
            BuildingRotation rotation) => true;

        public void InitializePlacedBuilding(GameObject buildingObject,
            BuildingPlacement placement)
        {
        }
    }
}
