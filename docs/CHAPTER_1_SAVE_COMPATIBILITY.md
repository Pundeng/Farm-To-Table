# Chapter 1 save compatibility

Version 6 retains its existing DTO shape. Food identity is the case-sensitive ID and kind; IDs must not be reused for a different kind. In-flight food sell values are legacy snapshots and are resolved from the current scene catalog during preflight. Historical delivery counts retain their ID/kind identity and earned currency remains snapshot state; loading does not recalculate past earnings.

Recipes retain their existing structural identity: processing input/property/output, unordered Mixer ingredients/output, and Cutter input/output. Balance values are not recipe identity. Removing or changing a saved recipe structure requires an explicit migration or rejection. Ambiguous signatures are invalid content.

Objective IDs and their completed prefix/order are identity. Append objectives after the existing sequence. Reordering, removing, or changing requirements in a way that invalidates saved progress requires migration or rejection. Individual requirements may be complete while an active objective remains incomplete overall.

Building definition IDs and enum numeric values are stable serialized identity. Renaming IDs, removing definitions, changing enum numbers, or changing footprints so placements conflict requires migration or rejection. Position, rotation, inventory, timers, connections, territory records, and routing cursors are snapshot state. Current process Restore rules govern whether timers remain compatible with edited durations; incompatible timers are rejected before scene replacement.

Version 1 progression-only saves retain existing support; version 6 is the current world format. Other versions retain existing rejection behavior. No format bump is needed for the sell-value resolution policy because the existing field remains readable.

Saving over a primary that validates against current content retains it as `<path>.bak`. An invalid primary does not replace an existing valid backup. Failed temporary writes or atomic replacements leave the primary intact. Backup recovery is manual; this milestone does not add automatic fallback or multiple slots.
