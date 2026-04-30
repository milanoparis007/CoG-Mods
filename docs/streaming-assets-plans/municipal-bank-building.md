# Municipal Bank Building

## Goal

Create a bank building in the same general content style as the school and church work, but make it a non-purchasable municipal building similar to train stations, police stations, or political offices.

## Clarified Direction

The bank should behave like a train station style location:

- the player can purchase from it
- the player can do quests there
- it should place easily in districts

Because of that, the best fit is a train-station-style business-backed special building, not a pure civic-only node.

## Recommended Node Style

Best recommended pattern:

1. a special bank building template in `Buildings.sim`
2. a bank business entry in `Businesses.sim`
3. the business `movesinto` the building, just like train stations do
4. purchases and quests live on the business-side modules
5. district placement lives on the building / template-list side

Why this fits:

- it follows an existing working content pattern
- it is easier to place in districts than a hard-coded civic landmark
- it preserves station-style interaction behavior
- it stays mostly in `StreamingAssets`

## Streaming Assets First

Start in `StreamingAssets`, not code.

Likely data work:

- building entity template
- business wrapper template
- district placement / template-list setup
- art / model slot setup if needed
- interaction / convo hooks tied to the bank

## Patch Only If Needed

Only use patches or methods if the game data layer cannot fully enforce the exact municipal / non-buyout behavior while still preserving train-station-style buying and quest interactions.

Likely fallback cases:

- blocking buyout or ownership transfer in edge cases
- forcing municipal interaction rules if the stock civic template is not enough

## Task Breakdown

1. Make a new special bank building template derived from the existing special-building workflow.
2. Make a business wrapper that `movesinto` the bank building the way train stations do.
3. Mark it so it behaves like a city-controlled landmark rather than a normal buyout business.
4. Decide which actions belong there now versus later:
   - loans
   - high-value deal handling
   - offshore account hookups
5. Add district placement support so it appears cleanly in targeted districts.

## Desired Outcome

The bank should feel like part of the city infrastructure, not like a hidden player business or a normal storefront waiting to be bought.
