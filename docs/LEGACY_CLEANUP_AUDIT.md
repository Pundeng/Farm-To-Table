# Legacy Cleanup Phase 1 through 3 results

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

Phase 2B removed the unreachable `foodDemoControls == false` F8, number-key,
Escape, and right-click construction shortcuts, plus the old non-Demo group
placement and rollback transaction. Demo group Move continues through
`CanCutDemoSources`, `CanPlaceDemoGroup`, and `PlaceDemoGroup`. The former
`IBuildingMoveState.ReattachAfterFailedMove` hook was exclusive to the retired
transaction and was removed; Belt's active `CanMove` guard and
`DetachForMove` cleanup remain.

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

## Remaining legacy names

| Candidate | Reason deferred |
| --- | --- |
| `ObjectivePanel`, the active `engraverUpgradePanel` serialized field, and `foodDemoControls` guards in placement and UI | Names and guards remain in shared food code. Demo serializes `foodDemoControls: 1` and references the active `ObjectivePanel` component. Removing or renaming them needs scene serialization and UI regression checks. |
| Legacy wording in non-food save errors and archived documents | Food save rejection and historical notes remain intentional. |
| `BuildingDefinition.id = "PrototypeMachine"` and Belt's `runeDebug` field | Naming/default/debug data only; neither proves a live rune gameplay path. Serialized field changes need prefab inspection. |

The active `ProgressionSaveService.DemoPath` and `isDemo` migration marker remain:
version-1 Demo progression migration and version-2 food world snapshots must
continue to load. Non-food payloads, unsupported building IDs, and Demo debug
demands still fail validation. `BuildingGroupCopy.CanPlace` and the related
occupancy tests remain even though the controller no longer calls that helper:
the helper is generic and directly tested, and deleting it is unnecessary for
Phase 2B.

Remaining serialized-name cleanup needs Editor and Demo regression results. It
must cover scene/prefab references, food saves, Blueprint IDs, and test fixtures.

## Phase 2C transport rename and inventory

The Phase 2B worktree remains uncommitted. A Unity Editor process is running,
but no Unity window was available through the UI inventory, and no current
EditMode or Demo Play Mode results were found. Import status, missing scripts,
Console errors, and the camera component in the running Demo could not be
confirmed. The camera GUID `a79441f348de89743a2939f4d699eac1` resolves
on disk to `UniversalAdditionalCameraData.cs.meta` in the installed URP
package. This does not replace an Editor check. The later prompt authorized
the internal type rename after reconfirming its dependency graph; the broader
serialized-field migrations remain deferred.

`TransportedItem` is a plain C# runtime wrapper for `ITransportItem`, entry
direction, and progress. Its project type references are limited to `BeltCell`,
`Belt`, and `BeltTransportSystem`. No scene, prefab, ScriptableObject, test, or
project setting contains the type name or its script GUID. The wrapper is not
a `MonoBehaviour` or a JSON field; food snapshots persist `SavedBelt.item`
as `SavedFood` plus `entryDirection` and `progress`. No reflection or
string-based construction of the wrapper was found in project source. Its
public C# API has project callers, but external compiled callers cannot be
ruled out from repository inspection alone. Phase 2C renamed the single
wrapper, its constructors, and its project references, retaining GUID
`e6d85389c97b2034cb20daa5ea435757` and behavior. No food save key, stable
building or food ID, serialized field, or `FantasyShapez` name changed.

Other remaining names: Belt's serialized `runeDebug`, the serialized
`engraverUpgradePanel` and `foodDemoControls` fields, and `ObjectivePanel`
require an Inspector/scene migration check. `BuildingDefinition`'s serialized
`PrototypeMachine` default needs prefab/default-value review. The `isDemo`
flag and historical RuneData error text remain tied to version-1 migration
and non-food save rejection. No independent safe naming-only code edit was
identified during Phase 2C. The Phase 3 namespace and assembly migration is
recorded below; it did not alter food IDs, building IDs, or save keys.

An earlier Phase 2C inspection compiled Runtime and EditMode test assemblies
offline with zero warnings and errors. The project asset scan found no missing
or orphan `.meta` files or duplicate GUIDs, and `git diff --check` passed.
Unity tests and Demo Play Mode were not executed during that inspection.

For the completed wrapper rename, Runtime and EditMode test assemblies compiled
offline with zero warnings and errors after temporarily updating the stale
Unity-generated `.csproj` source entry; the generated project file was
restored. No `TransportedRune` source or test reference remains. The renamed
script keeps its original GUID, all `Assets/_Project` files have matching
`.meta` partners, and no project asset GUID is duplicated. `git diff --check`
passed. Unity EditMode and Demo Play Mode verification of this rename remain
pending in the running Editor.

## Phase 3 namespace and assembly migration

Project-authored Runtime, Editor, and EditMode test C# code now uses
`CozyFoodFactory` namespaces and fully qualified references. The two asmdefs
are `CozyFoodFactory.Runtime` and `CozyFoodFactory.EditModeTests`; the test
asmdef references the new Runtime assembly. Their filenames moved with the
original `.meta` GUIDs (`7acd7498e041457aa4f641002b80123d` and
`b85420cf3c9d429b8068d1e7a697d4bc`). The reflection-based test lookup
was updated to the new namespace.

Only project-authored `m_EditorClassIdentifier` values were migrated in the
supported `Demo.unity` scene (14) and Belt, DemoFarmPlot, FarmPlot, and
Harvester prefabs (two each). Their `m_Script` GUIDs and other Inspector data
were not changed. No ScriptableObject needed an identifier migration.
The untracked `Assets/_Recovery/0.unity` retains old identifiers and was
preserved as user recovery data; it is not a supported playable scene.

The remaining `runeDebug` and `engraverUpgradePanel` serialized field names,
`foodDemoControls` guard, `ObjectivePanel` class name, and
`PrototypeMachine` default were retained to avoid an unrelated serialized
field or stable-ID change. Historical RuneData wording remains in active
invalid-save rejection and archived material. The repository folder name
also remains unchanged. Food save keys, Blueprint schema, and gameplay rules
were not migrated.

Phase 3 offline validation: Runtime and EditMode test assemblies compiled as
`CozyFoodFactory.Runtime` and `CozyFoodFactory.EditModeTests` with zero warnings
and errors. The Unity-generated `.csproj` files were adjusted temporarily for
the build, then restored; asmdefs remain the source of truth. Seventy-nine
otherwise unchanged C# files matched the exact namespace substitution.
The Demo/prefab diff contains only the 22 intended
`m_EditorClassIdentifier` changes. All 110 project asset GUIDs are unique,
all `Assets/_Project` files have matching `.meta` partners, and the renamed
asmdefs and transport script retain their prior GUIDs. The only project
scene/prefab script GUID absent from repository assets is the pre-existing
URP camera GUID, which resolves in the installed package cache.
`git diff --check` passed. Unity import, EditMode tests, and Demo Play Mode
remain pending for this migration; offline compilation is not their result.

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

Phase 2B: current Unity-generated projects compiled offline with zero warnings
and errors for both Runtime and EditMode test assemblies. The changed scripts
retain their `.meta` files; all 110 project asset GUIDs are unique, and no
file under `Assets/_Project` lacks its `.meta` partner or has an orphan partner.
No removed rune type remains referenced in active assets or project settings.
Demo still contains the pre-existing camera component GUID
`a79441f348de89743a2939f4d699eac1`, also present in SampleScene and the
URP scene template; it has no repository `.meta` and must be resolved by the
running Editor/package import check. No other nonzero Demo project asset GUID
was unresolved in the repository scan. `git diff --check` passed. Unity Editor,
EditMode Test Runner, and Play Mode were not run in Phase 2B.

In the running Unity Editor, verify import/compilation and focused EditMode
tests, then open Demo and check for missing scripts. In Play Mode, verify
startup; Farm Plot -> Harvester -> Belt -> Processor/Mixer/Cutter -> Market;
Property Collector/Pipe connections; Blueprint placement; Undo/Redo;
version-1 and version-2 Save/Load; and UX-07 visuals at Close, Medium, and
Far zoom. Record actual outcomes before claiming these pass.
