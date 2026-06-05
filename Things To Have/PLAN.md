# TrapLife Clientele, Park/Club Drug Sales, and Safe Cash Pass

## Summary
- Implement in `traplife_v4f_PIXEL_STAGE.html` as the live source of truth.
- Replace park/club “connections” as the active selling cap with population-based clientele tiers.
- Make park/club drug selling a competitive client market shared by the player, indie hustlers, and hustler-type rivals.
- Add home `displacedCash` so cash displaced by safe/storage changes stays at home instead of going to the gang bank or wallet.

## Key Changes

- Add save-safe clientele state:
  - `g.street.drugClienteleByCity[cityId].player = { tier5, tier10, tier20, lastFindWeek, lastSoldWeek }`
  - `g.street.drugClienteleByCity[cityId].hustlers[hustlerId] = { tier5, tier10, tier20, pressureScore }`
  - Legacy `drugConnectionsByCity` remains readable for old saves, but new clientele starts at `0/0/0` and old connections are not converted.

- Add population-based client caps using `getCityPopulationBase(cityId)`:
  - Tier 5 clients cap: `clamp(4, 60, floor(pop / 350000))`
  - Tier 10 clients cap: `clamp(2, 28, floor(pop / 900000))`
  - Tier 20 clients cap: `clamp(1, 12, floor(pop / 2200000))`
  - Weekly sale demand becomes `tier5 * 5 + tier10 * 10 + tier20 * 20`, limited by inventory.

- Rework park/club auto-sale:
  - Existing toggle becomes “Auto sell to park/club clientele.”
  - Weekly auto-sale sells only to the player’s current clientele.
  - Finding new clientele requires a direct park/club action and consumes `1` action point.
  - Direct park/club actions can find unclaimed clients or poach clients from competitors.

- Add client competition:
  - Competitors are seeded from city indie hustlers plus hustler-affiliated rival rappers.
  - Player poach score uses social skill, drug quality, city rep, and street cred.
  - Hustler defense score uses hustler size, turf, supply, and gang backing.
  - High social and high drug quality can move clients from hustlers to the player.
  - Low quality, poor social, heat, or neglect can cause the player to lose clients during weekly churn.
  - Client losses/gains happen gradually so clientele matters over time without whiplash.

- Preserve drug-sale balance:
  - Drug quality affects client growth, poaching, and tier upgrade odds.
  - Park/club sales still respect inventory, labor mode, heat risk, and existing cash routing.
  - Studio/music systems are untouched by this pass.

- Add home displaced cash:
  - Add `homeItems.displacedCash`, initialized to `0`.
  - Any safe overflow or forced storage reduction moves cash to `displacedCash`, not `gangFund` and not wallet.
  - Home/bank UI shows displaced cash separately from safe cash and loose cash.
  - Bank deposit/withdrawal logic can recover/deposit displaced cash from home.

- Add safe removal:
  - Home inventory gets remove buttons for owned safes.
  - A safe can be removed only when current `safeCash` is `0`.
  - If future storage recalculation displaces cash anyway, it goes to `displacedCash`.

- Add robbery risk for displaced and wallet cash:
  - Displaced home cash adds rare but notable burglary pressure, capped so it cannot spiral.
  - Wallet cash adds rare robbery pressure when carried cash is high relative to carry capacity.
  - Security, private security, housing quality, and crew protection reduce these risks.
  - Losses come from displaced cash or wallet cash only; stolen/displaced money never routes to gang bank.

## Test Plan

- Update `tools/v4f_charts_sales_balance_smoke.mjs`:
  - New saves start with zero park/club clientele even if legacy connections exist.
  - City population produces larger clientele caps in bigger cities than smaller cities.
  - Find Clientele consumes `1` action point.
  - Tiered clientele sells exactly through `5/10/20` demand units, limited by inventory.
  - High social plus high drug quality can poach clients from hustlers.
  - Low social or poor quality can fail to poach or lose clients over weekly churn.
  - Weekly auto-sale does not create free clients.

- Update `tools/v4f_cash_cap_storage_smoke.mjs`:
  - Empty safes can be removed.
  - Safes with stored safe cash cannot be removed.
  - Safe overflow/storage displacement moves money to `homeItems.displacedCash`.
  - Displaced cash does not enter wallet or gang bank.
  - Displaced cash can be deposited from home/bank UI paths.
  - Displaced and wallet cash increase robbery risk within bounded rare-risk caps.

## Assumptions

- “Start with zero clientele no matter what” means existing legacy park/club connections are retired for the new system and do not seed client tiers.
- “No cash” for removing a safe means the safe storage has `0` cash in it, not that the player wallet is empty.
- A “client” in each tier purchases that tier’s package amount during a sale cycle: `5`, `10`, or `20`.
- Park/club clientele discovery is manual and AP-based; the auto toggle only sells to already-owned clientele.
