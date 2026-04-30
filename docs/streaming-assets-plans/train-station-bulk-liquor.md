# Train Station Bulk Liquor Sales

## Goal

Add a way to sell each liquor tier in bulk through a train-station-linked warehouse storage location.

## Streaming Assets First

Start with data and location setup:

- train station interaction content
- warehouse / storage place definition
- bulk liquor trade items
- tier-based sale options
- repeatable trade or shipment actions

## Patch Only If Needed

Use code only if the station and warehouse need a custom inventory-transfer rule that cannot be expressed through normal item, storage, or quest actions.

## Task Breakdown

1. Define the sale point:
   - directly on train station
   - separate warehouse storage node attached to train station
2. Add bulk-sale actions for each liquor tier.
3. Decide whether bulk sales consume:
   - inventory items directly
   - stored crate-style items
   - warehouse stock states
4. Gate access if needed by:
   - storage ownership
   - warehouse ownership
   - station connection quest
5. Make the action readable as logistics, not as a normal storefront sale.

## Desired Outcome

Players should be able to move liquor at scale through rail logistics instead of relying only on ordinary business sales.

