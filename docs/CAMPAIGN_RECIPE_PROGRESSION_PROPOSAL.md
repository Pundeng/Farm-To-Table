# Cozy Food Factory Campaign & Recipe Progression Proposal

Design proposal, 2026-09-29. No content or balance in the Unity project is changed by this document. **IMPLEMENTED NOW** means present in inspected code or serialized Demo data; it does not claim Play Mode verification. **DOCUMENTED / PLANNED** means described by the GDD without matching current Demo content. **NEW PROPOSAL** means a design choice here, including every objective after the existing five. Counts and barter terms are playtest starting points, not final balance.

**Historical snapshot:** this proposal predates the implemented O1-O10 Chapter 1
campaign. Its audit, objective alternatives, and proposed trades are not current
campaign requirements. See [ARCHITECTURE.md](ARCHITECTURE.md) and the opening
Chapter 1 section of [COZY_FOOD_FACTORY_GDD.md](COZY_FOOD_FACTORY_GDD.md) for
the active sequence.

## 1. Current Game Audit

### Implemented

| Area | Inspected repository evidence and current content |
| --- | --- |
| Farm ingredients | [DemoFarmPlot.prefab](../Assets/_Project/Prefabs/DemoFarmPlot.prefab) authors Apple (initial), Onion (unlocked by order 1), Tomato (order 2), Basil (Seed Shop purchase after order 1), and Potato (East Field restoration). These are the five raw food identities; crop names and food IDs differ in capitalization. |
| Processed foods and recipes | [Demo.unity](../Assets/_Project/Scenes/Demo.unity) authors Apple + Air -> Dried Apple; Tomato + Onion -> Vegetable Base; Potato -> **2 Cut Potatoes**; Cut Potato + Heat -> French Fries. These are four recipes and four processed food identities. Dried Apple and Vegetable Base sell for 1 in this scene, Cut Potato for 2, and French Fries for 3. |
| Machines | Demo offers Farm Plot, Harvester, Belt, Processor, Basic Mixer, and Cutter. The Processor accepts one food and one Property; the Mixer accepts an unordered pair; the Cutter accepts one food and waits for **both** output belts before releasing its two identical outputs. Harvester gathers a Farm Plot crop. [Recipe model files](../Assets/_Project/Scripts/Food/ProcessingRecipe.cs) and [Cutter](../Assets/_Project/Scripts/Food/CuttingRecipe.cs) support these shapes, not three-input or multi-Property recipes. |
| Property | Demo contains one source each for Heat, Moisture, Time, and Air at capacity 4. Collector and Pipe connect one source to Processor demand; different sources cannot be joined. All four sources are already scene-authored, even though the current recipe set uses only Air and Heat. [Architecture](ARCHITECTURE.md) describes capacity failure as unavailable supply, with shortage allocation undecided. |
| Orders and economy | The Market accepts raw or processed food, awards its authored sell value, and tracks one active [FoodOrder](../Assets/_Project/Scripts/Food/FoodOrder.cs). Only deliveries **after** activation count. Order unlocks use category/ID keys; currency also purchases Basil and land. There is no separate objective-completion currency bonus in current data. |
| Land | Starter Fields begin restored. East Field costs 5, requires access from Vegetable Base, and grants Potato when purchased. West Meadow costs 3, North Terrace 4, and South Orchard 4; those three have **no current progression gate**. Only Farm Plot placement is limited to restored authored farmable regions; other building placement is not a land gate. Prices and rectangles are provisional scene content. |
| Discovery and help | A first actual Processor, Mixer, or Cutter production records discovery and opens its card. The Book shows discovered details, active-order manufacturing hints, and anonymous future recipes; a hint is not discovery. Market guidance names steps for each existing order. Blueprint, history, issue diagnosis, Save/Load, and zoom information levels support rebuilding. |
| Current objective sequence | See the next table. The IDs and order are serialized on the Market object in [Demo.unity](../Assets/_Project/Scenes/Demo.unity); editing or reordering them can invalidate saved progression, as [ARCHITECTURE.md](ARCHITECTURE.md) explains. |

| Position | Stable current ID | Delivery after activation | Actual unlock |
| --- | --- | --- | --- |
| 1 | First Harvest | Apple x3 | Processor, Onion, Basil Seed Shop offer |
| 2 | Dried Apples | Dried Apple x2 | Basic Mixer, Tomato |
| 3 | Vegetable Base | Vegetable Base x2 | Cutter, East Field access |
| Gate between 3 and 4 | East Field purchase | 5 currency, cardinal adjacency and access required | Potato crop |
| 4 | Cut Potatoes | Cut Potato x2 | None |
| 5 | French Fries | French Fries x2 | Demo complete key |

### Documented / Planned

The [current GDD](COZY_FOOD_FACTORY_GDD.md) calls for a longer food kingdom, deeper chained recipes, gradual Property learning, region expansion, and a later Advanced Mixer and multi-Property processing. Those later machines and recipe capabilities are **not** in the Demo. The GDD's example Pesto and Tomato Soup are illustrations, not authored recipes. It also discusses filtering, Compost, and production-rate Property networks as deferred. The prompt for this proposal introduces village trading; neither the inspected Demo systems nor the GDD provide a working village, Trade Building, exchange contract, or imported food source. The present Basil Seed Shop is a currency purchase, not production-network trading.

### Gaps

1. The second current order introduces Processor placement, a four-part Property path (source, collector, pipe, demand), and the first discovered processed food together. This is the sharpest existing onboarding jump.
2. Four Property sources exist in the scene, but only Air and Heat have authored uses. A document cannot honestly call Moisture or Time locked: future pacing needs explicit presentation or access rules.
3. The five-order Demo stops just as products could become shared intermediates. No current recipe asks the player to split Vegetable Base, reuse Dried Apple, or combine multiple finished chains.
4. There is no imported ingredient, trade loop, village location, or logistics interface. Recipe visibility is not a hard unlock: authored valid recipes can be discovered by production.
5. A cumulative delivery order can prove output and parallel demand, but cannot prove a sustained items-per-minute target. Land restoration presently expands farmable area, not exclusive factory floor.

## 2. Progression Philosophy

Keep each objective's main question concrete: *Can I grow it? route it? transform it? share it? supply it? exchange it? combine it? keep two lines running?* A player should be able to answer by watching food move and then receive a capability that makes the next question useful. Most objectives ask for 1-3 deliveries to confirm a new build; later multi-line checks rise to 3-4 per item only where they let the player watch a stable loop. Every later product draws on an earlier line. New foods should either add a recognizable meal or create a distinct routing decision.

The reference is a design principle, not copied content. [Factorio's tutorial review](https://factorio.com/blog/post/fff-327) argues for introducing automation in an order that lets players appreciate each replacement of manual work; its [campaign design discussion](https://www.factorio.com/blog/post/fff-284) favors organic production learning over purposeless tasks. [Shapez 2's developer research-system account](https://store.steampowered.com/news/posts/?appgroupname=shapez.io&appids=1318690&enddate=1719496875&feed=steam_community_announcements) describes evolving milestone goals alongside side tasks; the borrowed idea here is one clear main line with optional recipe experiments. [Satisfactory's developer launch note](https://coffeestain.com/news/satisfactory-1-0-launch/) points to visibly changing project phases; the borrowed idea is a village that visibly gains trade capacity after the player's food deliveries. [Dyson Sphere Program's developer update](https://store.steampowered.com/news/posts/?enddate=1732970088&feed=steam_community_announcements) describes goal hints tied to feasible next steps; this proposal uses contextual port, Book, and region guidance rather than long tutorials.

## 3. Campaign Overview

**NEW PROPOSAL:** Seven chapters, **17 ordered objectives** in total: the existing five kept in place, followed by 12 proposed objectives. The content plan uses the four current recipes and **eleven proposed recipes** (nine required, two optional), two imported ingredients, and two proposed villages. The first five IDs, order, amounts, and unlocks remain a fixed opening for save compatibility. Appended objectives use new stable IDs. A future redesign of those first five may be worthwhile, but requires an explicit save migration rather than silently rewriting authored content.

The path is farm -> first Property processing -> two-input food -> region/Cutter -> shared intermediate -> split and merge -> trade-in / ingredient-out -> Time and an older-chain return -> parallel village meal supply. Advanced Mixer, multi-Property recipes, Compost, filters, and true throughput objectives are outside this campaign's required path because the inspected runtime does not support them yet.

## 4. Chapter Structure

| Chapter and objectives | Theme / new lesson | Introduced food, machine, Property | Land and village | Expected factory and exit understanding |
| --- | --- | --- | --- | --- |
| 1. First harvest, O01-O02 | Grow, harvest, route, then transform. | Apple; Onion; Processor and Air; Dried Apple. | Starter Fields and Market. | One crop line becomes one Processor line. Player sees food and Property use separate paths. Existing O02 pacing risk needs focused guidance. |
| 2. Two inputs and new ground, O03-O05 | Mix two crops; restore land; split Cutter output; apply Heat. | Tomato, Basic Mixer, Vegetable Base; Cutter, Potato, Cut Potato, French Fries; Heat. | East Field purchase unlocks Potato. | Two crop inputs, one Cutter with two outlets, then a Processor. Player knows ports, land purchase, and one-to-two output. |
| 3. Share a kitchen, O06-O08 | Reuse Vegetable Base, branch an output, bring two processed lines together. | Moisture; Tomato Soup; Basil via existing shop; Tomato Basil Dip; Garden Fries & Dip. | **Proposed** West Meadow access after O07 for another crop lane. | Two or three active ingredient lines, a Belt branch, then a converging Mixer. Player can trace a shared intermediate without starving a line. |
| 4. Dairy Hamlet, O09-O10 | A finished food becomes a trade input; its imported output joins a recipe. | **Proposed** Trade Building, Milk, Creamy Tomato Soup. | **Proposed** Dairy Hamlet after O08 and North Terrace access after O10. | Trade input and output are separate visible belts. Player understands import as production, not a shop purchase. |
| 5. Slow flavors, O11-O13 | Introduce Time alone, then recombine its result with familiar Heat food. | Pickled Onion, Tomato Relish, Relish Fries; Time. | **Proposed** South Orchard access after O13. | Three- to four-stage branch with two independent Property networks. Player can reuse Vegetable Base and Fries. |
| 6. Apiary return, O14-O15 | Reopen the first Dried Apple line for a second village. | **Proposed** Honey, Honeyed Dried Apple. | **Proposed** Orchard Apiary after O13. | An old Air line gains a trade branch and a new Mixer outlet. Player values maintenance of older chains. |
| 7. Village picnic, O16-O17 | Supply two meals concurrently, then make a combined lunch while preserving a dessert side. | Village Lunch; no new machine or Property. | Visible village celebration is a **proposed** presentation reward. | Several lines share crops and outputs. Player can diagnose blocked or undersupplied branches and keep parallel production going. |

## 5. Main Objective Sequence

**Status:** O01-O05 are the authored Demo order. O06-O17 are **NEW PROPOSALS**; their IDs and rewards have not been created in Unity. “Recipe introduced” means a Book/order hint and a reachable combination, not an existing hard recipe lock. “Access” rewards, village flags, and Trade Building availability need authored systems. Complexity is a planning estimate, not measured balance.

| ID / title | Required delivery after activation | Main new lesson; earlier skill reused | Reward, newly introduced recipe / ingredient / machine / Property / region | Expected build and UX reason |
| --- | --- | --- | --- | --- |
| O01 First Harvest / Deliver Apples **[now]** | Apple x3 | Harvest-to-Market automation; place and route a Belt. | Processor, Onion, Basil shop offer. No recipe, Property, or region reward. | New one-line farm. Three deliveries prove a continuous loop quickly. |
| O02 Dried Apples / Deliver Dried Apples **[now]** | Dried Apple x2 | First Processor and Air supply; reuse Apple transport. | Mixer, Tomato. Introduces Apple + Air -> Dried Apple and Air use. | Extend Apple line into Processor. Two outputs confirm supply and reveal the first discovery. |
| O03 Vegetable Base / Deliver Vegetable Base **[now]** | Vegetable Base x2 | Two-input Mixer; reuse two crop/Harvester lanes. | Cutter and East Field access. Introduces Tomato + Onion -> Vegetable Base. | New parallel crop lane into Mixer. The distinct input ports make the lesson visible. |
| O04 Cut Potatoes / Deliver Cut Potatoes **[now]** | Cut Potato x2 | Restore East Field, then connect both Cutter outputs; reuse crop farming. | Potato comes from the **separate East Field purchase**, not from this order. Introduces Potato -> 2 Cut Potatoes. | New Potato line. Two products match one Cutter cycle, so blocked paired outputs are easy to see. |
| O05 French Fries / Deliver French Fries **[now]** | French Fries x2 | Heat processing after cutting; reuse Cutter output. | Demo complete key. Introduces Cut Potato + Heat -> French Fries and Heat use. | Extend one Cut Potato outlet into Processor; preserve the other. This closes the existing Demo. |
| O06 Garden Soup / Deliver Tomato Soup **[proposal]** | Tomato Soup x2 | Moisture at one Processor; reuse Vegetable Base. | Introduce Vegetable Base + Moisture -> Tomato Soup; Moisture guidance. No literal Property gate exists yet. | Extend a Vegetable Base branch. Reusing Processor reduces novelty while the new source teaches one Property. |
| O07 Basil Dip / Deliver Tomato Basil Dip **[proposal]** | Tomato Basil Dip x2 | Split Vegetable Base between soup and dip; reuse Basil shop and Mixer. | Introduce Vegetable Base + Basil -> Tomato Basil Dip; proposed West Meadow access. | Extend existing Vegetable Base line with a branch, not a replacement. Two accessible outlets make routing the task. |
| O08 Garden Meal / Deliver Garden Fries & Dip **[proposal]** | Garden Fries & Dip x2 | Converge Fries and Dip chains; reuse Heat, Cutter, Mixer. | Introduce French Fries + Tomato Basil Dip -> Garden Fries & Dip; proposed Trade Building and Dairy Hamlet. | Build one new join near existing lines. The resulting finished food becomes the next trade good. |
| O09 First Exchange / Deliver Milk **[proposal]** | Milk x1 | Send Garden Fries & Dip to a village and route the returned Milk; reuse meal line. | Introduce imported Milk and proposed Creamy Tomato Soup hint. No new recipe is produced here. | Extend meal line to Trade Building, then one output belt to Market. One imported item proves the two-sided exchange. |
| O10 Creamy Kitchen / Deliver Creamy Tomato Soup **[proposal]** | Creamy Tomato Soup x3 | Incorporate imported Milk; reuse Tomato Soup and Mixer. | Introduce Tomato Soup + Milk -> Creamy Tomato Soup; proposed North Terrace access. | New Mixer joining two existing outputs. Three servings expose inadequate Milk return without a long grind. |
| O11 Patient Onion / Deliver Pickled Onion **[proposal]** | Pickled Onion x2 | First Time source; reuse Onion farming and known Processor layout. | Introduce Onion + Time -> Pickled Onion; Time guidance. | Extend Onion line. A single new Property avoids a multi-network lesson. |
| O12 Relish / Deliver Tomato Relish **[proposal]** | Tomato Relish x3 | Combine Time product with shared Vegetable Base; reuse Mixer and split. | Introduce Vegetable Base + Pickled Onion -> Tomato Relish. | Add a Mixer branch. Shared Vegetable Base now serves three recipes, making routing and buffering useful. |
| O13 Relish Fries / Deliver Relish Fries **[proposal]** | Relish Fries x3 | Combine Heat and Time branches in one food chain; reuse Fries. | Introduce French Fries + Tomato Relish -> Relish Fries; proposed South Orchard and Apiary access. | Extend two products into a Mixer. This rehearses a late-game join without introducing another machine. |
| O14 Orchard Trade / Deliver Honey **[proposal]** | Honey x1 | Reopen Dried Apple output for a second trade route; reuse Air line. | Introduce imported Honey and proposed Honeyed Dried Apple hint. | Branch an old line to Orchard Apiary, then route one Honey to Market. The village matters because it revalues early infrastructure. |
| O15 Honeyed Apples / Deliver Honeyed Dried Apple **[proposal]** | Honeyed Dried Apple x3 | Use a second import inside the factory; reuse Dried Apple. | Introduce Dried Apple + Honey -> Honeyed Dried Apple. | Add a Mixer to an old line. Three items justify keeping trade operating while preserving the original output. |
| O16 Village Supply / Deliver two foods **[proposal]** | Creamy Tomato Soup x2 **and** Relish Fries x2 | Keep two known chains active simultaneously; reinforce import, Heat, Moisture, and Time. | No recipe, crop, machine, Property, or land unlock; proposed final picnic request. | Extend outlets to Market. The existing order model supports two requirements; this tests coexistence, not a guaranteed rate. |
| O17 Village Picnic / Deliver lunch and dessert **[proposal]** | Village Lunch x3 **and** Honeyed Dried Apple x3 | Join soup and fries, then maintain a separate dessert side; reinforce all prior routing and both villages. | Introduce Creamy Tomato Soup + Relish Fries -> Village Lunch; proposed campaign completion and visible village celebration. | Add a final Mixer while keeping Honeyed Apple flowing. The moderate paired targets reward a reliable factory rather than a stockpile. |

The existing five IDs are written exactly as serialized; O06-O17 are working IDs. Current order validation accepts multiple unique food requirements, so O16/O17 fit that model conceptually. Their foods, unlock keys, trade outputs, and Book hints still require future authoring and implementation.

## 6. Ingredient Unlock Order

| Stage | Ingredient | Source and status | Why here |
| --- | --- | --- | --- |
| Start -> O01 | Apple -> Onion | Apple initial; Onion rewarded by O01 **[now]** | One crop first, then a second input for the coming Mixer. |
| O02 | Tomato | O02 reward **[now]** | Arrives with the Mixer, so the next objective can use two raw lanes. |
| O01 available; used at O07 | Basil | Seed Shop offer after O01, 3 currency purchase **[now]**; campaign requirement at O07 **[proposal]** | Keeps early purchase optional during Demo; later turns it into a useful branching ingredient. Show its prerequisite before O07. |
| East Field purchase before O04 | Potato | Restoration reward **[now]** | New farmable area and Cutter are taught together in the existing slice. |
| O09 | Milk | Dairy Hamlet Trade Building output **[proposal]** | First import uses a finished-food line and immediately opens Creamy Tomato Soup. |
| O14 | Honey | Orchard Apiary Trade Building output **[proposal]** | Second import gives the earliest Air line a new job. |

No Egg, Meat, Seafood, Spice, Herb, or Salt import is required. Adding them now would widen the dependency graph without a distinct lesson.

## 7. Machine Unlock Order

| Machine | First use / prior knowledge | First teaching recipe or output; later use | Timing risk and recommendation |
| --- | --- | --- | --- |
| Farm Plot + Harvester + Belt **[now]** | Start; no previous automation assumed. | Apple delivery; every later crop. | Show one short path to Market and the Harvester's covered plot before exposing many build choices. |
| Processor **[now: O01 reward]** | Player has watched one steady food belt. | Dried Apple with Air at O02; Fries with Heat at O05; Tomato Soup with Moisture at O06; Pickled Onion with Time at O11. | O02 currently stacks first machine, Property source, collector, pipe, and discovery. Keep the authored order for save safety; reveal a short source-to-demand trace and one corrective hint at a time. Do not add another machine here. |
| Basic Mixer **[now: O02 reward]** | Player knows one machine output and the Market. | Vegetable Base at O03; later Dip, creamy soup, relish, meal combinations. | Its two input ports must be unmistakable. O03 is timely because Onion and Tomato are already unlocked. Avoid a three-input recipe while this is the only mixer model. |
| Cutter **[now: O03 reward]** | Player has seen two inputs on the Mixer and has access to East Field. | Two Cut Potatoes at O04; Cut Potato supports Fries and all later potato meals. | Paired outputs are a new constraint, so O04 should focus on both output belts and blockage before Heat is asked for at O05. |
| Trade Building **[proposal: after O08]** | Player can keep two food chains and a finished meal flowing. | Garden Fries & Dip -> Milk at O09; Dried Apple -> Honey at O14. | Too early would feel like a shop; too late would make imports decorative. Its food-input and import-output ports need visible independent buffers. No such building exists now. |
| Advanced Mixer **[GDD planned; not required here]** | Would need players comfortable with two-input combinations. | No required campaign recipe in this proposal. | Adding it before a real three-ingredient planning problem would be feature-driven progression. Reassess after the seven-chapter campaign is tested. |

## 8. Property Unlock Order

| Order | First recipe and why it teaches well | Later reinforcement / complexity spike |
| --- | --- | --- |
| Air at O02 **[current use]** | Dried Apple is a recognizable drying transformation of an already familiar Apple. One source -> Collector -> Pipe -> Processor demand is the entire lesson. | O14/O15 revive the Dried Apple line for Honey. O02 is still a steep first Property task; targeted port feedback matters more than extra text. |
| Heat at O05 **[current use]** | Cut Potato to French Fries is intuitive, and the Cutter output is already present. | Fries feed O08, O13, and O17. A second Processor near another source would create a later capacity/layout choice; the campaign does not require a new Heat recipe just for coverage. |
| Moisture at O06 **[proposal use, source now]** | Vegetable Base to Tomato Soup is legible after the Mixer lesson. The player repeats the known Property connection pattern with a new source. | Soup enters O10 and O17. O06 adds one network, so it should avoid a simultaneous land or trade introduction. |
| Time at O11 **[proposal use, source now]** | Onion to Pickled Onion has a clear preservation meaning and uses an existing crop line. | Pickled Onion -> Relish -> Relish Fries brings Time into a longer chain. This is the first point where several Property networks may be live at once. |

**Rule boundary:** Today all four fixed sources are present and the Processor handles one Property per recipe. “Unlock order” above is a teaching and proposed presentation order, not a current technical gate. If design later needs literal source gating, specify how existing saves and already-placed connections migrate. Teach source, collector, pipe, and demand together on one small Air path before asking players to compare capacity. A multi-Property Processor is not needed for this campaign; using separate Processors keeps current rules and makes Heat, Moisture, Time, and Air visually traceable. A later multi-Property expansion should be tested as its own mechanic, not slipped into a recipe row.

## 9. Trading Village Progression

**All village behavior in this section is NEW PROPOSAL.** The current Market buys delivered food for currency; it cannot emit imported ingredients. The Trade Building would take a specified food from a belt, buffer a completed exchange safely, and emit an imported FoodItemData identity on a separate belt. The village contract should be visible at the input/output ports, pause when the import output is blocked, and never consume food for an unavailable return. A simple claim/return animation makes the transaction feel like the factory supplied a neighbor. Currency should remain the separate Market/land/seed economy.

| Village and introduction | Proposed small playtest contract | Planning problem and next use | Why it is distinct |
| --- | --- | --- | --- |
| Dairy Hamlet, unlocked after O08; first exchange O09 | Garden Fries & Dip x1 -> Milk x1. | Deliver the first Milk to prove the return belt, then split future Milk toward Creamy Tomato Soup. The trade site should sit beyond the current meal output, so the existing line extends. | Turns a two-chain finished meal into the first imported ingredient. It teaches a closed production loop, not a menu purchase. |
| Orchard Apiary, unlocked after O13; first exchange O14 | Dried Apple x2 -> Honey x1 as an initial test ratio. | Reuse the old Apple/Air line, branch it toward the Apiary and Honeyed Dried Apple, and preserve one outlet for ordinary sales. | Rewards maintenance of an early factory and competes for a previously simple output. Spatial separation from Dairy Hamlet increases routing scale. |

The exact exchange ratios, village footprints, locations, transport interval, input capacity, output capacity, and unlock currency are undecided and require playtesting. Do not use a ratio to manufacture grind. Imported Milk and Honey should be saved by stable food ID, appear in the Recipe Book and Belt visuals, and be validated on world reload. Village access is not currently an UnlockKey consumer or a region mechanic; that integration must be designed and implemented before these objectives can run.

## 10. Recipe Progression

Recipe IDs R01-R04 below describe **implemented Demo data**. R05-R15 are **NEW PROPOSALS**. “P” is Processor, “M” Basic Mixer, and “C” Cutter. The single-Property Processor rows and two-food Mixer rows respect the current recipe shapes. A meal “set” made by a Mixer means combining prepared components into one packed food output; playtest whether the machine presentation makes that understandable. There is no hard recipe lock in the current system: order hints introduce a combination, and discovery still happens only on first actual production.

| Recipe, stage, status | Ingredients -> output; machine / Property | Role and earlier-chain reuse | Distinct layout question |
| --- | --- | --- | --- |
| R01 Dried Apple, Ch1 **[now, required]** | Apple -> Dried Apple; P / Air | First property transformation of first crop. | How does a food belt meet a separate Property demand? |
| R02 Vegetable Base, Ch2 **[now, required]** | Tomato + Onion -> Vegetable Base; M / none | First paired raw crop input. | How are two farm belts aligned with separate ports? |
| R03 Cut Potato, Ch2 **[now, required]** | Potato -> 2 Cut Potatoes; C / none | First paired output, later Fry line. | Can both exits remain clear? |
| R04 French Fries, Ch2 **[now, required]** | Cut Potato -> French Fries; P / Heat | Reuses Cutter output. | Which Cut Potato outlet feeds processing versus Market? |
| R05 Tomato Soup, Ch3 **[proposal, required]** | Vegetable Base -> Tomato Soup; P / Moisture | Reuses R02 and teaches Moisture. | How can Vegetable Base reach Soup while still serving other recipes? |
| R06 Tomato Basil Dip, Ch3 **[proposal, required]** | Vegetable Base + Basil -> Tomato Basil Dip; M / none | Makes the existing Basil shop useful and shares R02. | Add a branch to a productive Vegetable Base line. |
| R07 Garden Fries & Dip, Ch3 **[proposal, required]** | French Fries + Tomato Basil Dip -> Garden Fries & Dip; M / none | Joins R04 and R06; becomes Dairy trade good. | Converge a potato/Heat line and a tomato/Basil line without blocking either. |
| R08 Creamy Tomato Soup, Ch4 **[proposal, required, village-gated]** | Tomato Soup + imported Milk -> Creamy Tomato Soup; M / none | Reuses R05 and first trade output. | Route village import back into a known factory chain. |
| R09 Pickled Onion, Ch5 **[proposal, required]** | Onion -> Pickled Onion; P / Time | Reuses a starting crop while teaching Time. | Share Onion between Vegetable Base and a new Property path. |
| R10 Tomato Relish, Ch5 **[proposal, required]** | Vegetable Base + Pickled Onion -> Tomato Relish; M / none | Reuses R02 and R09. | Feed a heavily shared intermediate to a second Mixer. |
| R11 Relish Fries, Ch5 **[proposal, required]** | French Fries + Tomato Relish -> Relish Fries; M / none | Reuses Heat/Cutter and Time branches. | Merge outputs from two Property-supported chains. |
| R12 Honeyed Dried Apple, Ch6 **[proposal, required, village-gated]** | Dried Apple + imported Honey -> Honeyed Dried Apple; M / none | Revives R01 and second trade output. | Split Dried Apple between barter, dessert, and sale. |
| R13 Village Lunch, Ch7 **[proposal, required, village-gated]** | Creamy Tomato Soup + Relish Fries -> Village Lunch; M / none | Combines R08 and R11; four machine transformations at the longest dependency path. | Keep a dairy/soup chain and a potato/relish chain supplied together. |
| R14 Caramelized Onion, Ch5+ **[proposal, optional]** | Onion -> Caramelized Onion; P / Heat | A familiar alternate use of Onion with an already known source. | Optional branch competes with Pickled Onion and Vegetable Base for Onion. |
| R15 Apple Cream Cup, Ch6+ **[proposal, optional, village-gated]** | Dried Apple + imported Milk -> Apple Cream Cup; M / none | Crosses the earliest Air line with the first village. | Optional dessert competes with Honey barter for Dried Apple and with soup for Milk. |

The nine required additions each have an objective or a named trade use. The two optional discoveries have no main-order gate and create real allocation choices, so they can be cut if playtesting finds the branch load too high. No recipe has identical machine/input/Property matching keys, which matters because the current catalogs refuse ambiguous matches.

## 11. Recipe Tech Tree

The graph is left to right: raw or imported food enters on the left; recipe outputs advance to the right. Brackets show machine and Property gates. Solid nodes are required; dashed nodes are optional. Village exchange is a production edge, not a recipe. This is also translatable to a sheet with columns “input A / input B / Property / machine / output / required / first objective.”

~~~
Apple -> [P Air] Dried Apple -----------------------> [M + Honey] Honeyed Dried Apple
             |                   \                    [M + Milk] Apple Cream Cup (optional)
             +-> [Apiary trade] Honey ----+
Tomato + Onion -> [M] Vegetable Base -> [P Moisture] Tomato Soup
                     |                         + [Milk] -> [M] Creamy Tomato Soup --+
                     + Basil -> [M] Tomato Basil Dip                                |
                     + Pickled Onion -> [M] Tomato Relish --+                        |
Onion -> [P Time] Pickled Onion --------------------+     |                        |
Onion -> [P Heat] Caramelized Onion (optional)           |                        |
Potato -> [C x2] Cut Potato -> [P Heat] French Fries ----+--> [M] Relish Fries ---+--> [M] Village Lunch
                                             + Tomato Basil Dip -> [M] Garden Fries & Dip
                                                                  | [Dairy trade]
                                                                  +--> Milk
~~~

Trade links are: Garden Fries & Dip -> Dairy Hamlet -> Milk, and Dried Apple -> Orchard Apiary -> Honey. R05/R09 are Property-gated in the design's teaching order; R08/R12/R13/R15 are village-gated by ingredients. All Processor, Mixer, and Cutter nodes are machine-gated by current or proposed access. R14/R15 are optional. A printed or spreadsheet version should keep full names and direction; it should not imply that the present Recipe Book enforces these gates.

## 12. Region / Land Expansion

The current Demo has one meaningful campaign land gate: East Field is made available after O03, then purchased for 5 currency to obtain Potato before O04. West Meadow, North Terrace, and South Orchard are already present in the scene with prices 3, 4, and 4, but no order gate. These prices and the East Field sequence are **current content**, not proposed balance changes.

| Land | Proposed campaign moment | Space pressure and purpose | Implementation distinction |
| --- | --- | --- | --- |
| East Field **[now]** | O03 access, purchase before O04. | Potato farming and Cutter output routing need more cultivated area. | Preserve the authored gate and purchase. |
| West Meadow **[proposal gate]** | After O07, before the O08 convergence. | A second Tomato/Basil production area lets Vegetable Base serve Soup and Dip while Fries stay intact. | The scene currently allows its purchase earlier. A later campaign gate would change progression data and needs save migration rules. |
| North Terrace **[proposal gate]** | After O10, before O11-O13. | Separate Time processing and a second Vegetable Base branch from the existing soup line. | Currently purchasable without this objective gate. Test whether the usable plot area actually solves this layout before gating it. |
| South Orchard **[proposal gate]** | After O13, before the Apiary branch. | Expand Apple cultivation for Honey exchange and dessert while keeping early Air production useful. | Currently purchasable without this objective gate. The proposed Apiary location is not authored in the scene. |

Restored land currently constrains where Farm Plots can be placed; it is not a general factory-building boundary. Thus a new region is primarily a crop-space reward under today's implementation. It cannot truthfully promise more machine or pipe space without a separate placement rule and scene review. Keep all three later gates **provisional** until a map playtest measures plot occupancy, belt crossings, and route lengths. If players can solve O08/O13 comfortably without those regions, use the purchases as optional relief instead of mandatory order rewards. Do not raise prices merely to stretch the campaign.

## 13. Tutorial & UX Analysis

Each row states the building task that should carry the lesson. Short explicit cues belong at the first unfamiliar port or constraint; the objective itself should supply the motive.

| Stage | Learn by building; likely confusion | Implicit goal, explicit feedback, and stuck recovery | Reward timing |
| --- | --- | --- | --- |
| O01 first harvest | Plant Apple, connect Harvester and Belt to Market. A player may not know plot coverage or belt direction. | The delivery objective provides the reason. Show coverage/direction in placement preview and a specific no-output hint at the blocked tile or Harvester. | Processor and Onion after three actual deliveries. |
| O02 Air processing | Extend Apple to Processor and connect source -> Collector -> Pipe -> demand. Food input and Property input may look interchangeable. | Dried Apple is the implicit reason to process. Explicitly name the missing input on the Processor and highlight the Air path when stalled; reveal recipe hint before requiring the output. | Mixer and Tomato only after Dried Apple is produced and delivered. |
| O03 two crops and O04 Cutter | Align Tomato and Onion with two Mixer ports; then clear both Cutter exits. Players may expect one input or one output. | The new recipe and Cut Potato order carry the lesson. Show per-port food demand, both output states, and a blocked-output reason. East Field purchase needs an obvious path to Potato. | Cutter and East Field after Vegetable Base; Cut Potato objective itself has no new machine reward, so its completed line must visibly enable Fries. |
| O05 Heat and O06 Moisture | Reuse Cut Potato at a Heat Processor, then branch Vegetable Base into a Moisture Processor. A full source may hide an unconnected pipe or mismatched Property. | Show one missing Property and one missing food reason at the machine; avoid a broad tutorial popup. The O05 Demo-complete cue would become a chapter transition in a future campaign. | Celebrate Fries before introducing one new Property at O06; grant Basil/next recipe guidance after Soup. |
| O07-O08 split and join | A shared Vegetable Base line serves Soup and Dip; Dip later joins Fries. Players may starve one branch or deadlock an output. | The recipe preview should show upstream foods and per-port readiness. A contextual hint can suggest a splitter or a second production line, without prescribing one layout. | West Meadow access only if playtests show real crop-space pressure; Dairy Hamlet after the joined meal. |
| O09-O10 first village | Feed a prepared dish into Trade Building, receive Milk, and return it to the factory. Players may mistake trade for a currency shop or lose an item at a blocked import output. | Show input cost, pending exchange, output capacity, and exact reason for pause on the building. The Milk delivery is an explicit round-trip proof; Creamy Soup then makes the import useful. | Milk recipe hint after first Milk; North Terrace access only if justified by space use. |
| O11-O13 Time and shared intermediates | Add one Time line, then share Onion and Vegetable Base across longer chains. The player may not understand which consumer is starving. | Give the Pickled Onion recipe hint, then show current inputs at each waiting Mixer. Objective targets are only two or three because the lesson is routing, not a long quota. | South Orchard/Apiary access after Relish Fries, subject to map test. |
| O14-O17 second village and finale | Reopen Dried Apple for Honey, maintain two outputs, then join soup and fries for Village Lunch. Players may confuse an optional Apple sale with required trade flow or assume stored goods count before order activation. | Show per-requirement progress, active-order timing, and each trade queue. A factory overview or pinned recipe chain should identify the missing upstream producer. O17 has two visible requirements. | Honey after first return, completion celebration after both final foods. |

Current Market progress is counted from deliveries after an order becomes active. A future campaign UI must say this plainly; otherwise saved stockpiles and prior sales will look ignored. Recipe discovery also occurs on production, so a hint should expose inputs and machine without falsely marking a recipe discovered. A proposed chapter card can summarize the next use of a new unlock, but the build screen and machine feedback must remain the primary teaching surfaces.

## 14. Rebuilding / Scaling Moments

| Moment | What changes in the old factory | Why it is earned and how to avoid punishment |
| --- | --- | --- |
| O05 Cutter output -> Fries | One Cut Potato outlet that formerly went to Market can feed a Heat Processor. | The player has just learned paired outputs. Keep the existing Market route useful as an overflow or temporary order outlet; do not require tearing out both exits. |
| O06-O07 Vegetable Base branch | The former one-destination Base line now feeds Soup and Dip. | Add a branch or duplicate the Mixer if the first layout has no room. The game should show which consumer is waiting, and land pressure should remain gentle until a region actually helps. |
| O08 prepared-food join | Fries and Dip converge in one Mixer. | This is the first deliberate multi-chain join. Keep targets small and the two ingredient paths legible; the player can extend or build a parallel outlet. |
| O09 Dairy trade return | Finished meal leaves the factory; Milk comes back on another belt. | Give the player a visible import port and back-pressure state. No exchange should consume a dish if Milk cannot be emitted. The old Fries/Dip line becomes infrastructure rather than obsolete work. |
| O11-O13 shared Onion, Base, and Fries | A familiar crop and intermediate serve three destinations, while Time joins existing Heat/Moisture. | This is the main optimization window. Extra farm plots, separated branches, and modest buffers should work; avoid forcing a perfect balanced splitter or global rebuild. |
| O14-O15 Apple line reopens | Dried Apple now supports Honey barter and an optional Milk dessert as well as sale. | The earliest network gains a late use. The Apiary should accept a low initial quantity so existing Apple capacity can prove the idea. |
| O16-O17 concurrent supply | Creamy Soup and Relish Fries must both reach Market; Village Lunch consumes them for the finale while Honeyed Apple remains separate. | Count completed deliveries from live production and communicate both goals. Provide enough crop land and routing room, but do not require a rate threshold that the order system cannot measure. |

Existing building Move/blueprint behavior and active-item state require careful implementation review before presenting large-scale relocation as the intended solution. The campaign should be completable by extending or adding lines; a compulsory teardown would make the earlier efficient layout feel like a mistake.

## 15. Difficulty Curve

The values below are **design targets**, not measurements of the present scene. “Depth” counts recipe transformations from a raw crop or imported food to the requested output; “networks” counts distinct Property types that may need to remain useful in that stage. The recipe graph can use more machines in parallel than a single path count shows.

| Stage | Simultaneous food inputs / outputs to watch | Longest recipe depth; machines on that path | Property networks in play | Space, routing, throughput target |
| --- | --- | --- | --- | --- |
| O01 | 1 / 1 | 0; Harvester only | 0 | One short belt, three deliveries. |
| O02 | 1 / 1 | 1; Processor | 1 Air | One food lane plus one Property line, two deliveries. This is the first major spike. |
| O03-O04 | 2 / 1, then 1 / 2 Cutter exits | 1; Mixer or Cutter | Air known, not needed for these orders | Two input routes then paired outputs; two of each requested food. |
| O05-O06 | 1 / 1 at Market; shared upstream options | 2; Cutter + Processor, or Mixer + Processor | Heat, then Moisture, with Air reusable | Extend one known line per objective; two deliveries. |
| O07-O08 | 3-4 crop foods / 2-3 branch products | 2-3; Mixer chains plus Fries path | Air, Heat, Moisture available | Shared Base and first cross-chain join; two deliveries each. This is the first spatial pressure point. |
| O09-O10 | Finished trade good + Milk / Milk and Soup | Up to 3; trade building adds a separate conversion step | Same known types | One return route; one Milk then three Creamy Soups. Trade is a new layout class, not a long quota. |
| O11-O13 | Onion, Tomato, Potato, optional Basil / multiple foods | Up to 3 recipe steps to Relish Fries from a raw crop | Add Time; four available | Split Onion/Base, join Relish/Fries; two, three, three deliveries. This is the densest midgame span. |
| O14-O15 | Apple, Honey, existing chains / barter and dessert | Up to 2 recipe steps plus trade | Air revived alongside prior types | Reopen an old line; one Honey then three desserts. |
| O16-O17 | Two finished requirements / two Market counters | Up to 4 to Village Lunch | Four potentially active | Sustained concurrent routing, two of each at O16; three of each at O17. No rate target. |

**Spike review and revisions applied to this version:** O02 is fixed by existing content, so the proposal adds diagnostic feedback instead of another system. O04 deliberately has no Property lesson while both Cutter exits are learned. O06 introduces only Moisture; O07 and O08 then practice branching and joining separately. O09 tests one imported item before O10 uses it. O11 introduces Time alone, then O12/O13 deepen it. O14 returns to a simple older line before the two-output finale. The O17 target is a modest paired delivery; a measured throughput challenge would require an explicit rate/window feature and is excluded.

## 16. Risks and Problems

This section critiques the complete O01-O17 draft. The changes in the right column are reflected in Sections 5-15 and the final recommendation below.

| Weakness or realistic failure mode | Revision or decision |
| --- | --- |
| O02 combines first Processor, first Property supply, pipes, and recipe discovery. It may be the hardest single transition despite its early position. | Keep the authored order for existing saves, but teach the connection in a small visible trace and show exact missing-input feedback. Do not add a second new system there. |
| O04 Cut Potato has no unlock and could feel like a filler delivery. | Frame it as clearing both Cutter outputs and show that Cut Potato is the ingredient for O05 Fries. Two units are enough to prove this. |
| O06-O08 could feel like three Vegetable Base variations in succession. | Give each a different factory verb: Property branch, shared intermediate branch, and two-line join. If playtests cannot distinguish those actions, cut R06/O07 and use another trade good rather than keeping a recipe for count. |
| Time appears late, while Air and Moisture each have only one required direct recipe. | Use the old Air line again for Honey, Soup as an intermediate twice, and Time as a preservation branch feeding Relish Fries. This creates repeated value without premature multi-Property recipes. If Time still feels decorative, move O11 earlier after Dairy trade in a later iteration. |
| A second village may become an expensive shop with a production animation. | Both village contracts consume factory-made food and produce physical ingredients. Honey reactivates Dried Apple; Milk feeds a core Soup chain. If that loop is not spatially interesting in playtest, cut Apiary from the main path. |
| O11-O13 contain three low-quantity deliver-X goals and may feel repetitive. | The stage deliberately changes topology from new Property to shared intermediate to cross-line join. Add visual previews of the next dependency; avoid increasing counts. If repetition persists, combine O12 and O13 as one paired order after verifying it does not stack too many lessons. |
| O16 and O17 repeat multi-output supply and might feel like stockpiling. | O16 asks for two existing products concurrently; O17 consumes those products into a new meal while maintaining Honey dessert. Keep both only if the second changes layout and has a clear celebration. Otherwise cut O16. |
| Optional R14/R15 compete for ingredients but do not serve a required objective; they may add catalog clutter. | Treat them as two discovery tests outside the critical path. Remove either if the optional branch causes ambiguity or no meaningful tradeoff. |
| “Unlocked recipe” language can mislead: current Recipe Book records production, not formal unlock; all four Property sources already exist. | Use “introduced/hinted” in player copy. Do not claim source or recipe hard gates exist. A true lock would need save-compatible data and UI rules. |
| West/North/South currently lack order gates, and additional regions may not add machine space under present placement rules. | Make proposed gates conditional on map and save tests. For the MVP, keep these regions' current purchase behavior and omit campaign dependency on them. |
| Future order insertion or changed IDs may break saved progress, and a one-active-order Market does not measure continuous throughput. | Append new orders after O05, preserve IDs, define save migration for new unlocks, and use low cumulative counts. Reserve timed/rate orders for a separate validated system. |
| Trading, Milk/Honey identities, and two village sites do not exist yet. They are the largest scope and persistence risks. | Build one village and one import first; verify blocked output, save/load, and active-order feedback before proposing the second as committed content. |
| “Village Lunch” made by a Basic Mixer may read as a plated meal rather than a mixed ingredient. | Present it as a packed lunch assembled from two prepared components; validate the icon/name and machine animation in a player test. If it remains implausible, choose a more mixer-like final dish without adding a new machine. |

## 17. Revised Final Progression

The revision favors a narrow vertical slice followed by reuse. It retains the five authored orders and four authored recipes, then adds twelve main objectives and nine required recipes in four waves. These are design decisions for evaluation, **not authored project content**.

| Wave | Objectives and lesson | Recipe / ingredient / access result | Decision after critique |
| --- | --- | --- | --- |
| Existing foundation | O01-O05: harvest, Air Processor, two-input Mixer, two-output Cutter, Heat Fries. | R01-R04; Apple, Onion, Tomato, Basil offer, Potato; Processor, Mixer, Cutter; East Field. | Preserve current data and IDs. Add clearer feedback around O02/O04 when implemented. |
| Shared food lines | O06-O08: Moisture Soup, Basil Dip branch, Fries/Dip join. | R05-R07; no new raw crop; proposed West Meadow access after O07 only if land is truly needed. | Each objective changes one topology pattern. Trade waits until the player can supply a finished food. |
| First network trade | O09-O10: Milk round trip, then Creamy Soup. | Dairy Hamlet, Milk, R08; proposed North Terrace access after O10 only if tested. | One village, one import, one immediately useful recipe. This is the recommended MVP endpoint. |
| Deeper shared chains | O11-O13: Time preservation, Base/Onion sharing, Relish/Fries join. | R09-R11; proposed South Orchard/Apiary access after O13 if tested. | Reuse existing crops and machines; no Advanced Mixer or multi-Property demand. |
| Reuse and completion | O14-O17: Honey trade, dessert, concurrent supply, packed lunch plus dessert. | Orchard Apiary, Honey, R12-R13; campaign completion. | Revisit Apple/Air, then run known chains together. Drop O16 if it repeats the finale in practice. |

**Proposed progression rules:** Hints reveal a plausible next combination; discovery records actual first production. Machine availability and crop access follow current authored rewards through O05. Moisture/Time and the later land sequence are teaching order and conditional campaign proposals, not assertions that current sources or region gates enforce them. Two-input recipes remain compatible with the Basic Mixer shape; every required dish can be produced by current machine *types*, provided the proposed data and trade system are built. Paired requirements use the current FoodOrder model, but village rewards and new unlock keys need implementation and persistence design. Optional R14/R15 may be discovered after their ingredients become available and never block the main campaign.

## 18. Recommended MVP Campaign Cut

Build **O01-O10 as the first playable campaign**: keep the existing O01-O05 content, add O06 Tomato Soup x2, O07 Tomato Basil Dip x2, O08 Garden Fries & Dip x2, O09 Milk x1, and O10 Creamy Tomato Soup x3. This reaches a real automation-and-trade loop without depending on the final four-Property factory. The last five objectives here are proposals; O05 currently ends the Demo and would need a continuation path.

| MVP element | Include | Defer |
| --- | --- | --- |
| Objectives | 10 sequential objectives: 5 current + 5 proposed. Keep current IDs/order; append O06-O10. | O11-O17, paired-order finale, throughput challenges. |
| Recipes | R01-R04 current plus R05 Tomato Soup, R06 Tomato Basil Dip, R07 Garden Fries & Dip, R08 Creamy Tomato Soup: 8 required recipes total. | R09-R13 and both optional R14/R15. |
| Ingredients | Current five raw crops plus one imported Milk. Basil becomes meaningful at O07. | Honey and any other imports. |
| Villages | One Dairy Hamlet exchange: Garden Fries & Dip -> Milk, with a physical Trade Building and safe blocked-output behavior. Ratio and site require playtest. | Orchard Apiary and second trade contract. |
| Machines and Property | Existing Farm Plot, Harvester, Belt, Processor, Basic Mixer, Cutter; teach Moisture after current Air and Heat. | Time objective, Advanced Mixer, multi-Property recipes, new machine type. |
| Regions | Retain existing East Field gate and current optional West/North/South purchases. Test whether West Meadow needs a campaign gate, but do not depend on it in the MVP. | New region-gating rules and village-specific land gate until map/placement/save implications are proven. |
| UX and persistence | Recipe hints versus actual discovery, per-port failure reasons, trade input/output state, Milk serialization, active-order progress, and a clear post-Demo chapter transition. | Broad campaign framework, timed orders, rate scoring, large tutorial text sequence. |

The MVP has a clear completion test: a fresh save can produce the four current foods, route Vegetable Base to Soup and Dip, join Dip with Fries, trade that meal for Milk without loss on a blocked output, and make Creamy Tomato Soup after a reload. The implementation work is substantial because trading is wholly new; do not treat the design document as evidence that the playable loop exists. Validate the first village and the split/join layout in the running Editor before committing to the full O11-O17 arc.
