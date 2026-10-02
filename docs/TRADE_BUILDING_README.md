# Village Trade Building

The Trade Building was added as a general village trade foundation before Chapter 1 campaign integration. In active `Demo.unity`, O8 unlocks the Chicken Trading Center; its `chicken-village` trade exchanges Garden Lunch x1 for Egg x2. The campaign sequence, unlock, and recipe are authored on the Market and Building Placement Controller components described in `ARCHITECTURE.md`.

This repository integrates the trade foundation into the existing project; do not replace the project folder with the historical source archive.

## Demo

After completing O8, choose **Chicken Trading Center** in the Build Menu.
Place it on an empty 3x3 area and select the Garden Lunch for Egg trade. Each
Garden Lunch produces two Eggs. Connect Garden Lunch to any left input and
connect a bottom output to Market; the ports rotate with the building.

The colored placeholder body uses blue input arrows and red output arrows in preview and placed states. The chooser shows the exchange and port directions. Left inputs and bottom outputs follow the reference; all six ports rotate with the footprint.

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
- The campaign locks construction until O8 and uses this existing trade foundation.

## Configure more trades

On the Demo `BuildingPlacementController`, expand **Trade Recipes** in the Inspector. Each entry defines:

- `id`: unique stable trade ID, used by saves.
- `villageId`: the village offering the trade.
- `input`: normal FoodItemData (ID, kind, sell value).
- `inputQuantity`: positive batch size.
- `output`: normal FoodItemData; Egg and Milk use RawIngredient.
- `outputQuantity`: positive batch size.

**Trade Village Id** selects the village's available recipes for this building option. Chapter 1 uses `chicken-village`. Add recipes with that ID to extend its chooser. Another configured placement behavior can supply a different village's recipe list using the same building logic. Keep IDs and quantities stable for saved games; unavailable or incompatible saved trades are rejected before reconstruction.

The active Chapter 1 configures Egg + Tomato Sauce -> Tomato Omelette in the existing Basic Mixer recipe list. It does not add other villages, final balancing, final art, or dialogue.

## Verification

The existing TradeBuildingTests cover trade selection, invalid food, exact batch sizes, buffered output, save restoration, duplicate prevention, rotated Belt ports, and blocked output. ChapterOneCampaignTests now cover O1-O10, quantities, recipes, unlock timing, and O9/O10 progress save round trips.

These tests and Unity compilation have not been run in this task. Run them in the already-open Unity Editor, then manually verify the complete campaign and blocked trade output behavior in Play Mode.




## Existing implementation

The campaign reuses TradeRecipe, TradeProcess, TradePortLayout, TradeBuilding,
FactoryWorldSnapshot, ConstructionHistory, BuildingVisualFactory, and
MachineVisualAnimator. This task configures the existing systems for Chapter 1.

New tests: TradeBuildingTests.
