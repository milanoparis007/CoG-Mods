# Finance, Accounts, And High-Value Deals

## Goal

Group the money-system tasks that revolve around larger deals, offshore handling, and fake-account creation.

## Scope

- add `45k` and `100k` drug and liquor deals
- offshore accounts pay in full but get a discount when paid with half dirty cash
- connected traits can make fake accounts at a funeral home through a quest
- fake-account quest requires personal info, ink, and paper to begin

## Clarified Direction

The `45k` and `100k` deals are large drug and liquor purchases from other outfits.

Planned split:

- non-connected traits keep the older large-deal path
- connected traits get expanded large-deal content
- connected large deals should scale to roughly `4x` to `5x` the old deal size

## Streaming Assets First

This should begin with content definitions:

- new deal items or trade offers
- funeral-home convo chains
- fake-account quest data
- quest requirements for personal info, ink, and paper
- offshore-account reward / exchange variants
- trait and rank gating where data already supports it

## Patch Only If Needed

Use code only if the discount math or mixed dirty / clean payment rule cannot be represented through normal quest or trade definitions.

Likely fallback cases:

- partial dirty-cash payment logic
- dynamic percentage discounts
- trait-specific branching the data layer cannot express cleanly

## Task Breakdown

1. Add new large deal tiers for `45k` and `100k`.
2. Treat those deals as drug and liquor sourcing from other outfits.
3. Keep the old large-deal path for non-connected users.
4. Add expanded connected-only large deals at roughly `4x` to `5x` the older deal volume.
5. Define how those deals appear:
   - one-time
   - repeatable
   - gated by rank, respect, or business ownership
6. Add offshore-account variants that pay in full but reduce total cost when half the payment is dirty cash.
7. Build the funeral-home fake-account quest chain.
8. Gate the fake-account path behind `connected`.
9. Require:
   - personal info
   - ink
   - paper
10. Decide whether fake accounts become:
   - a reusable service
   - a consumable item
   - a quest-completion state

## Desired Outcome

Finance progression should feel more layered: normal money flow, larger deal tiers, and a criminal paperwork route that opens up cleaner late-game play.
