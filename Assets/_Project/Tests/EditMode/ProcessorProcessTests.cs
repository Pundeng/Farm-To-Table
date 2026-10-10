using System.Collections.Generic;
using System.Linq;
using CozyFoodFactory.Buildings;
using CozyFoodFactory.CameraControl;
using CozyFoodFactory.Food;
using CozyFoodFactory.Logistics;
using NUnit.Framework;
using UnityEngine;
using CozyFoodFactory.Grid;

namespace CozyFoodFactory.Tests.EditMode
{
    public sealed class ProcessorProcessTests
    {
        [TestCase(CookingProperty.Heat)]
        [TestCase(CookingProperty.Water)]
        [TestCase(CookingProperty.Time)]
        [TestCase(CookingProperty.Cold)]
        public void GeneratedSourcesResolveLocallyAndSurviveConnectionReload(CookingProperty property)
        {
            PropertySourceSetup[] patch = GeneratedPatch(property);
            var root = new GameObject("Generated source lookup test");
            try
            {
                var grid = root.AddComponent<GridSystem>();
                var occupancy = new GridOccupancy();
                // The first source belongs to another location, as in the growing world list.
                var supply = new PropertySupplyPlayMode(grid, null, occupancy, root.transform,
                    new[] { new PropertySourceSetup { cell = Vector2Int.zero } });
                supply.AddSources(patch);
                Assert.That(supply.CaptureSources().Length, Is.EqualTo(patch.Length + 1));
                foreach (PropertySourceSetup source in patch)
                {
                    Assert.That(supply.TryGetSourceStatus(source.cell, out PropertySupplyStatus status), Is.True);
                    Assert.That(status.Capacity, Is.EqualTo(source.capacity));
                    Assert.That(occupancy.CanPlace(source.cell, Vector2Int.one, BuildingRotation.Degrees0), Is.False);
                }
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>().Length, Is.GreaterThanOrEqualTo(patch.Length));
                Vector2Int selected = patch[0].cell;
                Vector2Int collector = selected + Vector2Int.left;
                Vector2Int pipe = collector + Vector2Int.left;
                Assert.That(supply.TryPlaceCollector(selected), Is.False); // Source cell is occupied.
                Assert.That(supply.TryPlaceCollector(selected + new Vector2Int(-3, 0)), Is.False);
                Assert.That(supply.TryPlaceCollector(collector, Vector2Int.zero), Is.False);
                Assert.That(supply.TryPlaceCollector(collector), Is.True);
                Assert.That(supply.PreviewPipePath(new[] { pipe, pipe + Vector2Int.left }), Is.All.EqualTo(true));
                Assert.That(supply.TryPlacePipe(pipe), Is.True);
                supply.RegisterProcessorPort(pipe + Vector2Int.left, pipe);
                Assert.That(supply.TryGetProcessorSupply(pipe + Vector2Int.left, out CookingProperty supplied), Is.True);
                Assert.That(supplied, Is.EqualTo(property));

                var saved = JsonUtility.FromJson<FactoryWorldData>(JsonUtility.ToJson(new FactoryWorldData
                {
                    propertySources = supply.CaptureSources().Select(source => new SavedPropertySource(source)).ToArray(),
                    connections = supply.CaptureWorldConnections()
                }));
                // Reuse the normal reconstruction path twice to check reset releases all reservations.
                for (int reload = 0; reload < 2; reload++)
                {
                    supply.ResetSources(saved.propertySources.Select(source => source.ToSetup()));
                    supply.RestoreWorldConnections(saved.connections);
                    supply.RegisterProcessorPort(pipe + Vector2Int.left, pipe);
                    Assert.That(supply.TryGetProcessorSupply(pipe + Vector2Int.left, out supplied), Is.True);
                    Assert.That(supplied, Is.EqualTo(property));
                    Assert.That(supply.CaptureSources().Select(source => source.cell).Distinct().Count(), Is.EqualTo(patch.Length + 1));
                    Assert.That(supply.CaptureWorldConnections().All(connection =>
                        new Vector2Int(connection.sourceX, connection.sourceY) == selected && connection.property == property), Is.True);
                    Assert.That(supply.TryRemoveConnection(collector), Is.True);
                    Assert.That(supply.TryGetProcessorSupply(pipe + Vector2Int.left, out _), Is.False);
                    Assert.That(supply.TryPlaceCollector(collector), Is.True);
                    Assert.That(supply.TryGetProcessorSupply(pipe + Vector2Int.left, out _), Is.True);
                    Assert.That(supply.CaptureSources().Single(source => source.cell == selected).property, Is.EqualTo(property));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(CookingProperty.Heat)]
        [TestCase(CookingProperty.Water)]
        [TestCase(CookingProperty.Time)]
        [TestCase(CookingProperty.Cold)]
        public void GeneratedAndAuthoredClusterCellsUseTheSamePlacementPredicates(CookingProperty property)
        {
            PropertySourceSetup[] patch = GeneratedPatch(property);
            var authoredRoot = new GameObject("Authored cluster test");
            var generatedRoot = new GameObject("Generated cluster test");
            try
            {
                var authored = new PropertySupplyPlayMode(authoredRoot.AddComponent<GridSystem>(), null,
                    new GridOccupancy(), authoredRoot.transform, patch);
                var generated = new PropertySupplyPlayMode(generatedRoot.AddComponent<GridSystem>(), null,
                    new GridOccupancy(), generatedRoot.transform, System.Array.Empty<PropertySourceSetup>());
                generated.AddSources(patch);
                foreach (PropertySourceSetup source in patch)
                foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                {
                    Vector2Int cell = source.cell + direction;
                    bool expected = authored.TryPlaceCollector(cell, source.cell);
                    Assert.That(generated.TryPlaceCollector(cell), Is.EqualTo(expected), $"Source {source.cell}, cell {cell}");
                    if (expected)
                    {
                        Assert.That(authored.TryRemoveConnection(cell), Is.True);
                        Assert.That(generated.TryRemoveConnection(cell), Is.True);
                    }
                }
                Assert.That(generated.CaptureSources().Length, Is.EqualTo(patch.Length));
            }
            finally { Object.DestroyImmediate(authoredRoot); Object.DestroyImmediate(generatedRoot); }
        }

        private static PropertySourceSetup[] GeneratedPatch(CookingProperty property)
        {
            var generator = new PropertyWorldGenerator(new TerritoryWorldSettings());
            return Enumerable.Range(0, 1024)
                .Select(seed => generator.Generate(seed, new Vector2Int(4, -3)))
                .First(items => items[0].property == property);
        }

        [Test]
        public void RevealedSourceRequiresBuildableTerritoryAndAvailableProperty()
        {
            var settings = new TerritoryWorldSettings();
            var territories = new TerritorySystem(settings).Initialize();
            PropertySourceSetup[] patch = new PropertyWorldGenerator(settings).Generate(42, new Vector2Int(2, 0));
            var root = new GameObject("Property territory test");
            bool unlocked = true;
            try
            {
                var supply = new PropertySupplyPlayMode(root.AddComponent<GridSystem>(), null,
                    new GridOccupancy(), root.transform, patch, sourceAvailable: _ => unlocked,
                    cellOwned: territories.IsBuildableCell);
                Vector2Int collector = patch[0].cell + Vector2Int.left;
                Assert.That(supply.TryGetSourceStatus(patch[0].cell, out _), Is.True);
                Assert.That(supply.TryPlaceCollector(collector), Is.False);
                var wallet = new MarketInventory();
                wallet.RecordDelivery(new FoodItemData("test-currency", FoodItemKind.RawIngredient, 100));
                Assert.That(territories.TryPurchase(new Vector2Int(2, 0), wallet), Is.True);
                unlocked = false;
                Assert.That(supply.TryPlaceCollector(collector), Is.False);
                unlocked = true;
                Assert.That(supply.TryPlaceCollector(collector), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void MachineAnimationDecisions_ReactOnlyToNewOutputOrHarvest()
        {
            Assert.IsFalse(MachineAnimationDecisions.OutputCreated(false, false, true));
            Assert.IsTrue(MachineAnimationDecisions.OutputCreated(true, false, true));
            Assert.IsFalse(MachineAnimationDecisions.OutputCreated(true, true, true));
            Assert.IsFalse(MachineAnimationDecisions.Harvested(false, 0, 1));
            Assert.IsTrue(MachineAnimationDecisions.Harvested(true, 1, 2));
            Assert.IsFalse(MachineAnimationDecisions.Harvested(true, 2, 1));
        }

        [Test]
        public void MachineAnimationDecisions_PausesBlockedMotionAndHidesFarDetail()
        {
            Assert.IsTrue(MachineAnimationDecisions.Processing(
                ProcessorState.Processing, true));
            Assert.IsFalse(MachineAnimationDecisions.Processing(
                ProcessorState.Processing, false));
            Assert.IsFalse(MachineAnimationDecisions.Cutting(
                CutterState.Processing, false));
            Assert.IsFalse(MachineAnimationDecisions.ShowMotion(
                WorldInformationLevel.Far, false));
            Assert.IsTrue(MachineAnimationDecisions.ShowMotion(
                WorldInformationLevel.Far, true));
        }

        private static readonly FoodItemData Apple = new("Apple", FoodItemKind.RawIngredient);
        private static readonly FoodItemData DriedApple =
            new("Dried Apple", FoodItemKind.ProcessedFood);

        [Test]
        public void PropertyVisuals_MissingAndPartialArtworkKeepAllConnectionsVisible()
        {
            var texture = new Texture2D(2, 2);
            var artwork = Sprite.Create(texture, new Rect(0, 0, 2, 2),
                new Vector2(0.5f, 0.5f));
            var missingArtwork = Sprite.Create(texture, new Rect(0, 0, 2, 2),
                new Vector2(0.5f, 0.5f));
            Object.DestroyImmediate(missingArtwork);
            try
            {
                var configurations = new[]
                {
                    (PropertyVisualDefinition)null,
                    new PropertyVisualDefinition(),
                    new PropertyVisualDefinition { source = missingArtwork },
                    new PropertyVisualDefinition { source = artwork },
                    new PropertyVisualDefinition
                    {
                        source = artwork, collector = artwork, pipe = artwork
                    }
                };
                int[] artworkCounts = { 0, 0, 0, 1, 3 };
                for (int index = 0; index < configurations.Length; index++)
                {
                    var root = new GameObject("Property visual test");
                    try
                    {
                        var grid = root.AddComponent<GridSystem>();
                        var occupancy = new GridOccupancy();
                        var supply = new PropertySupplyPlayMode(grid, null,
                            occupancy, root.transform,
                            new[] { new PropertySourceSetup
                            {
                                cell = Vector2Int.zero,
                                property = CookingProperty.Heat,
                                capacity = 2
                            } }, true, configurations[index]);
                        Assert.That(supply.TryPlaceCollector(Vector2Int.right,
                            Vector2Int.zero), Is.True);
                        Assert.That(supply.TryPlacePipe(new Vector2Int(2, 0)), Is.True);
                        int visible = 0;
                        int assignedArtwork = 0;
                        foreach (SpriteRenderer renderer in
                            root.GetComponentsInChildren<SpriteRenderer>())
                        {
                            if (renderer.sortingOrder != 12) continue;
                            Assert.That(renderer.sprite, Is.Not.Null);
                            visible++;
                            if (renderer.sprite == artwork) assignedArtwork++;
                        }
                        Assert.That(visible, Is.EqualTo(3));
                        Assert.That(assignedArtwork,
                            Is.EqualTo(artworkCounts[index]));
                        Assert.That(supply.CaptureWorldConnections().Length,
                            Is.EqualTo(2));
                    }
                    finally { Object.DestroyImmediate(root); }
                }
            }
            finally
            {
                Object.DestroyImmediate(artwork);
                Object.DestroyImmediate(texture);
            }
        }

        [TestCase(CookingProperty.Heat)]
        [TestCase(CookingProperty.Water)]
        public void PipePathPreview_KeepsStartingOwnerBesideForeignPipe(CookingProperty otherProperty)
        {
            var root = new GameObject("Adjacent pipe preview test");
            try
            {
                var grid = root.AddComponent<GridSystem>();
                var occupancy = new GridOccupancy();
                var owner = Vector2Int.zero;
                var other = new Vector2Int(5, 2);
                var supply = new PropertySupplyPlayMode(grid, null, occupancy,
                    root.transform, new[]
                    {
                        new PropertySourceSetup { cell = owner, property = CookingProperty.Heat, capacity = 1 },
                        new PropertySourceSetup { cell = other, property = otherProperty, capacity = 3 }
                    });
                Assert.That(supply.TryPlaceCollector(Vector2Int.right, owner), Is.True);
                Assert.That(supply.TryPlacePipe(new Vector2Int(2, 0)), Is.True);
                Assert.That(supply.TryPlaceCollector(new Vector2Int(4, 2), other), Is.True);
                Assert.That(supply.TryPlacePipe(new Vector2Int(3, 2)), Is.True);
                Assert.That(supply.TryPlacePipe(new Vector2Int(3, 1)), Is.True);
                CollectionAssert.AreEqual(new[] { false, true }, supply.PreviewPipePath(new[]
                {
                    new Vector2Int(2, 0), new Vector2Int(3, 0)
                }));
                Assert.That(occupancy.TryGetBuilding(new Vector2Int(3, 0), out _), Is.False,
                    "Preview must not mutate the live network or occupancy.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void PipePathPreview_ValidatesCornerAndDoesNotPlaceUntilCommit()
        {
            var root = new GameObject("Pipe path preview test");
            try
            {
                var grid = root.AddComponent<GridSystem>();
                var occupancy = new GridOccupancy();
                var source = new Vector2Int(0, 0);
                var supply = new PropertySupplyPlayMode(grid, null, occupancy,
                    root.transform, new[] { new PropertySourceSetup
                    {
                        cell = source, property = CookingProperty.Heat, capacity = 2
                    } });
                Assert.That(supply.TryPlaceCollector(Vector2Int.right, source), Is.True);
                var path = new[]
                {
                    new Vector2Int(2, 0), new Vector2Int(3, 0),
                    new Vector2Int(3, 1)
                };
                CollectionAssert.AreEqual(new[] { true, true, true },
                    supply.PreviewPipePath(path));
                Assert.That(occupancy.TryGetBuilding(path[0], out _), Is.False);
                foreach (Vector2Int cell in path)
                    Assert.That(supply.TryPlacePipe(cell), Is.True);
                CollectionAssert.AreEqual(new[] { false, false, false },
                    supply.PreviewPipePath(path));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PipePathPreview_MarksBlockedAndDisconnectedSegmentsInvalid()
        {
            var root = new GameObject("Blocked pipe preview test");
            try
            {
                var grid = root.AddComponent<GridSystem>();
                var occupancy = new GridOccupancy();
                var supply = new PropertySupplyPlayMode(grid, null, occupancy,
                    root.transform, new[] { new PropertySourceSetup
                    {
                        cell = Vector2Int.zero, property = CookingProperty.Heat,
                        capacity = 2
                    } });
                Assert.That(supply.TryPlaceCollector(Vector2Int.right,
                    Vector2Int.zero), Is.True);
                Assert.That(occupancy.TryRegister("Block", new Vector2Int(3, 0),
                    Vector2Int.one, BuildingRotation.Degrees0, out _), Is.True);
                CollectionAssert.AreEqual(new[] { true, false, false },
                    supply.PreviewPipePath(new[]
                    {
                        new Vector2Int(2, 0), new Vector2Int(3, 0),
                        new Vector2Int(4, 0)
                    }));
                Assert.That(occupancy.TryGetBuilding(new Vector2Int(2, 0),
                    out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PropertyClipboard_RejectsMergeWithAnotherSource()
        {
            var root = new GameObject("Property clipboard test");
            try
            {
                var grid = root.AddComponent<GridSystem>();
                var occupancy = new GridOccupancy();
                var supply = new PropertySupplyPlayMode(grid, null, occupancy,
                    root.transform, new[]
                    {
                        new PropertySourceSetup { cell = Vector2Int.zero,
                            property = CookingProperty.Heat, capacity = 2 },
                        new PropertySourceSetup { cell = new Vector2Int(5, 0),
                            property = CookingProperty.Heat, capacity = 2 }
                    });
                Assert.That(supply.TryPlaceCollector(Vector2Int.right,
                    Vector2Int.zero), Is.True);
                Assert.That(supply.TryPlacePipe(new Vector2Int(2, 0)), Is.True);
                Assert.That(occupancy.TryGetBuilding(new Vector2Int(2, 0),
                    out BuildingPlacement selected), Is.True);
                Assert.That(supply.TryGetClipboardConnection(selected,
                    out PropertyConnection connection), Is.True);
                var items = new[] { new PropertyGroupCopyItem(connection, Vector2Int.zero) };
                Assert.That(supply.TryPlanClipboardConnections(items,
                    new Vector2Int(3, 0), new HashSet<Vector2Int>(), out _), Is.True);
                Assert.That(supply.TryPlanClipboardConnections(items,
                    new Vector2Int(3, 0), new HashSet<Vector2Int>(),
                    new[] { new Vector2Int(2, 0) }, out _), Is.False,
                    "A cut cannot rely on the Pipe it removes.");
                Assert.That(occupancy.TryGetBuilding(new Vector2Int(3, 0),
                    out _), Is.False, "Preview must leave the live occupancy unchanged.");
                Assert.That(supply.TryPlaceCollector(new Vector2Int(4, 0),
                    new Vector2Int(5, 0)), Is.True);
                Assert.That(supply.TryPlanClipboardConnections(items,
                    new Vector2Int(3, 0), new HashSet<Vector2Int>(), out _), Is.False);
                Assert.That(occupancy.TryGetBuilding(new Vector2Int(2, 0),
                    out BuildingPlacement stillSelected), Is.True);
                Assert.That(stillSelected, Is.SameAs(selected));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PropertyClipboard_RemapsCollectorToMatchingSourceAtDestination()
        {
            var root = new GameObject("Property remap test");
            try
            {
                var grid = root.AddComponent<GridSystem>();
                var occupancy = new GridOccupancy();
                var supply = new PropertySupplyPlayMode(grid, null, occupancy,
                    root.transform, new[]
                    {
                        new PropertySourceSetup { cell = Vector2Int.zero,
                            property = CookingProperty.Heat, capacity = 2 },
                        new PropertySourceSetup { cell = new Vector2Int(5, 0),
                            property = CookingProperty.Heat, capacity = 2 }
                    });
                var copied = new PropertyConnection(Vector2Int.right,
                    Vector2Int.zero, CookingProperty.Heat,
                    PropertyConnectionKind.Collector);
                var items = new[] { new PropertyGroupCopyItem(copied, Vector2Int.zero) };
                Assert.That(supply.TryPlanClipboardConnections(items,
                    new Vector2Int(4, 0), new HashSet<Vector2Int>(),
                    out IReadOnlyList<PropertyGroupCopyItem> plan), Is.True);
                Assert.That(plan[0].Connection.SourceCell,
                    Is.EqualTo(new Vector2Int(5, 0)));
                Assert.That(supply.TryPlaceClipboardConnection(plan[0],
                    new Vector2Int(4, 0)), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void PropertyDependencyCheck_RejectsRemovingCollectorUnderLivePipe()
        {
            var root = new GameObject("Property dependency test");
            try
            {
                var grid = root.AddComponent<GridSystem>();
                var supply = new PropertySupplyPlayMode(grid, null,
                    new GridOccupancy(), root.transform, new[]
                    {
                        new PropertySourceSetup { cell = Vector2Int.zero,
                            property = CookingProperty.Heat, capacity = 2 }
                    });
                Assert.That(supply.TryPlaceCollector(Vector2Int.right,
                    Vector2Int.zero), Is.True);
                Assert.That(supply.TryPlacePipe(new Vector2Int(2, 0)), Is.True);
                Assert.That(supply.CanRemoveWithoutBreakingDependents(
                    new[] { Vector2Int.right }), Is.False);
                Assert.That(supply.CanRemoveWithoutBreakingDependents(
                    new[] { Vector2Int.right, new Vector2Int(2, 0) }), Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void PlacedProcessor_RegistersDemandAndAcceptsFarmAppleWhenAirPipeConnects()
        {
            var root = new GameObject("Property test root");
            var coordinatorObject = new GameObject("Transport coordinator");
            var processorObject = new GameObject("Processor");
            try
            {
                var grid = root.AddComponent<GridSystem>();
                var occupancy = new GridOccupancy();
                var sourceCell = new Vector2Int(-6, -4);
                var sourceSetup = new PropertySourceSetup
                {
                    property = CookingProperty.Cold,
                    cell = sourceCell,
                    capacity = 4
                };
                var supply = new PropertySupplyPlayMode(grid, null, occupancy,
                    root.transform, new[] { sourceSetup });
                Assert.That(supply.IsVisible, Is.False);
                var coordinator = coordinatorObject.AddComponent<BeltTransportCoordinator>();
                var processor = processorObject.AddComponent<Processor>();
                var anchor = new Vector2Int(-3, -3);
                var placement = new BuildingPlacement(nameof(Processor), anchor,
                    ProcessorPortLayout.Footprint, BuildingRotation.Degrees0,
                    new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.one });
                var farmApple = new FoodItemData("apple", FoodItemKind.RawIngredient);
                var catalog = new ProcessingRecipeCatalog(new[]
                {
                    new ProcessingRecipe(farmApple, CookingProperty.Cold, DriedApple)
                });
                processor.Initialize(placement, coordinator, supply, catalog, 1f);

                Assert.That(processor.PropertyCell, Is.EqualTo(new Vector2Int(-2, -2)));
                Assert.That(processor.InputCell, Is.EqualTo(new Vector2Int(-3, -2)));
                Assert.That(processor.OutputCell, Is.EqualTo(new Vector2Int(-3, -1)));
                Assert.That(processor.SupplyMessage, Does.Contain("(-2, -3)"));
                Assert.That(processor.CanAcceptItem(farmApple, GridDirection.East), Is.False);
                Assert.That(supply.TryGetSourceStatus(sourceCell,
                    out PropertySupplyStatus disconnected), Is.True);
                Assert.That(disconnected.ConnectedConsumers, Is.Zero);

                Assert.That(supply.TryPlacePipe(new Vector2Int(0, -4)), Is.False);
                Assert.That(supply.Message, Does.Contain("must touch"));

                Assert.That(supply.TryPlaceCollector(new Vector2Int(-5, -4), sourceCell), Is.True);
                foreach (Vector2Int pipe in new[]
                {
                    new Vector2Int(-4, -4), new Vector2Int(-3, -4),
                    new Vector2Int(-2, -4), new Vector2Int(-2, -3)
                })
                {
                    Assert.That(supply.TryPlacePipe(pipe), Is.True, $"Pipe at {pipe}");
                }

                Assert.That(supply.TryGetSourceStatus(sourceCell,
                    out PropertySupplyStatus connected), Is.True);
                Assert.That(connected.ConnectedConsumers, Is.EqualTo(1));
                Assert.That(connected.ConnectedDemand, Is.EqualTo(1));
                Assert.That(connected.AvailableCapacity, Is.EqualTo(3));
                Assert.That(supply.TryGetProcessorSupply(processor.PropertyCell,
                    out CookingProperty property), Is.True);
                Assert.That(property, Is.EqualTo(CookingProperty.Cold));
                supply.TogglePanel();
                Assert.That(supply.IsVisible, Is.True);
                supply.TogglePanel();
                Assert.That(supply.IsVisible, Is.False);
                Assert.That(supply.TryGetProcessorSupply(processor.PropertyCell,
                    out property), Is.True);
                Assert.That(processor.SupplyMessage, Does.Contain("Cold supplied"));
                Assert.That(processor.TryAcceptItem(farmApple, GridDirection.East), Is.True);
                Assert.That(processor.State, Is.EqualTo(ProcessorState.Processing));

                Assert.That(supply.TryRemoveConnection(processor.PropertyCell), Is.False);
                Assert.That(supply.Message, Does.Contain("automatic"));
                Assert.That(supply.TryGetProcessorSupply(processor.PropertyCell, out _), Is.True);

                Assert.That(supply.TryRemoveConnection(new Vector2Int(-3, -4)), Is.True);
                Assert.That(supply.TryGetProcessorSupply(processor.PropertyCell, out _), Is.False);
                Assert.That(processor.SupplyMessage, Does.Contain("disconnected"));
                Assert.That(processor.ProcessingStateMessage, Does.Contain("paused"));
                Assert.That(supply.TryGetSourceStatus(sourceCell,
                    out PropertySupplyStatus afterDisconnection), Is.True);
                Assert.That(afterDisconnection.ConnectedConsumers, Is.Zero);
                Assert.That(supply.TryPlacePipe(new Vector2Int(-3, -4)), Is.True);
                Assert.That(supply.TryGetProcessorSupply(processor.PropertyCell, out _), Is.True);

                Assert.That(supply.TryRemoveConnection(new Vector2Int(-5, -4)), Is.True);
                Assert.That(supply.TryGetProcessorSupply(processor.PropertyCell, out _), Is.False);
                Assert.That(supply.TryPlaceCollector(new Vector2Int(-5, -4), sourceCell), Is.True);
                Assert.That(supply.TryGetProcessorSupply(processor.PropertyCell, out _), Is.True);
                Assert.That(processor.ProcessingStateMessage, Is.EqualTo("Processing"));

                Vector2Int propertyCell = processor.PropertyCell;
                supply.UnregisterProcessorPort(propertyCell);
                Assert.That(supply.TryGetProcessorSupply(propertyCell, out _), Is.False);
                Assert.That(supply.TryGetSourceStatus(sourceCell,
                    out PropertySupplyStatus afterRemoval), Is.True);
                Assert.That(afterRemoval.ConnectedConsumers, Is.Zero);
                supply.UnregisterProcessorPort(propertyCell);
                Assert.That(supply.TryGetSourceStatus(sourceCell,
                    out PropertySupplyStatus afterRepeatedRemoval), Is.True);
                Assert.That(afterRepeatedRemoval.ConnectedConsumers, Is.Zero);
                Object.DestroyImmediate(processorObject);
            }
            finally
            {
                if (processorObject != null)
                {
                    Object.DestroyImmediate(processorObject);
                }
                Object.DestroyImmediate(coordinatorObject);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UniqueRecipe_ProcessesAndHoldsOutputUntilTaken()
        {
            var process = CreateProcess(new ProcessingRecipe(Apple,
                CookingProperty.Cold, DriedApple));
            Assert.That(process.TryAccept(Apple, CookingProperty.Cold, true), Is.True);
            Assert.That(process.State, Is.EqualTo(ProcessorState.Processing));
            Assert.That(process.Advance(0.5f, true), Is.False);
            Assert.That(process.Advance(0.5f, true), Is.True);
            Assert.That(process.State, Is.EqualTo(ProcessorState.WaitingForOutput));
            Assert.That(process.PeekOutput(), Is.EqualTo(DriedApple));
            Assert.That(process.TryAccept(Apple, CookingProperty.Cold, true), Is.False);
            Assert.That(process.TryTakeOutput(out FoodItemData output), Is.True);
            Assert.That(output, Is.EqualTo(DriedApple));
            Assert.That(process.State, Is.EqualTo(ProcessorState.Idle));
        }

        [Test]
        public void InvalidOrAmbiguousCombination_DoesNotAcceptFood()
        {
            var valid = new ProcessingRecipe(Apple, CookingProperty.Cold, DriedApple);
            var catalog = new ProcessingRecipeCatalog(new[] { valid, valid });
            Assert.That(catalog.Find(Apple, CookingProperty.Cold, out _),
                Is.EqualTo(ProcessingRecipeMatch.Ambiguous));
            var process = new ProcessorProcess(catalog, 1f);
            Assert.That(process.TryAccept(Apple, CookingProperty.Cold, true), Is.False);
            Assert.That(process.State, Is.EqualTo(ProcessorState.Idle));

            process = CreateProcess(valid);
            Assert.That(process.TryAccept(Apple, CookingProperty.Heat, true), Is.False);
            Assert.That(process.TryAccept(Apple, CookingProperty.Cold, false), Is.False);
            Assert.That(process.State, Is.EqualTo(ProcessorState.Idle));
        }

        [Test]
        public void LostSupply_PausesProcessingWithoutLosingInput()
        {
            var process = CreateProcess(new ProcessingRecipe(Apple,
                CookingProperty.Cold, DriedApple));
            Assert.That(process.TryAccept(Apple, CookingProperty.Cold, true), Is.True);
            Assert.That(process.Advance(0.5f, true), Is.False);
            Assert.That(process.Advance(5f, false), Is.False);
            Assert.That(process.State, Is.EqualTo(ProcessorState.Processing));
            Assert.That(process.Advance(0.5f, true), Is.True);
            Assert.That(process.PeekOutput(), Is.EqualTo(DriedApple));
        }

        [Test]
        public void BeltFlow_RetainsInvalidInputAndBlockedFinishedOutput()
        {
            var process = CreateProcess(new ProcessingRecipe(Apple,
                CookingProperty.Cold, DriedApple));
            var system = new BeltTransportSystem(1f);
            var receiver = new ProcessReceiver(process);
            var source = new ProcessSource(process);
            system.RegisterInputReceiver(receiver);
            system.RegisterOutputSource(source);
            Vector2Int inputBeltCell = ProcessorPortLayout.GetFoodInputOutsideCell(
                Vector2Int.zero, BuildingRotation.Degrees0);
            BeltCell input = system.AddBelt(inputBeltCell, GridDirection.East);
            var invalid = new FoodItemData("Tomato", FoodItemKind.RawIngredient);
            input.TryAccept(invalid, GridDirection.East);
            system.Advance(1f);
            Assert.That(input.Item.Item, Is.SameAs(invalid));
            Assert.That(process.State, Is.EqualTo(ProcessorState.Idle));

            system.RemoveBelt(input);
            input = system.AddBelt(inputBeltCell, GridDirection.East);
            input.TryAccept(Apple, GridDirection.East);
            system.Advance(1f);
            Assert.That(input.HasItem, Is.False);
            Assert.That(process.Advance(1f, true), Is.True);
            system.Advance(0f);
            Assert.That(process.State, Is.EqualTo(ProcessorState.WaitingForOutput));
            Assert.That(process.PeekOutput(), Is.EqualTo(DriedApple));

            BeltCell output = system.AddBelt(
                ProcessorPortLayout.GetFoodOutputOutsideCell(Vector2Int.zero,
                    BuildingRotation.Degrees0), GridDirection.North);
            system.Advance(0f);
            Assert.That(output.Item.Item, Is.EqualTo(DriedApple));
            Assert.That(process.State, Is.EqualTo(ProcessorState.Idle));
        }

        private static ProcessorProcess CreateProcess(params ProcessingRecipe[] recipes)
        {
            return new ProcessorProcess(new ProcessingRecipeCatalog(recipes), 1f);
        }

        private sealed class ProcessReceiver : IItemInputReceiver
        {
            private readonly ProcessorProcess process;

            public ProcessReceiver(ProcessorProcess process) { this.process = process; }
            public Vector2Int InputCell => ProcessorPortLayout.GetChamberCell(
                Vector2Int.zero, BuildingRotation.Degrees0);
            public bool AllowsConcurrentInput => false;
            public bool CanAcceptItem(ITransportItem item, GridDirection direction) =>
                direction == GridDirection.East && item is FoodItemData food &&
                process.Evaluate(food, CookingProperty.Cold, true) ==
                ProcessingRecipeMatch.Unique;
            public bool TryAcceptItem(ITransportItem item, GridDirection direction) =>
                CanAcceptItem(item, direction) &&
                process.TryAccept((FoodItemData)item, CookingProperty.Cold, true);
        }

        private sealed class ProcessSource : IItemOutputSource
        {
            private readonly ProcessorProcess process;

            public ProcessSource(ProcessorProcess process) { this.process = process; }
            public Vector2Int OutputCell => ProcessorPortLayout.GetFoodOutputOutsideCell(
                Vector2Int.zero, BuildingRotation.Degrees0);
            public GridDirection OutputDirection => GridDirection.North;
            public bool HasOutput => process.HasOutput;
            public ITransportItem PeekOutput() => process.PeekOutput();
            public bool TryTakeOutput(out ITransportItem item)
            {
                bool taken = process.TryTakeOutput(out FoodItemData food);
                item = food;
                return taken;
            }
        }
    }
}
