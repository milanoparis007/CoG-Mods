# After Prohibition Legal Property Tax Balance

## File

`Things To Have/Current After Prohibition Mod/CoG_Data/StreamingAssets/Settings/People.sim`

The legal business tax entries live in `people.social.crew.turnCostBoss`, under:

```sim
;; Legal Front Property Taxes
```

These lines are visible to the game's people/crew turn-cost system. Decompiled `PlayerFinances.GetCrewSalary` evaluates `crew.turnCostBoss` for the player boss on each player turn, so the entries affect the boss upkeep payment rather than a separate city tax event.

## Balance Intent

The old legal front taxes were too punishing because values like `-100`, `-150`, and `-200` were being applied through the recurring boss turn cost. The revised values treat property tax as a light weekly reserve toward a four-week bill, keeping the 1920s municipal-tax flavor without making legal fronts feel like a trap.

The new practical four-week totals are:

| Tier | Per-turn reserve | Four-week feel | Examples |
| --- | ---: | ---: | --- |
| Small lot / low overhead | -10 | -40 | taxi company, parking lot, cigarette distro |
| Small storefront | -15 | -60 | barber, florist, laundry, tailor, candy shop |
| Standard storefront | -20 | -80 | restaurant, hardware, liquor shops, pawnshop |
| Large retail / prestige front | -25 | -100 | grocery, car dealership, real estate, hotel |
| Heavy commercial backroom | -35 | -140 | contractor, refinery, money laundering, record label |
| Major studio property | -50 | -200 | film studio |

## Notes For Future Visual Studio Pass

- Current legal tax coverage is synced against all concrete `tag-player-legal-biz` module IDs in `Entities/Modules.sim`.
- Recent sync updates:
  - `player-legal-contractor` was replaced with `player-legal-general-contractor`.
  - Added skipped legal modules: `player-legal-cafe`, `player-legal-pub`, `player-legal-lawoffice`, and `player-legal-backroomwholesalew`.
  - Removed the old heavy `player-legal-lawoffice` boss-cost entry so law office is not taxed twice.
- If a true once-every-four-weeks tax event is desired later, it should be implemented in code rather than by adding more `turnCostBoss` entries.
- Until then, keep these values modest because they stack per installed module and are paid with other boss upkeep costs.
- The `lockey` values are unchanged to avoid introducing new localization requirements.
