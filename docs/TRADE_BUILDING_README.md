# Village Trade Building

Replace your Unity project's `Assets/_Project` folder with the `_Project` folder in this archive. This is the supplied project with the trade foundation added; the archive does not include Unity packages or ProjectSettings.

## Demo

1. Open `Scenes/Demo.unity` and enter Play Mode.
2. Choose **TradeBuilding** in the build menu or hotbar (shortcut **7**).
3. Place it on an empty 3×3 area. Choose Egg or Milk in the popup.
4. Route **Dried Apple** into any of its three left inputs. At rotation 0, the incoming Belts point right.
5. Connect any of the three bottom outputs. At rotation 0, outgoing Belts point down.
6. Both demo trades consume Dried Apple ×3 and produce the selected ingredient ×2.
7. Click the building to reopen its chooser. Changing trades requires an empty input buffer and no pending output.

The solid coloured placeholder body uses compact blue input arrows and red output arrows in both preview and placed states, with no A/B/C text. The chooser uses the same compact layout as the crop popup; hover Egg or Milk to see the full exchange. Left inputs and bottom outputs follow the reference, with three of each. Rotation rotates the footprint and all six ports together.

## Safety and output behavior

- A trade must be selected before any food is accepted.
- Wrong food remains on the incoming Belt.
- Accepted food waits in a bounded batch buffer until the recipe quantity is reached.
- The batch is exchanged only when its exact quantity is collected.
- All exits share one pending output count. Items leave through available exits without multiplication; not every exit needs a Belt.
- Missing or occupied exit Belts retain undelivered output. New input waits until output is drained.
- Trade selection, partial input batches, and pending output counts are saved and restored.
- Removal and Demo move operations are blocked while the building holds items.
- Existing issue feedback, port highlighting, and machine animation are reused.
- Demo trades are immediately available; no village progression or NPC system was added.

## Configure more trades

On the Demo `BuildingPlacementController`, expand **Trade Recipes** in the Inspector. Each entry defines:

- `id`: unique stable trade ID, used by saves.
- `villageId`: the village offering the trade.
- `input`: normal FoodItemData (ID, kind, sell value).
- `inputQuantity`: positive batch size.
- `output`: normal FoodItemData; Egg and Milk use RawIngredient.
- `outputQuantity`: positive batch size.

**Trade Village Id** selects the village's available recipes for this building option. The demo uses `demo-village`. Add recipes with that ID to extend its chooser. Another configured placement behavior can supply a different village's recipe list using the same building logic. Keep IDs and quantities stable for saved games; unavailable or incompatible saved trades are rejected before reconstruction.

This adds the ingredient foundation. It does not add Egg/Milk cooking recipes, all villages, final balancing, unlock progression, final art, or dialogue.

## Verification

All 91 C# source files passed a C# syntax parser. Unity is unavailable in the execution environment, so compilation, Edit Mode tests, and Play Mode validation have not been run here.

Run Unity Test Runner → EditMode, including `TradeBuildingTests` and the existing suite. New cases cover selection, wrong food, exact quantities, recipe switching restrictions, partial-batch/output restoration, invalid saves, duplicate IDs, JSON round trips with existing Belts, and real Belt transfers through rotated ports with blocked output.

Manual regression checks:

1. Trade three Dried Apples for two Eggs, then repeat for Milk.
2. Feed raw Apple to a selected trade; it must remain on the Belt.
3. Remove all exit Belts, complete a batch, reconnect only exit C; exactly two outputs must leave.
4. Save and reload with two inputs buffered, then with one output still waiting.
5. Rotate the building and test its outermost input/output ports.
6. Verify farms, Harvesters, Processors, Mixers, Cutters, Market delivery, and existing Save/Load still work.

## Changed files

New runtime scripts: TradeRecipe, TradeProcess, TradePortLayout, TradeBuilding, TradeBuildingPlacementBehavior.

Integration edits: Demo.unity, BuildingPlacementController, BuildingVisualFactory (third-port feedback and compact Trade Building arrows), MachineVisualAnimator, FactoryWorldSnapshot, ConstructionHistory.

New tests: TradeBuildingTests.
