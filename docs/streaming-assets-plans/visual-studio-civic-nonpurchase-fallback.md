# Civic Nonpurchase Patch Fallback

## Purpose

This document covers the narrow Visual Studio fallback for civic locations that should behave like train stations:

- bank
- church regular
- church large
- school medium
- school large

The goal is not only to block takeover. The goal is to make the purchase option not appear at all.

## Why this moved past StreamingAssets

`StreamingAssets` can do most of the civic setup:

- building templates
- business wrappers
- placement
- modules
- quests and interactions

But the remaining requirement is stronger:

1. the node should not be purchasable
2. the purchase affordance should not be shown
3. the node should read like city infrastructure, the same way train stations do

We already saw that content-side takeover changes can affect map generation when pushed too far on banks. That makes this a good candidate for a narrow runtime patch instead of more content-only guessing.

## Current content state

Handled in `StreamingAssets`:

- bank inventory and bank counter behavior
- church wrappers
- school wrappers
- civic placement and school visibility remain a separate `StreamingAssets` tuning task and are not part of this Visual Studio fallback

Still not handled in content:

- hiding the buy/takeover action entirely for bank, church, and school nodes

## Patch objective

At runtime, treat these civic businesses like train stations for purchase UI purposes.

Desired player-facing result:

- no buy button
- no takeover prompt
- no purchase hover/action entry
- civic interactions remain available

## Lowest-risk patch shape

Patch the action-generation or action-visibility stage where takeover actions are offered for a selected node.

Recommended behavior:

1. identify the node business template or building template
2. if it is one of the civic templates below, suppress takeover-related actions
3. leave all non-purchase civic interactions untouched

## Templates to treat as nonpurchase civic

Business templates:

- `biz-bank`
- `biz-neighborhood-bank`
- `biz-downtown-bank`
- `biz-downtown-bank-new-york`
- `school-medium`
- `school-large-sub`
- `worship-church-regular`
- `worship-church-large`

Building templates that may also be useful as a fallback check:

- `com-medium-philly-bank`
- `com-large-downtown-bank`
- `school-regular`
- `civic-school-large`
- `worship-regular`
- `worship-large`

Use business-template checks first. Use building-template checks as a safety net.

## Recommended implementation order

1. find the same action-filter point already used by train-station-like nodes, if one exists
2. otherwise patch the purchase/takeover action builder
3. suppress only:
   - purchase
   - takeover
   - any "available to buy" highlight derived from that action
4. verify normal civic conversations and modules still work

## Validation checklist

1. bank is visible on map and can still buy/sell bank items
2. bank shows no purchase action
3. church regular and church large show no purchase action
4. school medium and school large show no purchase action
5. map generation still succeeds
6. no new setup errors in `Player.log`
7. no null errors when selecting or right-clicking civic nodes

## Notes for the code pass

- keep this patch narrow and data-driven
- do not change generic purchase rules for all businesses
- do not rely only on `playerTakeoverDisabled` if the UI still advertises the action
- prefer a helper like `IsCivicNonpurchaseNode(...)` so bank/church/school checks live in one place

## StreamingAssets companion note

This patch is a fallback for visibility and action suppression only.

The content source of truth should stay in `StreamingAssets` for:

- placement
- modules
- building identity
- quest hooks
- civic flavor
