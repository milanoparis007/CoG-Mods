# After Prohibition Systems Guide

This build is a public beta of a much larger After Prohibition direction. The city is becoming more reactive: crews remember how they are treated, gangs organize into pacts, routes can grow fronts, police heat has more consequences, and people on the map have stronger roles. These systems are playable now, but they will keep changing over the next few months as balance, UI clarity, and edge cases are tightened.

## StreamingAssets Content Layer

This release is not only a BepInEx DLL update. It also changes the authored `StreamingAssets` data that controls businesses, modules, conversations, quests, buffs, AI, people, maps, schemes, gambling, and localization.

The content package adds or retargets a broad legal-front layer. Business and module data now point more often at `player-legal-*` fronts such as grocery, restaurant, barber, florist, funeral, cafe, hardware, laundry, car dealership, real estate, wholesale, contractor, gas station, jewelry, hotel, liquor, pub, movie theater, pawnshop, law office, record, film, gym, talent, and money-laundering fronts. These are intended to make legitimate-facing businesses matter instead of leaving them as simple placeholders.

Bank and employment-style businesses were also tightened. Banks now use the newer bank-counter front path for permits, accounts, and bank-flavored resources. Bank buildings now spawn through controlled civic-style map entries instead of the generic commercial lot pool, so they should appear more deliberately like schools and churches. Employment-agency style content connects to labor contracts, talent agents, cooks, managers, coaches, and entertainment staffing resources.

Several businesses now seed delis, home-booze sources, legal grocery-style modules, florist/funeral fronts, bank counters, and updated tobacconist content. Funeral-home starter fronts are no longer New-York-only and use a lower population gate so they can show up more often. Some extreme population requirements and takeover blockers were reduced so the newer business pool can appear in more normal play.

The map data for Atlantic City, Chicago, Cincinnati, Detroit, New York, Philadelphia, and Pittsburgh now uses the current `school-large` civic reference. Most of those cities also lower initial family density to leave more room for real businesses and the expanded front pool.

The economy data adds more upkeep/property-tax pressure for legal fronts, homes, rentals, backrooms, drug rooms, brothels, parking, taxis, and other owned modules. At the same time, some old protection and professional costs were reduced so the new ownership layer is not only punishment.

## Crew Relations

Crew members now carry persistent relationship state. The important values are loyalty, happiness, loyalty cap, street credit progress, odd-job history, vacation state, spouse search state, and snitch risk.

Happiness is the day-to-day morale layer. Good treatment, useful meetings, rest, and successful work can keep it healthy. Dangerous work, fights, kills, poor outcomes, and extended neglect can push it down. When happiness is high, loyalty decay can be held back. When happiness stays low, the crew member becomes a bigger long-term risk.

Loyalty is the deeper trust layer. It does not need to move every turn, but it matters when the game checks whether a person is reliable, whether they might become a snitch risk, and how stable the family feels. The log marker `[Loyalty]` shows daily results such as hold, high-happiness protection, cap initialization, or decay behavior.

Vacations are a pressure valve. A crew member can be sent away for a short break, which removes them from immediate work but helps stabilize morale. Use this when a valuable crew member is getting unhappy but you do not want to lose them.

Gang meetings are a social control tool. They can improve happiness, help stabilize loyalty, and can expose snitch problems. Better spending tiers are meant to feel more meaningful than a bare-minimum meeting.

## Street Credit

Street credit represents reputation earned through dangerous or high-status work. It exists in two forms:

- personal street credit progress on crew members;
- safehouse `streetcredit` resources awarded when personal progress levels up.

Street credit can come from fights, kills, successful gang jobs, pact work, goon work, robbery-style work, dirty cash or laundering jobs, liquor/trade deals, jail or smuggling-style work, and other dangerous underworld actions. Combat currently gives a larger direct bump for kills than for fights. Many conversation and job rewards give smaller progress chunks.

When a crew member fills a full progress level, their personal street credit level rises and the safehouse receives a batch of `streetcredit` resources. The boss starts with an early level so the player has some reputation footing at the beginning of a campaign.

Street credit is not just a number to hoard. It is meant to become a reputation currency for pact access, hard jobs, family weight, and high-risk underworld actions as the mod grows.

## Spouse And Kids

The spouse system is an early family-life layer for crew identity. A crew member can look for a spouse over time. The search works on a timed interval, can spend clean cash on dating, and can prefer same-ethnicity matches when that option is enabled.

Finding a spouse gives the crew member stronger family context. The system also tracks future kids count so the mod has a foundation for dynasty, succession, and family continuity later. In this build, the most important effect is roleplay and persistent crew identity. The long-term goal is that spouse and child history will matter more as the family systems grow.

This part of the mod is intentionally a journey. It is not meant to be finished in one release; it is the base for deeper family stories over the next few months.

## Odd Jobs

Odd jobs give idle crew a way to make clean money without using them for a full illegal action. An odd job consumes part of the crew member's current action points and pays a modest wage. Pay scales with crew role and street credit, so a more established person can bring in a little more.

Odd jobs are not supposed to beat organized crime income. They are a fallback, a pacing tool, and a way to make low-level crew feel useful between bigger jobs. The log marker `[OddJob]` shows who was paid, how many actions were consumed, and where the money went.

## Pacts

Pacts are gang alliances. They give AI gangs a larger identity than isolated crews and allow the city to react in groups. Pacts can protect turf, score enemy pressure, build WarHeat, vote or change over time, and retaliate when members are attacked.

AI pact behavior has several moving parts:

- AutoProtect checks nearby turf pressure and tries to defend valuable corners.
- WarHeat tracks anger between gangs or pact groups.
- Revenge queues turn serious attacks into delayed retaliation.
- Coordinated attacks can happen when pressure is high enough.
- Hire automation can strengthen gangs so large organizations do not collapse too easily.

The logs use `[GangOps.Pact]` for pact-channel behavior. If no eligible gangs exist, a pact pass can skip cleanly.

## My Pact

My Pact is the player's pact track. It is separate from the regular AI pact slots so the player can eventually build their own alliance identity.

Joining or forming pact relationships can award street credit progress and can change how other gangs read the player's position in the city. Pact membership also matters for retaliation: harming a pact member can pull other members into the response.

The pact color and display systems are still being refined. Some logs may show pact color refreshes even when no pact color applies yet. That is normal while the UI and territory layers are being stabilized.

## Gang Operations

Gang operations now run in two channels:

- pact operations for gangs inside an active pact;
- independent operations for gangs outside pacts.

Independent gangs use a lighter and faster pressure model. Pact gangs use a heavier model because they represent organized alliances. Both can build WarHeat, retaliate, protect turf, and react to attacks.

WarHeat is not police heat. It is underworld anger. Attacking, wounding, or killing gang members raises it. Time can decay it. Higher aggression settings lower the practical threshold for retaliation, while calmer settings make gangs tolerate more before acting.

Watch for these log markers:

- `[GangOps.Independent]`
- `[GangOps.Pact]`
- `[GangOps.Route]`
- `[GangOps.Independent.WarHeat]`
- `[GangOps.Independent.Revenge]`
- `[PactRetaliation]`

## Quests And Jobs

The current StreamingAssets update touches quest, scheme, goon, gang, crew, and business conversations. This means more systems are being routed through conversation choices instead of only hidden runtime hooks.

Look for jobs that involve:

- gang deals;
- goon rewards;
- pact work;
- robbery pressure;
- laundering or dirty cash;
- liquor and trade movement;
- jail or smuggling-style work;
- business/backroom opportunities.

Many of these can now feed street credit, heat, happiness, or gang pressure. Some jobs are still simple on the surface while their backend systems are being connected.

StreamingAssets adds several concrete quest and conversation paths:

- rail shipments for bulk liquor through train stations;
- short, full-car, and long-order rail tiers for home brew, cider, brick wine, moonshine, bathtub gin, sparkling cider, fake beer, and fake wine;
- school support, school payout, and school recruiting windows;
- church support, church loan, and church loan repayment;
- connected fake-account paperwork schemes from funeral homes and churches;
- gang war mediation text;
- updated dirty-cash and laundering rewards;
- updated Canada, leader, Gatsby/recruiting, drugs, liquor, and related quest access checks.

Rail shipments are meant to be a storage-network payoff. If you control the right train station and have the right storage setup, the rail line can move large liquor lots out of town for large safehouse payouts.

School and church paths are civic influence hooks. They are not finished political simulations yet, but they are the beginning of a system where public institutions can become part of the family's local support network.

## Snitches

Snitches are a long-term risk system. Crew members can carry snitch weight, become exposed, leak information, or become part of a delayed case. Snitch outcomes can connect to police pressure, raids, and the aftermath of violent incidents.

What to watch for:

- crew with poor happiness and loyalty;
- exposed snitch alerts;
- repeated suspicious consequences after risky work;
- gang meeting results that reveal someone;
- heat or raid behavior after illegal sales and violent jobs.

Gang meetings are one of the main ways to discover a problem before it becomes worse. Removing or resolving a snitch can also create reputation consequences, including street credit for the boss in some cases.

## Heat

There are multiple pressure systems that can look similar but are not the same.

Corner heat is local police attention. Selling booze, smuggling, or running illegal activity can raise it. The log marker `BoozeSell credit` can show heat before and after a sale.

National heat is broader law pressure. Cop assault, cop killing, and important witness situations can feed this layer when those systems are enabled.

WarHeat is gang anger. It decides whether gangs and pacts retaliate, not whether police raid you.

Some logs can say `CornerHeatRaid trySellOutToFeds-skipped` when the game does not have the right federal hint or precinct context. That is a skipped opportunity, not a crash.

## Cop Killing

Cop killing and cop assault are handled as serious escalation systems. The separate CopKilling runtime can show combat names, manage residual aggro cleanup, stabilize cop-war behavior, and connect violent incidents to witness/snitch pressure.

At the moment, some cop-war features are intentionally guarded by config and safety wrappers. If an attack-advisor edge case happens, the wrapper can swallow it and keep the game running. Keep reporting any case where combat stalls, names disappear, or police stay hostile forever after the incident should have ended.

Cop killing is not meant to be a free strategy. It should create pressure, witnesses, heat, and retaliation risk as the system matures.

## Business Owners, Workers, Gangs, And Troublemakers

Business owners, workers, gang members, troublemakers, and family-style people should not all be treated as the same hiring pool. This build adds more guardrails so gangs and troublemakers do not become invalid business owners or workers, and so people reserved for family or ownership roles are not pulled into the wrong job.

The long-term goal is closer to vanilla behavior: business owners should hire family or people they plausibly know, while gangs and troublemakers keep their own identity. More owner-pool tuning is expected in later passes.

## Safehouse And Owned-Building Resources

Storage access is intentionally strict because the final-goal travel preview can otherwise lie about where the vehicle physically is.

Safehouse resources should only be transferable when the vehicle is physically at the safehouse node. Owned-building storage can be visible for context, but transfer depends on physical presence or the exact intended travel target. If the vehicle leaves the corner, transfer buttons should be disabled for that vehicle even if the panel can still be opened.

Scopeout preview can authorize the intended destination action where appropriate. It must not count as physical presence for safehouse resources or wrong-corner actions.

## Delivery Route Front Expansion

Delivery routes can now optionally expand fronts while collecting. A compact `+` marker appears on supported `Collect front` route steps. If enabled, a successful collection can call the native expansion system.

The route still obeys vanilla-style rules:

- the front must have a valid expansion opportunity;
- the vehicle must have enough cash;
- the route step must be the enabled collection step;
- the game must be able to start the outpost expansion normally.

If the log says `expanded=0 skippedNoCorner=...`, the route tried but those fronts had no legal next corner. That is expected behavior, not a failure to move.

## What To Expect Next

This release is the foundation for a larger public arc. The next few months should focus on:

- clearer UI for crew relation state;
- stronger feedback for spouse/family outcomes;
- more predictable pact diplomacy;
- better visible explanations for street credit and heat;
- continued cleanup of storage, scopeout, and vehicle-node edge cases;
- deeper business owner and worker pools;
- more balance passes on gang expansion and retaliation.

Play it as an evolving systems build. Reports with videos and logs are still the fastest way to tighten the remaining edge cases.
