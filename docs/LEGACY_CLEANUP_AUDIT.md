# Legacy Cleanup Phase 1 and 2A results

**Date:** 2026-09-27. **Branch:** `chore/legacy-cleanup`.
`Demo.unity` is the only supported playable scene. No Phase 1 Demo smoke-test
result was available before Phase 2A. Unity Editor and Play Mode checks remain
manual under the project's validation rules.

## Removed

| Phase | Category | Files |
| --- | --- | --- |
| 1 | Build entry and scene | Prototype's Build Settings entry; `Prototype.unity` |
| 1 | Editor setup and assets | `PrototypeBeltTransportSetup.cs`; four rune machine prefabs; six rune objective assets |
| 1 | Isolated implementation/test | `GlyphRotator.cs`, `GlyphRotatorProcess.cs`, `GlyphRotatorPlacementBehavior.cs`, `GlyphRotatorTests.cs` |
| 2A | Rune runtime | All remaining scripts in `Scripts/Runes/`, `Scripts/Resources/`, `Scripts/Production/`, and `Scripts/Objectives/`; `IRuneInputReceiver.cs`; `IRuneOutputSource.cs` |
| 2A | Exclusive tests | `RuneDataTests.cs`, `RuneExtractorTests.cs`, `EngraverTests.cs`, `ElementInfuserTests.cs`, `HubObjectiveTests.cs` |

Each deleted asset or script was removed with its `.meta` file. Phase 2A
also removed rune configuration and Hub branches in
`BuildingPlacementController` and `ObjectivePanel`, the Prototype-only
Market save controls/path, the Hub fixture in food snapshot validation, and
the isolated rune transport adapters. Demo's two null Hub YAML fields were
removed. The active machine panel reference remains.

## Preserved food and shared code

Demo, `DemoFarmPlot.prefab`, the older food `FarmPlot.prefab`, Belt and
Harvester prefabs, Grid and occupancy, construction and placement, generic
item transport, `FoodItemData`, Farm Plot, Harvester, Processor, Mixer,
Cutter, Property supply, Market and orders, progression, regions,
Blueprints, history, camera LOD, Factory Issues, UX-07 visuals/animation,
and version-1/version-2 food save/load remain. The older `FarmPlot.prefab`
and archived rune design notes are retained as non-playable content.

Mixed Belt tests now use `FoodItemData`; Market and save rejection tests use
a non-food `ITransportItem`. Grid, food machine, Cutter paired-output,
Property, Blueprint, history, and food Save/Load coverage remains. No rune
transport adapter remains.

## Remaining Phase 2B/2C candidates

| Candidate | Reason deferred |
| --- | --- |
| `TransportedRune` | Active food carrier in `BeltCell`; renaming needs a tested migration. |
| `ObjectivePanel`, the active `engraverUpgradePanel` serialized field, `foodDemoControls`, and non-Demo branches in `BuildingPlacementController` | Names and guards remain in shared food code. Cleanup needs scene serialization and construction regression checks. |
| `FantasyShapez` assemblies and namespaces | Renaming affects serialized class identifiers and project references. |
| Legacy wording in non-food save errors and archived documents | Food save rejection and historical notes remain intentional. |

## Validation

Build Settings list only Demo. No deleted GUID occurs in retained `Assets`
or `ProjectSettings`; retained `Assets/_Project` files have matching
`.meta` files, with no orphan pairs. Before Phase 2A deletion, none of the
remaining rune runtime script GUIDs appeared in supported scenes, prefabs,
or ScriptableObjects. Demo retains its active project script GUIDs; one
pre-existing camera script GUID also occurs in SampleScene and the URP scene
template and is not among the deleted project GUIDs.
`git diff --check` passed. The runtime and EditMode test assemblies compiled
offline with zero warnings and errors after temporarily correcting stale
Unity-generated `.csproj` source lists. The generated files were restored.
Offline compilation is not Unity Test Runner execution.

In the running Unity Editor, verify import/compilation and focused EditMode
tests, then open Demo and check for missing scripts. In Play Mode, verify
startup; Farm Plot -> Harvester -> Belt -> Processor/Mixer/Cutter -> Market;
Property Collector/Pipe connections; Blueprint placement; Undo/Redo;
version-1 and version-2 Save/Load; and UX-07 visuals at Close, Medium, and
Far zoom. Record actual outcomes before claiming these pass.
