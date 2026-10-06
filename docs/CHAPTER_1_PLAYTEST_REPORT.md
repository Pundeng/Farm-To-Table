# Chapter 1 Playtest Report

**Playtest date:** October 5, 2026  
**Tester:** Hazel  
**Evidence:** Tester observations and stopwatch/notes screenshots.  
**Completion:** All ten objectives completed; a separate timing entry for O2 was not recorded.  
**Build/version and fresh-save status:** Not recorded.

This report documents the tester's playthrough. Findings have not been independently reproduced, and no code fixes were made as part of this report. Random map generation is outside this QA task.

## Objective timings

Lap 1 corresponds to O1. Because the tester missed recording Roasted Carrot, subsequent completed laps are provisionally mapped to O3–O10. Lap 10 (00:03.15 in the screenshot) is an unfinished post-objective interval and is excluded.

If the stopwatch continued through O2 without a lap being recorded, Lap 2 includes both O2 and O3. Its 05:28.39 must therefore not be treated as an isolated O3 production time without confirmation.

| Objective | Required amount | Completed | Stopwatch lap | Recorded interval | Pacing assessment |
|---|---:|---|---:|---|---|
| O1 Carrot | 30 | Yes | 1 | 00:52.53 | Shortest recorded interval. Could suit an introductory objective; tester rating not provided. |
| O2 Roasted Carrot | 40 | Yes | Not recorded | Unknown | Cannot assess separately. |
| O3 Tomato Sauce | 70 | Yes | 2 | 05:28.39* | Timing may include O2; no independent pacing conclusion. |
| O4 Potato Slice | 90 | Yes | 3 | 05:14.01 | Tester rating not provided. |
| O5 French Fries | 150 | Yes | 4 | 12:14.34 | Longer than the preceding interval; setup/waiting breakdown unavailable. |
| O6 Tomato Soup | 220 | Yes | 5 | 08:33.35 | Tester rating not provided. |
| O7 Loaded Fries | 300 | Yes | 6 | 13:31.05 | Setup/waiting breakdown unavailable. |
| O8 Garden Lunch | 400 | Yes | 7 | 23:13.11 | Longest recorded interval; investigate the cause before changing quantities. |
| O9 Egg | 500 | Yes | 8 | 12:24.72 | Trading-specific friction was not separately recorded. |
| O10 Tomato Omelette | 700 | Yes | 9 | 15:37.05 | Setup/waiting breakdown unavailable. |

*O3 attribution is provisional for the reason above. Intervals are wall-clock lap durations, not measured machine production times.

The stopwatch shows approximately **1 hour 37 minutes** of elapsed session time. The nine completed lap intervals sum to **1:37:08.55**. Do not add an estimated O2 duration to that sum; it may already be included in Lap 2.

For each objective, next-step clarity, unlock confusion, rebuilding effort, meaningful scaling, Belt/machine/Market friction, and progress-counter behavior were not recorded separately. The overall findings below should not be assigned to a specific objective without further evidence.

## Bugs

All entries below are tester-reported issues. Reproduction procedures are verification steps to follow, not independently confirmed reproductions. Root causes and fixes remain unconfirmed.

### B1 — Collector placement blocked where Property areas overlap

- **Observed:** A Collector cannot be installed in an area where Properties overlap.
- **Expected:** A free, otherwise valid cell adjacent to the intended Source should support Collector placement, with clear Source ownership. An overlapping visual area should not by itself block construction.
- **Verify:** Find overlapping Property areas, select the intended Source, and attempt Collector placement in a free adjacent cell. Record the cells, Source types, existing connections, and rejection message.
- **Impact:** Makes nearby Sources difficult to use and adds routing frustration.
- **Follow-up:** Distinguish overlapping influence visuals from occupied cells and actual network conflicts. Do not infer that collectors should be placed on occupied Source cells.

### B2 — Pipes cannot pass beside a different Property

- **Observed:** Routing a Pipe next to another Property is blocked.
- **Expected:** A Pipe should be able to pass through an otherwise valid empty cell without unintentionally joining or changing ownership to the neighboring network.
- **Verify:** Extend a connected Pipe through free cells alongside a different Property Source/network. Compare with the same route away from that Property.
- **Impact:** Forces detours and wastes the already limited construction space.
- **Follow-up:** Check proximity-based placement restrictions separately from actual connection/merging restrictions. Preserve Source ownership and capacity rules.

### B3 — Mixer reports missing ingredients despite ingredients being present

- **Observed:** The Mixer displays a missing-ingredient message even when ingredients appear to be available.
- **Expected:** Feedback should accurately distinguish a missing ingredient, an item still waiting on a Belt, an incompatible recipe pair, and a blocked output.
- **Verify:** Feed the required ingredients through the intended inputs, inspect the internal slots and Belt contents, and compare the message with the actual state. Record the recipe and item IDs.
- **Impact:** Makes it unclear whether the factory is incorrectly connected or the message is wrong.
- **Follow-up:** Confirm whether this is stale feedback, an input-transfer problem, or a recipe mismatch. The observation alone does not establish the cause.

### B4 — Saving fails with an object-reference error

- **Observed message:** `Save failed: object reference not set to instance of an object`
- **Expected:** Saving should capture the factory and progression successfully. A failed save must not corrupt a previously valid save.
- **Verify:** Save the affected factory, capture the complete Console stack trace, then test a minimal factory and gradually reintroduce buildings/connections.
- **Impact:** Prevents reliable preservation of progress during a roughly 97-minute playthrough.
- **Priority:** Highest priority in this report.
- **Follow-up:** Identify the failing reference from the stack trace; retest Save/Load with active machines, stored ingredients, trading, and connected Property networks.

### B5 — Corrupted text in the rotation/cancel instruction

- **Observed:** The rotation / Esc-cancel guidance contains an unexpected garbled character or fragment, described in the notes as “쨋.”
- **Expected:** The instruction should display readable text, for example `R: Rotate | Esc: Cancel`.
- **Verify:** Enter the relevant placement mode and inspect the exact instruction. Record whether the issue affects other labels or only this string.
- **Impact:** Reduces instruction clarity and makes the interface look broken.
- **Follow-up:** Check the source string, encoding, and font support. Exact cause is unknown.

## Balance

### BA1 — Available map space feels too small

The tester reported that the map feels small. This is a construction-space observation, not a confirmed map-dimension defect. Larger production chains need room for machines, Belt turns, Property routing, and later expansion.

Review usable space and expansion access together rather than only enlarging the visible map. Record when the player first runs out of comfortable building space and whether expansion is affordable at that point.

### BA2 — Property density reduces usable construction space

The screenshot notes that there are too many Properties on the map and they obstruct placement. Combined with the small-map feedback and adjacency restrictions, this makes the factory feel crowded.

Review Source density, placement, and clearance around useful building areas. This is feedback on the tested layout; implementing random map generation is not part of this report.

### BA3 — Later objectives need a closer pacing review

Garden Lunch has the longest mapped interval at 23:13.11, followed by Tomato Omelette at 15:37.05. These timings identify objectives to investigate, but do not prove their quantities are too high.

Separate active construction time, troubleshooting, rebuilding, and passive production waiting before changing objective amounts. The tester did not label individual objectives as too easy, good, or too grindy.

## UX annoyances

### UX1 — Processor layout discourages building more machines

The tester described the Processor structure as excessively difficult. Its complexity makes them less willing to build additional Processors, which works against production scaling.

Review input/output/Property port arrangement and connection clarity. The desired challenge should come from managing production, rather than repeatedly struggling to fit each machine into the factory.

### UX2 — A mandatory intermediate Belt step feels inconvenient

The tester found it inconvenient that items must pass through an additional Belt step. The exact affected pair of buildings was not specified.

Identify the affected connection first. Consider direct transfer between compatible adjacent ports, or clearly explain the Belt requirement if it is intentional. Do not remove Belt requirements globally without checking throughput and ownership behavior.

### UX3 — The System menu should pause the game

The tester expects the game to stop when the System button is opened. This is recorded as a requested behavior; no timed verification of the current pause behavior was provided.

Pause simulation while the System menu is open and restore the previous pause state when it closes. Verify crops, machines, Belts, trading, and objective progress stop consistently, while menu input remains usable.

### UX4 — Show the map before unlocking regions

The tester prefers an already visible map with locked regions, allowing players to unlock the next area when their factory needs more space. Revealing the next region only after an unlock makes planning less comfortable.

Show locked region boundaries, costs, and access requirements in advance. Keep building access locked until the region is purchased/unlocked.

### UX5 — Add a flip/mirror option alongside rotation

The notes request flipping in addition to rotation, particularly to make Processor layouts easier to fit.

Review whether existing mirror controls cover individual machines and whether they are discoverable. If new mirroring is needed, ensure the preview, footprint, food ports, Property port, and saved orientation all agree. This is a feature request, not confirmation that every existing mirror tool is absent.

## Suggested changes

| Priority | Suggested action | Acceptance check |
|---|---|---|
| 1 | Investigate and fix the save exception. | Save succeeds; reload restores the factory, items, selected trades, progression, and Property connections. Failed saves preserve the prior valid save. |
| 2 | Resolve Collector overlap and foreign-Property Pipe routing restrictions. | Valid free cells permit construction; passing beside another network does not merge it or change ownership. |
| 3 | Correct Mixer ingredient feedback and rotation/cancel text. | Status reflects actual input/recipe/output state; placement guidance is readable. |
| 4 | Review Processor port layout and expose suitable rotation/mirroring controls. | A second production line can be added without excessive detours or rebuilding. |
| 5 | Improve usable construction space and reduce obstructive Property density. | There is comfortable room for machines, Belt routing, and planned expansion. |
| 6 | Pause consistently from the System menu. | All simulation systems stop and resume consistently; the menu remains responsive. |
| 7 | Make locked regions visible before unlocking. | Players can inspect boundaries, requirements, and costs before deciding where to expand. |
| 8 | Review the forced intermediate Belt connection. | Supported adjacent transfers work safely, or the required Belt step is clear and easy to place. |
| 9 | Recheck O8/O10 pacing after the functional and layout issues are addressed. | Timings distinguish production waiting from troubleshooting; quantity adjustments are based on that evidence. |

## Coverage and remaining checks

| Requested check | Evidence/status |
|---|---|
| Objective amounts | All objectives completed; recorded intervals available except separate O2 timing. Individual pacing ratings remain unrecorded. |
| Unlock order | Preference for visible, demand-driven region unlocking recorded; exact machine/recipe unlock order not evaluated individually. |
| Starting land / expansion | Small-map and Property-crowding concerns reported. |
| Heat / Water accessibility | General Property access/routing issues reported; Heat and Water availability not separately measured. |
| Machines / recipes | Processor layout frustration and misleading Mixer feedback reported. |
| Trading Center | O9 Egg completed; invalid inputs, blocked output, trade switching, and persistence were not separately verified. |
| Market delivery | Objectives completed, but delivery behavior and progress counters were not separately audited. |
| Save / Load | Save failure reported. A successful end-to-end load is not established. |
| Other UX | Intermediate Belt step, System-menu pause, unlock visibility, mirroring, and garbled guidance recorded. |