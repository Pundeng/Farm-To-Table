using System;
using System.IO;
using System.Reflection;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.CameraControl;
using CozyFoodFactory.Food;
using CozyFoodFactory.Logistics;
using NUnit.Framework;
using UnityEngine;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class Ux06FactoryScaleTests
    {
        [Test]
        public void BlueprintLibrary_PersistsNamedLayoutAndPreservesIncompatibleFile()
        {
            string path = Path.Combine(Path.GetTempPath(),
                "cozy-blueprint-test-" + Guid.NewGuid().ToString("N") + ".json");
            var prefab = new GameObject("Belt prefab");
            try
            {
                var option = new BuildingPlacementOption();
                BuildingDefinition definition = option.Definition;
                typeof(BuildingDefinition).GetField("id", BindingFlags.NonPublic |
                    BindingFlags.Instance)?.SetValue(definition, "Belt");
                typeof(BuildingDefinition).GetField("instancePrefab",
                    BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(
                        definition, prefab);
                var copy = new BuildingGroupCopy(new[]
                {
                    new BuildingGroupCopyItem(option, new Vector2Int(5, 6),
                        BuildingRotation.Degrees90, outputMask:
                            BeltCell.Bit(GridDirection.East) |
                            BeltCell.Bit(GridDirection.North))
                });
                var library = new BlueprintLibrary(path);
                Assert.That(library.TryAdd("Line", copy, out string error), Is.True, error);
                var restored = new BlueprintLibrary(path);
                Assert.That(restored.Records.Count, Is.EqualTo(1));
                Assert.That(restored.Records[0].name, Is.EqualTo("Line"));
                Assert.That(BlueprintLibrary.TryResolve(restored.Records[0],
                    new[] { option }, out BuildingGroupCopy layout, out error),
                    Is.True, error);
                Assert.That(layout.Items[0].CropId, Is.Null);
                Assert.That(layout.Items[0].Rotation,
                    Is.EqualTo(BuildingRotation.Degrees90));
                Assert.That(layout.Items[0].OutputMask,
                    Is.EqualTo(BeltCell.Bit(GridDirection.East) |
                        BeltCell.Bit(GridDirection.North)));
                Assert.That(layout.RotateClockwise().Items[0].OutputMask,
                    Is.EqualTo(BeltCell.Bit(GridDirection.South) |
                        BeltCell.Bit(GridDirection.East)));
                Assert.That(layout.TryMirrorHorizontal(out BuildingGroupCopy mirrored),
                    Is.True);
                Assert.That(mirrored.Items[0].OutputMask,
                    Is.EqualTo(BeltCell.Bit(GridDirection.West) |
                        BeltCell.Bit(GridDirection.North)));
                // A fresh library instance models reopening after a restart.
                var reopened = new BlueprintLibrary(path);
                Assert.That(reopened.Records.Count, Is.EqualTo(1));
                Assert.That(BlueprintLibrary.TryResolve(reopened.Records[0],
                    new[] { option }, out _, out error), Is.True, error);
                Assert.That(restored.TryDuplicate(restored.Records[0].id, out error),
                    Is.True, error);
                Assert.That(restored.Records.Count, Is.EqualTo(2));
                File.WriteAllText(path, "{\"version\":99,\"records\":[]}");
                var incompatible = new BlueprintLibrary(path);
                Assert.That(incompatible.LoadError, Is.Not.Null);
                Assert.That(incompatible.TryAdd("Another", copy, out _), Is.False);
                Assert.That(File.ReadAllText(path), Does.Contain("\"version\":99"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefab);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            }
        }

        [Test]
        public void BlueprintLibrary_InvalidRecordRemainsVisibleButCannotBePlaced()
        {
            var option = new BuildingPlacementOption();
            var record = new BlueprintRecord
            {
                id = "invalid", name = "Unavailable",
                buildings = new[] { new BlueprintBuilding
                {
                    definitionId = "MissingBuilding", x = 0, y = 0
                } }
            };
            Assert.That(BlueprintLibrary.TryResolve(record, new[] { option },
                out _, out string error), Is.False);
            Assert.That(error, Does.Contain("Missing building"));
            Assert.That(record.name, Is.EqualTo("Unavailable"));
        }

        [Test]
        public void BlueprintLibrary_EmptyCropIdOnNonFarmBuildingIsNoCrop()
        {
            var option = new BuildingPlacementOption();
            var prefab = new GameObject("Belt prefab");
            try
            {
                typeof(BuildingDefinition).GetField("id", BindingFlags.NonPublic |
                    BindingFlags.Instance)?.SetValue(option.Definition, "Belt");
                typeof(BuildingDefinition).GetField("instancePrefab",
                    BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(
                        option.Definition, prefab);
                var record = new BlueprintRecord
                {
                    id = "empty-crop", name = "Belt",
                    buildings = new[] { new BlueprintBuilding
                    {
                        definitionId = "Belt", cropId = ""
                    } }
                };
                Assert.That(BlueprintLibrary.TryResolve(record, new[] { option },
                    out BuildingGroupCopy layout, out string error), Is.True, error);
                Assert.That(layout.Items[0].CropId, Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(prefab); }
        }

        [Test]
        public void BlueprintTextFocus_BlocksGameplayShortcutsOnlyWhileFocused()
        {
            Assert.That(BuildingPlacementController.ShouldBlockGameplayKeyboardInput(
                true, true, BuildingPlacementController.DemoPanel.Build, true), Is.True);
            Assert.That(BuildingPlacementController.ShouldBlockGameplayKeyboardInput(
                true, true, BuildingPlacementController.DemoPanel.Build, false), Is.False);
            Assert.That(BuildingPlacementController.ShouldBlockGameplayKeyboardInput(
                true, true, BuildingPlacementController.DemoPanel.None, true), Is.False);
        }

        [Test]
        public void History_FailedReplayKeepsStackAndNewActionClearsRedo()
        {
            var empty = ConstructionLayout.FromWorld(new FactoryWorldData());
            var withBelt = ConstructionLayout.FromWorld(new FactoryWorldData
            {
                buildings = new[] { new SavedBuilding
                {
                    definitionId = "Belt", x = 2, y = 3,
                    rotation = BuildingRotation.Degrees90, belt = new SavedBelt()
                } }
            });
            var history = new ConstructionHistory();
            history.Record(empty.Difference(withBelt));
            Assert.That(history.TryUndo((_, _) => false), Is.False);
            Assert.That(history.UndoCount, Is.EqualTo(1));
            Assert.That(history.RedoCount, Is.Zero);
            Assert.That(history.TryUndo((_, _) => true), Is.True);
            Assert.That(history.RedoCount, Is.EqualTo(1));
            history.Record(empty.Difference(withBelt));
            Assert.That(history.RedoCount, Is.Zero);
        }

        [Test]
        public void History_GroupDeletionRestoresAndRemovesAllSelectedBuildingsInOneStep()
        {
            var populated = ConstructionLayout.FromWorld(new FactoryWorldData
            {
                buildings = new[]
                {
                    new SavedBuilding { definitionId = nameof(Belt), x = 1, y = 2,
                        belt = new SavedBelt() },
                    new SavedBuilding { definitionId = nameof(Belt), x = 2, y = 2,
                        belt = new SavedBelt() },
                    new SavedBuilding { definitionId = nameof(Belt), x = 3, y = 2,
                        belt = new SavedBelt() }
                }
            });
            var empty = ConstructionLayout.FromWorld(new FactoryWorldData());
            ConstructionChange deletion = populated.Difference(empty);
            var history = new ConstructionHistory();
            ConstructionLayout current = empty;
            history.Record(deletion);

            Assert.That(deletion.Before.Buildings, Has.Length.EqualTo(3));
            Assert.That(deletion.After.Buildings, Is.Empty);
            Assert.That(history.UndoCount, Is.EqualTo(1));
            Assert.That(history.TryUndo((expected, target) =>
            {
                Assert.That(current.Buildings, Has.Length.EqualTo(0));
                Assert.That(expected.Buildings, Is.Empty);
                current = target;
                return true;
            }), Is.True);
            Assert.That(current.Buildings, Has.Length.EqualTo(3));
            Assert.That(history.RedoCount, Is.EqualTo(1));
            Assert.That(history.TryRedo((expected, target) =>
            {
                Assert.That(current.Buildings, Has.Length.EqualTo(3));
                Assert.That(expected.Buildings, Has.Length.EqualTo(3));
                current = target;
                return true;
            }), Is.True);
            Assert.That(current.Buildings, Is.Empty);
        }

        [Test]
        public void History_RecordsBeltConnectionChangeAsOneConstructionDelta()
        {
            static ConstructionLayout Layout(int mask) =>
                ConstructionLayout.FromWorld(new FactoryWorldData
                {
                    buildings = new[] { new SavedBuilding
                    {
                        definitionId = nameof(Belt), x = 2, y = 3,
                        rotation = BuildingRotation.Degrees90,
                        belt = new SavedBelt { outputMask = mask }
                    } }
                });
            ConstructionLayout straight = Layout(BeltCell.Bit(GridDirection.East));
            ConstructionLayout branch = Layout(BeltCell.Bit(GridDirection.East) |
                BeltCell.Bit(GridDirection.North));
            ConstructionChange change = straight.Difference(branch);
            Assert.That(change.IsEmpty, Is.False);
            Assert.That(change.Before.Buildings.Length, Is.EqualTo(1));
            Assert.That(change.After.Buildings[0].belt.outputMask,
                Is.EqualTo(BeltCell.Bit(GridDirection.East) |
                    BeltCell.Bit(GridDirection.North)));
        }

        [Test]
        public void Lod_HysteresisPreventsBoundaryFlicker()
        {
            var lod = new WorldInformationLod();
            Assert.That(lod.Update(7.1f), Is.EqualTo(WorldInformationLevel.Close));
            Assert.That(lod.Update(7.7f), Is.EqualTo(WorldInformationLevel.Medium));
            Assert.That(lod.Update(6.8f), Is.EqualTo(WorldInformationLevel.Medium));
            Assert.That(lod.Update(13.7f), Is.EqualTo(WorldInformationLevel.Far));
            Assert.That(lod.Update(12.8f), Is.EqualTo(WorldInformationLevel.Far));
            Assert.That(lod.Update(12.3f), Is.EqualTo(WorldInformationLevel.Medium));
            Assert.That(lod.Update(3f), Is.EqualTo(WorldInformationLevel.Close));
            Assert.That(lod.Update(20f), Is.EqualTo(WorldInformationLevel.Far));
        }

        [Test]
        public void BeltLod_ChangesRenderedItemScaleAndIdentityVisibility()
        {
            Assert.That(Belt.ItemVisualScale(WorldInformationLevel.Close, false),
                Is.GreaterThan(Belt.ItemVisualScale(WorldInformationLevel.Medium, false)));
            Assert.That(Belt.ItemVisualScale(WorldInformationLevel.Medium, false),
                Is.GreaterThan(Belt.ItemVisualScale(WorldInformationLevel.Far, false)));
            Assert.That(Belt.ShowItemIdentity(WorldInformationLevel.Close, false), Is.True);
            Assert.That(Belt.ShowItemIdentity(WorldInformationLevel.Medium, false), Is.False);
            Assert.That(Belt.ShowItemIdentity(WorldInformationLevel.Far, false), Is.False);
            Assert.That(Belt.ShowItemIdentity(WorldInformationLevel.Far, true), Is.True);
        }

        [Test]
        public void FactoryIssues_StabilizePrioritizeAndResolve()
        {
            var tracker = new FactoryIssueTracker<string>();
            var needsInput = MachineFeedbackResolver.Processor(false, false,
                false, true);
            var property = MachineFeedbackResolver.Processor(false, false,
                true, false);
            var blocked = MachineFeedbackResolver.Cutter(true, false, false, false);
            tracker.Observe("idle", needsInput, 0f);
            tracker.Observe("property", property, 0f);
            tracker.Observe("blocked", blocked, 0f);
            tracker.EndFrame(0f);
            Assert.That(tracker.Issues, Is.Empty);
            tracker.Observe("property", property, 0.8f);
            tracker.Observe("blocked", blocked, 0.8f);
            tracker.EndFrame(0.8f);
            Assert.That(tracker.Issues.Count, Is.EqualTo(1));
            Assert.That(tracker.Issues[0].Machine, Is.EqualTo("blocked"));
            tracker.Observe("property", property, 1.6f);
            tracker.Observe("blocked", blocked, 1.6f);
            tracker.EndFrame(1.6f);
            Assert.That(tracker.Issues[0].Machine, Is.EqualTo("blocked"));
            Assert.That(tracker.Issues.Count, Is.EqualTo(2));
            tracker.Observe("blocked", MachineFeedbackResolver.Cutter(
                false, false, false, true), 1.7f);
            tracker.Observe("property", property, 1.7f);
            tracker.EndFrame(1.7f);
            Assert.That(tracker.Issues.Count, Is.EqualTo(1));
            Assert.That(tracker.Issues[0].Machine, Is.EqualTo("property"));
        }
    }
}
