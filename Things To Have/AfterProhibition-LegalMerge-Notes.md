## After Prohibition Legal Merge Notes

Base:
- `Things To Have/Current After Prohibition Mod/StreamingAssets`

Donor:
- `Things To Have/PIA Mod Vanilla/StreamingAssets`

Reference:
- `Things To Have/Vanilla Streamingassets and Csharp/StreamingAssets`

### Goal

Bring Pia's legal-business streaming asset content into the After Prohibition base without breaking:
- prohibition law behavior
- city-specific illegal business chains
- schema validity for `Modules.sim` and `Businesses.sim`

### Modules.sim Status

After Prohibition already contains these Pia legal-business module families:
- `player-legal-cafe`
- `player-legal-grocery`
- `player-legal-restaurant`
- `player-legal-florist`
- `player-legal-funeral`
- `player-legal-laundry`
- `player-legal-general-contractor`
- `player-legal-liquor-base`
- `player-legal-liquor-wine`
- `player-legal-liquor-spirits`
- `player-legal-liquor-premium`
- `player-legal-pub`
- `Taxicp`
- `ParkingLot`

That means the first merge priority is not module creation. It is the business placements and hookups that expose those modules in play.

### Businesses.sim Pia Legal Placements

Pia legal-operation placements found in `Businesses.sim`:
- `player-legal-cafe`
  - `biz-poolhall-starter` uses `player-legal-cafe`
  - additional warehouse placements
- `player-legal-grocery`
  - several basement and warehouse placements
- `player-legal-restaurant`
  - one explicit Pittsburgh placement
- `player-legal-florist`
  - warehouse placement
- `player-legal-funeral`
  - warehouse placement
- `player-legal-laundry`
  - medium and small warehouse placements

### After Prohibition Businesses.sim Current State

After Prohibition currently exposes these Pia-style legal placements:
- `player-legal-barber`
- `player-legal-cigarette-distro`
- `player-legal-laundry`
- `ParkingLot`
- `biz-liquorstore` art/name block

After Prohibition does **not** currently expose the Pia placements for:
- `player-legal-cafe`
- `player-legal-grocery`
- `player-legal-restaurant`
- `player-legal-florist`
- `player-legal-funeral`

### High-Value Businesses.sim Deltas

Candidate ports from Pia into After Prohibition:
- `biz-poolhall-starter`
  - Pia uses `player-legal-cafe`
  - AP currently uses `player-legal-barber`
  - needs a design decision, not a blind overwrite
- `player-legal-grocery` placement blocks
  - several PiA-only placements exist
- `player-legal-florist` placement block
- `player-legal-funeral` placement block
- `player-legal-restaurant` placement block

### Completed In First Businesses Pass

Patched in `Current After Prohibition Mod/StreamingAssets/Entities/Businesses.sim`:
- `biz-deli-starter`
  - swapped `ParkingLot` to `player-legal-grocery`
  - lowered starter `popneeded` from placeholder value to `35000`
- `biz-deli-starter-cincinnati`
  - swapped `ParkingLot` to `player-legal-grocery`
- `biz-deli-starter-new-york`
  - swapped `ParkingLot` to `player-legal-grocery`
- `biz-cold-storage-starter`
  - swapped `ParkingLot` to `player-legal-grocery`
  - lowered starter `popneeded` from placeholder value to `35000`
- `biz-cold-storage-starter-cincinnati`
  - swapped `ParkingLot` to `player-legal-grocery`
- `biz-cold-storage-starter-new-york`
  - swapped `ParkingLot` to `player-legal-grocery`
- `biz-florist-starter`
  - swapped `player-legal-barber` to `player-legal-florist`
  - lowered starter `popneeded` to `35000`
- `biz-funeral-home-starter`
  - swapped `player-legal-laundry` to `player-legal-funeral`
  - lowered starter `popneeded` to `35000`

### Support Check Status

Verified already present in After Prohibition:
- loc strings for:
  - `player-legal-grocery`
  - `player-legal-florist`
  - `player-legal-funeral`
  - `player-legal-cafe`
  - `player-legal-restaurant`
- no additional `Businesses.sim` support edits were required for this pass

`ConvoBiz.sim` did not show explicit references for these player-legal modules in the current AP file, but the same was true before this pass and no missing reference was introduced by the business-hook edits.

### Remaining Decisions

- `biz-poolhall-starter`
  - Pia route: `player-legal-cafe`
  - After Prohibition route: `player-legal-barber`
  - unresolved by design
- review whether any remaining Pia legal placements should replace AP placeholders or stay AP-owned

### Keep As After Prohibition Authority

Do not replace these AP-owned behaviors during the legal merge:
- city-specific bootlegger chains
- speakeasy layouts
- Canada district business logic
- prohibition-era illegal booze chains
- any AP legal-liquor visibility gated by prohibition law state

### Schema Safety Rules

Do not reintroduce these invalid patterns:
- `law-mod` inside `visreqs`
- `check-laws` placed inside invalid `mods`/`produceqty` modifier blocks
- malformed `sink` blocks
- malformed `refill` blocks

### Next Merge Order

1. Classify each conflicting `Businesses.sim` legal placement as:
   - keep AP
   - port Pia
   - merge manually
2. Patch `Businesses.sim` only for the approved legal placement set.
3. Pull required support from `Loc` and `UI/ConvoBiz.sim` only if references are missing.
4. Run a schema-risk scan before live testing.
