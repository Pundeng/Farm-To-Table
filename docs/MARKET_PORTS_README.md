# 5x5 Market perimeter inputs

`MarketPortLayout` in `Assets/_Project/Scripts/Food/MarketReceiver.cs` derives the Market footprint and its twelve inputs from the serialized Market anchor. The occupied footprint is 5x5. Each side exposes only its center three edge cells; corner-edge positions are not ports. A port records its Market edge cell, outward side, external Belt cell, and required inward travel direction.

Every receiver delegates to one `MarketReceiver` and one `MarketInventory`, so valid food entering any port follows the existing delivery handling, sell value, currency gain, active objective progress, latest-delivery feedback, and unlock/order behavior. There are no per-port inventories. Belts facing away from the Market do not deliver.

The Market reserves only its 25 footprint cells. All free cells outside those cells remain normal world construction space, including the surrounding margin within the reserved Hub parcel and each of the twelve external Belt input cells. The Market visual Sprite or prefab is parented under the Market and is independent of occupancy and input coordinates. Decorative children can extend visually over neighboring cells without reserving them.

Assign Market art in the Demo scene by selecting **Market** and setting **Visual Sprite** or **Visual Prefab** in the Inspector. For machines, select **Building Placement**, expand **Building Options**, choose a **Building Definition**, then set **Visual Sprite/Visual Prefab**, **Visual Scale**, **Visual Offset**, and **Visual Sorting Offset**. Art can be swapped without editing gameplay footprint or port layout.

Focused EditMode tests cover all twelve unique ports, three positions per side, corner exclusion, delivery through multiple lanes, Belt direction, Market occupancy, and available external lane cells. The running Unity Editor still needs to verify the visuals at camera zoom levels and interactive deliveries from all four sides.
