
# Cozy Food Factory
## Game Design Document (GDD)

**Version:** 0.5
**Date:** September 25, 2026
**Status:** Core Concept & Systems Defined

**Working Title:** Cozy Food Factory

---

## Milestone 04 — Factory Building UX & Readability (demo rules)

Selecting a normal building tool keeps it active after each successful placement. Failed placements also retain the tool and its rotation. R rotates the preview clockwise, and each building type remembers its last rotation when the player switches tools. Belt and Pipe tools support drag placement. Placing a Farm Plot immediately opens its crop picker, and the Farm Plot tool remembers the last crop selected. Esc or right-click cancels the active construction tool. In normal mode, right-click shows a red hover outline and removes removable buildings or Property connections; holding it draws a removal path. Ctrl+C copies a selected group, Ctrl+V previews a paste, and Ctrl+X stages a cut that removes its source only after a valid paste. Selecting a placed machine outside construction opens its configuration panel.

Construction presents Farming, Logistics, Manufacturing, and Property groups while retaining number-key shortcuts. It shows the current building and rotation, and locked machines state their required Market-order unlock. Active UI panels consume pointer input before world placement or removal. Placement feedback identifies occupied cells and building-specific restrictions.

Food inputs, food outputs, and property ports use distinct colors and shapes. The Mixer's A and B inputs and Cutter's A and B outputs have visible labels. Working and Idle machines need no persistent status badge. Actionable problems show one indicator, the affected port or combination area, and a short problem and action tooltip; Output Blocked takes priority over Invalid Recipe, Needs Property, and Needs Input. Event Toasts are reserved for progression, currency, and save events. Transported foods use distinct temporary colors and labels based on their food identity until final art exists.

## Milestone 03 — Land Expansion & Farmable Readability (demo rules)

The map shows restored farmable cells, locked future farmland, and ground that cannot host Farm Plots at all times. Farm Plot previews explain whether a cell is locked, non-farmable, or occupied. Only restored, designated farmable cells accept Farm Plots; other construction retains its current placement rules.

Players select locked regions by clicking their map area. Restored regions can be inspected through the Market list or Alt-clicked on the map, leaving ordinary building interactions available. The selected region shows its name, cell bounds, price, required progression, and used/free Farm Plot capacity. A region may be bought only when it shares a cardinal edge with an already restored region, its progression requirement is met, and regular currency covers its price. Diagonal contact does not count. A successful purchase spends currency once, restores the region immediately, and activates its farmable cells. Failed or repeated purchases spend nothing. The selected region and map show progression-locked, unaffordable, available, and restored states. Restoration gives visible confirmation. Region selection and purchase clicks cannot place or remove buildings.

The existing Vegetable Base order remains the prerequisite for East Field; restoring East Field still grants Potato once. The starting region is restored for free. The demo's additional surrounding rectangles and all region prices are **provisional authored content**, not finalized world layout or balance. Each authored rectangle currently designates every cell inside it as farmable; ground outside those rectangles is non-farmable. Region unlocks and remaining currency use the existing save data; Farm Plot usage and free-cell capacity are derived from restored buildings after loading.

Final region geometry, pricing, progression for regions beyond East Field, and procedural distribution remain undecided.

## Milestone 02 — From Farm to Food Factory (finalized demo slice)

This section fixes the first-time demo path. Quantities, sell values, crop times, and Cutter cycle time below are provisional Inspector values, not final balance.

The authored demo asks for 3 Apples, then 2 each of Dried Apples, Vegetable Base, Cut Potatoes, and French Fries. Existing sample foods sell for 1 currency; Cut Potato sells for 2 and French Fries for 3. Potato takes 2 seconds per crop and the Cutter takes 1 second per cycle in this prototype.

| Stage | Player action | Unlock |
| --- | --- | --- |
| 1 | Deliver Apples | Processor and Onion |
| 2 | Deliver Dried Apples (Apple + Air) | Basic Mixer and Tomato |
| 3 | Deliver Vegetable Base (Tomato + Onion) | Cutter and East Field restoration access |
| 4 | Restore East Field | Potato |
| 5 | Deliver Cut Potatoes | Final manufacturing order |
| 6 | Deliver French Fries (Cut Potato + Heat) | Demo Complete |

The Basil Seed Shop remains optional. Each delivery order counts only food delivered after that order becomes active. Stage 4 is a restoration gate. The Cut Potato delivery order becomes active after Vegetable Base, but Potato remains locked until East Field is restored. Restoration grants Potato once, and the guidance panel presents restoration as the next step. An order's required quantities remain serialized and editable in the scene Inspector. Locked machines are visible but cannot be selected for placement; locked crops remain visible in Farm Plot configuration.

The Cutter occupies a rotatable 1×2 footprint. At 0° its rear input is on local cell (0,0) from South; its front is (0,1), with outputs to West and East. Rotation moves all cells and ports together. One accepted ingredient yields two identical cut-result items according to a unique recipe. The initial recipe is Potato → 2 Cut Potatoes. Raw and processed foods may be authored as future Cutter inputs. The Cutter may buffer one valid ingredient while waiting for its outputs. Both output belts must be present and able to accept items before a cycle starts or advances. If either output becomes blocked, processing pauses; both completed products leave together when both outputs can accept them. The Cutter holds its input or completed output through save/load, with no cooking property input or byproduct.

The existing Processor gains Cut Potato + Heat → French Fries. Discovery records the first completed Cutter or Processor recipe. The Market's last order completion is the saved Demo Complete state. Guidance at each stage explains the relevant construction, ports, property source, crop, and Market connection.
# 1. Game Overview

## 1.1 High Concept

Cozy Food Factory is a cozy, grid-based factory automation game combining farming, cooking, recipe discovery, and production puzzles.

Players develop a small farm into a charming automated food production kingdom by growing crops, discovering recipes, designing production lines, selling food, and restoring new regions.

The game prioritizes playful internal consistency over strict realism.

## 1.2 Genre

- Cozy Factory Automation
- Recipe Discovery
- Production & Logistics Puzzle
- Farming
- Light Sandbox Building

## 1.3 Presentation

- 2D top-down perspective
- Grid-based construction
- Single persistent world
- Randomly distributed resource and farmland clusters
- Cute, chunky, simplified visual designs
- Cozy atmosphere with stylized subculture-inspired aesthetics

## 1.4 Core Design Pillars

### Farming as Automation

Farming is primarily a production and spatial-planning system rather than a manual farming simulator.

Players configure farmland to grow selected crops automatically. Harvesters collect mature crops and feed conveyor belts.

### Cooking as Discovery

Players discover recipes by combining ingredients and applying processing properties.

The recipe system should encourage logical experimentation while retaining occasional surprising discoveries.

### Factory as a Food Kingdom

Players transform discovered recipes into automated production lines.

The final factory should resemble a charming network of farms, kitchens, processing facilities, and markets rather than an industrial complex.

### Cozy but Meaningful Puzzles

The game should provide meaningful production and logistics challenges without excessive punishment or time pressure.

---

# 2. Core Gameplay Loop

1. Acquire farmland or restore a new region.
2. Unlock and purchase seeds.
3. Configure farmland to grow crops.
4. Harvest mature crops and convey them to production lines.
5. Collect processing properties from environmental nodes.
6. Experiment with ingredient combinations.
7. Discover new food recipes.
8. Build automated production lines.
9. Sell products through the market and complete orders.
10. Earn money and unlock new crops, machines, and regions.
11. Expand and decorate the food production kingdom.

## Secondary Loop

Complete special popup events to earn decoration currency.

Use this currency to customize the appearance of the farm and factory.

---

# 3. World & Map

## 3.1 World Structure

The game uses a single persistent map.

The map contains randomly distributed clusters of:

- Farmable land
- General construction space
- Special environmental nodes
- Purchasable land
- Regions requiring restoration

The exact map generation algorithm is TBD.

### Prototype Save Boundary

The first factory snapshot records placed food production equipment, its held food and
production progress, belts and their in-flight food, and player-built property
connections. Debug test loads are included because they occupy cells and consume
supply. The prototype Load action reconstructs this snapshot in a fresh scene.
Legacy rune state is not part of the new food game
design and must not be silently discarded by a prototype save.

The belt-reconstruction item deletion rule below applies when a player rebuilds a
belt, not when a snapshot is captured or restored. The Demo System Menu owns manual
Save Game and confirmed Load Game; the saved-data summary reads the stored file, not
the live session. Multiple slots and autosave timing remain TBD.

## 3.2 Expansion

Players expand through two primary methods.

### Land Purchase

Spend regular currency to unlock additional construction or farming space.

### Region Restoration

Complete progression requirements to restore and unlock new areas.

Restoration may unlock additional farmland, seeds, environmental nodes, or machines.

Exact region requirements and rewards are TBD.

---

# 4. Farming System

## 4.1 Farmland

Farming follows a configurable resource-node model.

Crops may only be grown on designated farmable land.

Players select a seed type for each available farming area.

The farmland then grows the selected crop automatically. Farmland does not output items directly to conveyor belts; a separate Harvester collects mature crops.

### Basic Flow

Farmable Land
→ Select Seed
→ Crop Growth
→ Harvester
→ Conveyor Belt
→ Processing / Mixing / Market

## 4.2 Farmland Rules

- Farmland is reusable.
- Players can reset farmland.
- Players can change the selected crop.
- Farmland does not directly output items to conveyor belts.
- Different seeds produce different crops.
- Farmland can be upgraded using regular currency.
- Farmland relocation is planned.
- Raw resource extraction does not require ongoing monetary input.

## 4.3 Seed Progression

The intended progression is:

Complete Orders
→ Restore Regions
→ Unlock Seeds in Shop
→ Purchase Seeds
→ Grow New Crops

The exact starting crop catalog is TBD.

## 4.4 Farming Upgrades

Farmland upgrades are planned.

Potential upgrade categories include production speed, yield, and relocation.

Their exact effects, costs, and unlock requirements are not yet finalized.

## 4.5 Harvester

The Harvester is a separate machine that collects mature crops from connected or nearby farmland. It stores harvested crops in an internal output buffer and transfers them to connected conveyor belts.

For the initial prototype, the Harvester has a rotatable 1x2 footprint. One cell must cover an existing Farm Plot, and the other cell must be free. The player clicks the Farm Plot cell to place the Harvester in any rotation. It collects from that plot and outputs beyond its free cell in the direction shown by its arrow. One Harvester may cover a Farm Plot at a time. Removing the Harvester leaves the Farm Plot and its crop configuration in place. A covered Farm Plot remains configurable through the Harvester panel.

When a connected belt cannot accept output, harvested crops wait in the buffer. Collection pauses when the buffer is full and resumes when space becomes available.

The following Harvester rules remain TBD:

- Wider harvesting range beyond the covered Farm Plot
- Harvesting interval
- Internal buffer capacity
- Additional placement restrictions
- Interaction with multiple farmland tiles
- Whether future farmland areas can support multiple Harvesters

---

# 5. Four Processing Properties

## 5.1 Concept

The game uses four culinary processing properties inspired by the conceptual structure of the classical four elements.

These are processing forces rather than physical elemental ingredients.

Players do not literally add soil or fire to food.

The four properties describe how ingredients transform.

## 5.2 Properties

### Heat

Concept: Heat and transformation.

Associated processes:

- Cooking
- Roasting
- Heating
- Frying

### Moisture

Concept: Water and extraction.

Associated processes:

- Boiling
- Simmering
- Infusion
- Hydration

### Time

Concept: Aging and accumulation.

Originally inspired by the conceptual qualities of Earth.

Associated processes:

- Aging
- Fermentation
- Preservation
- Maturation

### Air

Concept: Air and lightness.

Associated processes:

- Drying
- Aeration
- Whipping
- Cooling

## 5.3 Property Acquisition

Properties are acquired from special environmental nodes.

Collectors connect to these nodes and supply properties through a pipe or connection network.

Properties are not transported as ordinary items on conveyor belts.

---

# 6. Property Network

## 6.1 Current Supply Model

The initial implementation uses node-based supply capacity.

Each environmental node supports a limited amount of connected processing demand.

Example:

Heat Node Capacity: 4

Four processors requiring one unit each may be supported by that node.

Exact capacity values are TBD.

## 6.2 Network Rules

- Each supply network is associated with one source node.
- Multiple source nodes cannot merge into one network.
- Pipe length does not reduce supply performance.
- Longer pipes require additional construction cost.
- Processors connect through dedicated property pipe ports.

## 6.3 Future Direction

The capacity model is intended as an initial implementation.

A production-rate-based system is planned for a later stage.

Potential future model:

Property Production / Second
→ Property Network
→ Machine Consumption / Second

Actual flow simulation, storage, and distribution rules are TBD.

## 6.4 Multiple Properties

Basic processing initially supports one property.

Later progression allows processors to receive multiple processing properties.

All connected properties are considered part of the recipe requirements.

Example:

Apple + Heat → Baked Apple

Apple + Air → Dried Apple

Apple + Heat + Air → Apple Chips

These recipes are illustrative examples, not finalized content.

## 6.5 Unresolved Network Rules

- Behavior when supply capacity is exceeded
- Different-property pipe intersection rules
- Pipe port placement for machines beyond the initial Processor
- Future rate-based distribution rules

---

# 7. Recipe System

## 7.1 Recipe Depth

Recipes should generally require no more than three to four production stages.

Typical structure:

Raw Ingredient
→ Processed Ingredient
→ Intermediate Product
→ Finished Dish

Not every recipe must use all four stages.

## 7.2 Recipe Types

The game has two fundamental recipe types.

### Processing Recipe

Ingredient + Property Set → Result

Example:

Apple + Air → Dried Apple

### Mixing Recipe

Ingredient Set → Result

Example:

Basil + Pine Nuts → Pesto

Complex recipes are constructed by chaining these two operations.

Example:

Tomato + Onion
→ Vegetable Base

Vegetable Base + Moisture
→ Tomato Soup

## 7.3 Automatic Recipe Detection

Machines automatically determine recipes based on their current inputs.

Players do not need to manually select a fixed recipe for each machine.

A valid recipe may be produced even when it has not previously been discovered.

A new discovery triggers the recipe discovery system.

## 7.4 Recipe Matching Rules

- Production requires exactly one matching recipe.
- Invalid combinations do not produce an output.
- Invalid combinations do not automatically consume ingredients.
- Multiple simultaneously matching recipes cause the machine to wait.
- Processing considers the complete connected property set.
- Mixing evaluates the ingredients in its required slots.

## 7.5 Recipe Complexity

Early recipes require fewer ingredients and simpler operations.

Advanced recipes may require:

- Multiple processing stages
- Intermediate products
- Three or more ingredients
- Multiple processing properties
- Byproduct handling

The recipe catalog and final progression order are TBD.

---

# 8. Recipe Discovery Experience

Recipe discovery is a primary emotional reward.

The desired presentation is inspired by the anticipation and reveal of Pokémon evolution sequences.

The animation should feel cute, playful, and celebratory.

## Discovery Sequence

1. A previously unknown valid recipe is produced.
2. A discovery popup appears.
3. A short anticipation animation begins.
4. Ingredients or the resulting food undergo a playful transformation.
5. The new food is revealed.
6. The recipe is registered in the recipe journal.

Example:

Basil + Pine Nuts

"Something is happening...?"

→ Transformation

→ PESTO!

NEW RECIPE DISCOVERED!

## Presentation Goals

- Make first discoveries memorable.
- Provide clear visual rewards.
- Highlight the resulting food.
- Maintain the cozy visual identity.

Exact animation duration, sound design, rarity tiers, and skipping behavior are TBD.

---

# 9. Machine System

## 9.1 General Architecture

The machine system separates ingredient transformation from ingredient combination.

The two primary food transformation machine types are:

- Processor
- Mixer

Both use the shared recipe detection system.

Their accepted inputs and recipe categories differ.

The Harvester is a separate crop collection machine; it does not perform recipe detection.

| Machine | Responsibility |
| --- | --- |
| Harvester | Automatically collects mature crops from farmland and outputs them to conveyor belts. |
| Processor | Transforms an ingredient using processing properties. |
| Mixer | Combines ingredients. |

---

# 10. Processor

## 10.1 Purpose

The Processor transforms one ingredient or intermediate product using processing properties.

## 10.2 Footprint

Three grid cells.

L-shaped footprint.

Default (0°) footprint, with local coordinates increasing right and up:

[X][X]  y = 1
[X][ ]  y = 0

The unoccupied cell is not part of the machine footprint.

Local cells are (0, 1) for the main cooking chamber, (1, 1) for the cooking property module, and (0, 0) for the lower support module. Cell (1, 0) is empty and unoccupied.

The food input is on (0, 1), from West. The finished-food output is on (0, 1), facing North. The cooking property input is on (1, 1), from South and connects only to the separate property pipe network. All occupied cells and port positions and directions rotate together at 90°, 180°, and 270°.

## 10.3 Inputs

- One ingredient type
- Dedicated property pipe connection
- Initially one processing property
- Multiple properties in later progression

## 10.4 Outputs

- Product output
- Dedicated Compost output in a later implementation

The initial working Processor has no Compost output. A later Compost output is relevant only to recipes that generate the byproduct.

## 10.5 Processing Behavior

1. Receive ingredient.
2. Determine connected property set.
3. Evaluate matching processing recipes.
4. Produce the result if exactly one valid recipe exists.
5. Trigger discovery if the recipe is new.
6. Output the product.

The Processor does not combine multiple distinct ingredient types directly.

Recipes requiring ingredient combinations use a Mixer first.

---

# 11. Basic Mixer

## 11.1 Purpose

The Basic Mixer combines two ingredients into a new product.

## 11.2 Footprint

2 × 2 grid cells. Default (0°) occupies (0,0), (1,0), (0,1), and (1,1).

Ingredient Input A is on (0,0), from West. Ingredient Input B is on (0,1),
from West. The finished-dish output is on (1,1), toward East. All ports and
occupied cells rotate together at 90°, 180°, and 270°.

## 11.3 Inputs

Two independent ingredient input ports.

Each port feeds its corresponding ingredient slot.

## 11.4 Recipe Evaluation

The Mixer waits until both required ingredient slots are filled.

It then evaluates the resulting ingredient combination.

If exactly one valid recipe matches, the Mixer produces the result.

## 11.5 Outputs

One combined product.

The result may be:

- An intermediate ingredient
- A processed food component
- A finished dish

## 11.6 Properties

The Basic Mixer does not require property network connections.

---

# 12. Advanced Mixer

A separate Advanced Mixer will be introduced through progression.

It supports combinations involving three or more ingredients.

The Advanced Mixer is not simply an upgraded configuration of the Basic Mixer.

Its exact implementation remains TBD.

Unresolved details:

- Grid footprint
- Number of physical input ports
- Maximum ingredient count
- Unlock requirements
- Construction cost

---

# 13. Logistics

## 13.1 Item Transportation

Conveyor belts transport:

- Crops
- Processed ingredients
- Intermediate products
- Finished dishes
- Compost

Crops enter this item transportation network through Harvesters. Farmland grows crops but does not directly feed belts.

## 13.2 Property Transportation

Pipes or connection networks transport processing properties.

Item belts and property networks are separate systems.

## 13.3 Belt Reconstruction

Reinstalling a belt removes items occupying the affected belt.

This behavior is intended to make production-line modification easier.

## 13.4 Incorrect Ingredients

Players may discard incorrect ingredients without recovering their value.

Raw resource extraction is free, so retaining every item is not a core requirement.

Incorrect ingredients are not automatically consumed by invalid recipes.

The exact interaction for clearing machine buffers is TBD.

## 13.5 Blocked Machines

Blocked machines stop operating.

The affected output port displays a red warning indicator.

Examples:

- Product output blocked
- Compost output blocked
- Harvester crop output blocked

The warning should identify the actual blocked port.

Once the blockage is resolved, the machine can resume production.

## 13.6 Filtering

An advanced filtering system is planned.

Its purpose is to help manage mixed item streams and prevent unwanted ingredients from entering production machines.

Exact mechanics and unlock timing are TBD.

---

# 14. Compost & Byproducts

## 14.1 Purpose

Compost introduces an additional logistics challenge during advanced food production.

## 14.2 Generation

Certain advanced recipes may generate Compost probabilistically.

Not every recipe produces Compost.

The exact generation probabilities are TBD.

## 14.3 Future Processor Handling

When Compost handling is implemented, Processors may have:

- Dedicated Compost output
- Small internal Compost buffer

If the buffer becomes full and Compost cannot be discharged, production stops.

## 14.4 Recycling

Compost may be processed into fertilizer.

The intended loop is:

Farm
→ Food Production
→ Compost
→ Fertilizer
→ Farm

Fertilizer is intended to improve farming.

Exact conversion rates and fertilizer effects are TBD.

---

# 15. Economy

## 15.1 Regular Currency

Regular currency is earned primarily through selling food.

It is used for:

- Machine construction
- Building construction
- Land purchase
- Farmland upgrades
- Seed purchases

## 15.2 Market

The market provides ongoing opportunities to sell finished products.

The basic prototype Market accepts food delivered by conveyor belt and displays cumulative delivered quantities by food identity. Raw crops are accepted for validating the first Farm Plot → Harvester → Belt → Market loop. Basic selling awards a configurable positive value for each delivered food; current sample foods use a provisional value of one.

The exact pricing system is TBD.

## 15.3 Orders

Orders provide specific production goals.

Completing orders contributes to progression, including regional restoration and seed unlocks.

## 15.4 Special Currency

Special currency is earned through popup events.

It is used primarily for decoration and cosmetic customization.

The decoration economy is separate from the primary production economy.

---

# 16. Decoration System

Decoration is a separate gameplay component.

Players can customize the appearance of their farm and factory using decorations acquired through special event currency.

Potential decoration categories:

- Flowers
- Trees
- Fences
- Benches
- Signs
- Lamps
- Paths
- Market stalls
- Garden ornaments

The specific decoration catalog is TBD.

---

# 17. Game Progression

## Early Game

Focus:

- Basic farmland
- Simple crop growth
- Harvester collection and belt output
- Basic Mixer
- Single-property Processor
- Simple recipe discovery
- Market delivery

## Mid Game

Focus:

- More crop varieties
- Additional environmental nodes
- Multi-stage recipes
- Property network planning
- Region restoration
- More complex production layouts

## Late Game

Focus:

- Advanced Mixer
- Multi-property processing
- Advanced dishes
- Compost management
- Larger production networks
- Food kingdom expansion and decoration

Exact progression order and unlock requirements are TBD.

---

# 18. Art Direction

## Visual Goals

The game should feel:

- Cute
- Cozy
- Playful
- Charming
- Soft
- Readable from a top-down view

The design should favor simplified shapes and chunky proportions over realistic industrial detail.

Machines should look like charming miniature production devices.

Crops and food should be visually recognizable at small sizes.

## Visual Consistency

Important visual distinctions include:

- Ingredient ports versus property ports
- Processor versus Mixer
- Normal operation versus blockage
- Raw ingredients versus processed products
- Different property types

The exact palette, sprite resolution, animation style, and asset pipeline are TBD.

---

# 19. User Experience

## UX Principles

- Building and rebuilding should be convenient.
- Incorrect experiments should not be excessively punishing.
- Blocked production should be easy to diagnose.
- New recipe discoveries should feel rewarding.
- The player should be able to understand a machine's function visually.
- Complexity should increase gradually.

## Confirmed UX Features

- Belt reconstruction deletes items on the affected belt.
- Blocked machines stop.
- Blocked output ports display red warnings.
- Incorrect ingredients can be discarded.
- New recipe discoveries trigger popup animations.
- The Demo keeps order progress attached to the Market. Clicking it opens a compact
  current-order popover with separate Seeds, Sales, and History detail views.
- Each completed order shows one dismissible card with its requirements, actual
  order bonus, and newly granted unlocks. Delivery sale income remains a Toast;
  the same order unlocks are not repeated as Toasts.
- Newly unlocked buildings and crops show NEW until that exact item is selected.
  The first selection gives brief usage guidance, also available through Help.
- Region purchase feedback shows progression, adjacency, and currency conditions
  together, with actionable guidance for each unmet condition.
- Save and Load live in the Demo System Menu. Load and Quit require confirmation
  because unsaved progress may be lost. The menu shows stored-save metadata only.

## Unresolved UX Features

- Final machine rotation affordances
- Final pipe placement presentation
- Machine buffer clearing interface
- Recipe journal layout
- Building previews
- Filtering interface
- Game speed controls
- Discovery animation skipping

---

# 20. Prototype Scope

The following is a proposed initial prototype scope, not a finalized production commitment.

## Initial Prototype Goals

Validate:

1. Configurable farmland.
2. Automatic crop growth.
3. Harvester collection and buffered output.
4. Conveyor transportation.
5. Property node collection.
6. Property pipe connections.
7. Basic processing.
8. Two-ingredient mixing.
9. Automatic recipe discovery.
10. Product delivery and selling.

For this proposed initial playable prototype, the Harvester is required. Its production chain should validate:

Farmable Land
→ Crop Growth
→ Harvester
→ Conveyor Belt
→ Processor / Mixer
→ Market

## Suggested Test Recipes

### Pesto

Basil + Pine Nuts
→ Mixer
→ Pesto

### Dried Apple

Apple + Air
→ Processor
→ Dried Apple

### Tomato Soup

Tomato + Onion
→ Mixer
→ Vegetable Base

Vegetable Base + Moisture
→ Processor
→ Tomato Soup

These recipes are prototype examples and may change during content design.

## Deferred Prototype Features

- Advanced Mixer
- Compost recycling
- Full decoration system
- Large-scale regional progression
- Advanced filtering
- Multi-property recipes
- Production-rate-based property networks

---

# 21. Open Design Questions

## Machines

- Machine rotation rules beyond the initial Processor
- Harvester range beyond one covered Farm Plot, interval, buffer capacity, and additional placement restrictions
- How Harvesters interact with multiple farmland tiles or share one farmland area
- Advanced Mixer footprint and ports
- Machine upgrade architecture
- Machine processing times

## Logistics

- Property supply shortage behavior
- Pipe intersection behavior
- Machine buffer clearing interaction
- Advanced filtering behavior
- Item throughput

## Farming

- Farm production speed
- Farm upgrade effects
- Farm relocation rules
- Starting crops
- Farmland size and distribution

## Recipes

- Initial recipe catalog
- Ingredient quantities
- Recipe processing times
- Advanced recipe structure
- Discovery progression

## Economy

- Construction costs
- Product sale prices
- Land purchase costs
- Order rewards
- Event currency rewards

## World

- Map dimensions
- Procedural generation rules
- Starting region
- Region restoration requirements
- Environmental node distribution

## Art & UX

- Final visual identity
- Sprite production pipeline
- UI design
- Animation requirements
- Audio direction

---

# 22. Project Transition Notes

This GDD replaces the previous Fantasy Rune Factory gameplay concept.

The change is not limited to visual presentation.

The previous rune-specific resource model, machines, objectives, and progression are no longer authoritative requirements.

However, existing technical infrastructure may remain reusable.

Potential reusable systems include:

- Grid placement
- Machine footprint handling
- Building rotation
- Belt transportation
- Buffered item processing
- Input/output ports
- Delivery systems
- Objective tracking
- Camera controls
- Building UI

Reuse must be evaluated against the actual existing Unity codebase.

No assumption is made that all existing systems should be preserved.

---

# 23. Development Principle

The central development objective is to preserve three interconnected forms of gameplay:

**Grow ingredients.**

**Discover what they can become.**

**Build a factory that makes them automatically.**

Every major system should contribute to at least one of these experiences while supporting the long-term fantasy of building a charming, automated food kingdom.
