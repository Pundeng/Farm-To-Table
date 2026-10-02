# Territory world and save-slot workflow

## Starting map and ownership

The Demo uses 9x9-cell parcels. The Market parcel is reserved and neutral; the eight parcels in the surrounding 3x3 ring are owned and buildable at game start. The Market is centered at anchor (2,2) inside the reserved parcel and occupies cells 2..6 on both axes. The reserved parcel does not receive the standard owned/frontier/locked overlay and does not generate ordinary Property clusters. Its free cells outside the Market footprint are shared factory space and accept normal player construction.

Territory rules and pricing live on **Demo > Market > Territory Settings**. `expansionCosts` defaults to 100, 180, 320, 550, 900, 1500. The next tier sums the preceding two values (2400, 3900, 6300...) and saturates at `int.MaxValue`. Each purchase expands the complete current north, south, east, or west side by one parcel row/column. The global purchase count determines price regardless of direction. Parcel ownership is saved; current bounds must form a rectangle except for the reserved center parcel.

Owned parcels use a subtle light-green tint; the currently purchasable side is yellow; unavailable territory is gray; the Market parcel remains untinted. Property clusters are deterministic from world seed, parcel coordinate, and generation version 5. Eligible parcels get a connected 4-6-cell cluster in local cells 2..6. The Hub and its immediate neighboring parcels are excluded to preserve space around the Market. The first Heat and Water deposits remain guaranteed in the accessible side expansions.

## Market and construction visuals

The Market reserves exactly 5x5 cells. The three center cells on each of its four edges form twelve input lanes. Belts outside the footprint are ordinary cells and use the existing delivery, currency, objective, and feedback pipeline. The visual root never changes occupancy or port coordinates.

Assign Market art on **Demo > Market > Visual Sprite** for one large image or **Visual Prefab** for a composed visual with child sprites. A prefab takes precedence over the sprite. Its bounds may extend beyond the footprint but do not reserve neighboring cells. Assign machine art on **Demo > Building Placement > Building Options > each Building Definition > Visual Sprite/Visual Prefab**, with `Visual Scale`, `Visual Offset`, and `Visual Sorting Offset` for presentation adjustments. The prefab is a child visual and does not alter the building definition footprint. Property artwork is assigned in **Building Placement > Property Visuals**; transported-food artwork is assigned in **Belt Transport Coordinator > Food Visuals**. Import art externally; the project retains a placeholder fallback until assets are assigned.

## Group delete and history

Shift-drag selects multiple player-built objects. Delete removes the selected group after the existing protected-object and Property-dependency checks. The operation captures one before/after construction layout, so one Undo restores the group and one Redo removes it. The selection/highlights are cleared as objects are removed. Belt removal updates live transport topology; Property connections are removed through the existing network, which derives connected state from the current connection graph.

## Save slots and migration

The System menu exposes five independent files under `Application.persistentDataPath`: `cozy-food-factory-slot-1.json` through `cozy-food-factory-slot-5.json`. Each reuses `ProgressionSaveData` and `FactoryWorldSnapshot`; the active slot is session state. The menu shows Empty/Saved and, for valid saves, timestamp, objective, currency, seed, parcel count, and next expansion price. Load replaces the running scene from the selected snapshot. Saving to an occupied active slot asks for confirmation. Starting a new game in an occupied selected slot also asks for confirmation and clears that old slot only after the clean scene reload succeeds.

Version 6 is the current map/snapshot format. Version 1 progression-only saves remain readable. Version 5 saves describe the previous single-parcel map and cannot be safely reinterpreted under the new layout; they are rejected with an explanation and left untouched. The old single-save filenames are not automatically migrated into slot 1. An empty Slot 1 summary points out their presence so the user can start a clean game while retaining the old files.

## Verification boundaries

EditMode coverage and source compilation are distinct from Unity Editor compilation, Unity Test Runner execution, and manual Play Mode. In the running Editor, verify all four port sides, side expansion, construction and group deletion/Undo/Redo, visual scale/occupancy separation, and two distinct save slots. No manual Play Mode result should be inferred from offline checks.
