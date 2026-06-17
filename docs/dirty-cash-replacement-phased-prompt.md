# Dirty Cash Replacement Phased Prompt

Use this prompt when returning to the long-term goal of replacing the external Dirty Cash DLL with the After Prohibition mod suite's own dirty-cash and backroom systems.

Do not start this plan until the current robbery heat and weapon stance work is stable.

## Goal

Move toward relying only on our own dirty-cash system, owned primarily by `AfterProhibitionEconomy.dll`, without breaking existing saves, backroom modules, legal-front behavior, route/shop orders, territory visuals, or external compatibility fallbacks during the transition.

The final target is:

- no required external Dirty Cash Economy DLL
- dirty cash storage, routing, laundering, and spending owned by After Prohibition code
- legal and illegal backroom behavior owned by After Prohibition code
- clear boundaries between economy, UI, routes, territory, and compatibility
- config fallback during migration so live saves can recover if a phase regresses

## Current Direction

The first small-blast passes should not remove the external DLL. They should build proof that our system can classify and mirror the existing behavior before mutation moves.

Use `AfterProhibitionEconomy` as the main owner for economy-only logic.

Keep these boundaries:

- `AfterProhibitionEconomy`: dirty-cash classification, economy audits, stock refresh, module repair, dirty-cash routing, laundering, and eventual money mutation
- `GameplayTweaks`: route movement authority, multi-crew vehicle rules, territory refresh, combat, family, politics, and temporary dirty-cash fallback behavior
- `AfterProhibitionCompatibility`: external DLL detection, broad-unpatch protection, compatibility ownership decisions
- `AfterProhibitionUI`: panels, popups, labels, visual layout, and owned-business UI polish

## Blast Radius Estimate

Small:

- docs and owner maps
- read-only dirty-cash/backroom diagnostics
- bridge methods that classify current behavior
- live log comparison against external Dirty Cash behavior

Medium:

- owned-business backroom classification
- module choice visibility
- NPC empty module repair
- NPC purchase-stock refresh
- player legal backroom consumer candidate checks

Large:

- dirty cash income conversion
- dirty cash storage and safehouse routing
- laundering ticks
- overdraw protection
- dirty cash spending rules
- runtime dirty-cash safety sweep mutation

Very large:

- full external Dirty Cash removal
- route/shop dirty-cash commits
- owned-business install validation
- UI replacement for all dirty-cash/backroom actions
- territory respect, AOE, and visual ownership effects
- save migration and recovery for older external-DLL saves

## Phase 0 - Owner Audit

Status: planned only.

Map every dirty-cash and backroom behavior currently owned by:

- external Dirty Cash DLL
- `GameplayTweaks`
- `AfterProhibitionEconomy`
- `AfterProhibitionCompatibility`
- `AfterProhibitionUI`

Deliverable:

- update or replace `docs/ai/after-prohibition-economy-migration-inventory.md`
- identify the first mutation that can move safely
- identify everything that must remain fallback-only

No runtime behavior changes.

## Phase 1 - Shadow Classification

Status: planned only.

Add or expand read-only `AfterProhibitionEconomy` bridge methods that classify:

- dirty-cash backrooms
- legal fronts
- legal player business consumer modules
- dirty-cash storage candidates
- laundering candidates
- module install candidates
- external Dirty Cash ownership status

Logs should compare our candidate counts against current live behavior.

No money, inventory, module, territory, route, or UI mutation.

## Phase 2 - Backroom Visibility Shadow

Status: planned only.

Mirror owned-business module choice and upgrade decisions in diagnostics.

Check:

- which backroom modules should be visible
- which are duplicate city/suffix variants
- which are blocked by existing rules
- which external Dirty Cash hooks are active
- whether player garage and repair-bay modules remain visible

No module list mutation yet.

## Phase 3 - Safe Economy Repairs

Status: planned only.

Move or confirm low-risk economy repairs under `AfterProhibitionEconomy` ownership:

- NPC purchase-stock refresh
- NPC empty business module repair
- startup readiness guards
- detail log throttles for large maps

GameplayTweaks fallback remains active until live logs prove Economy owns the repair cleanly.

## Phase 4 - Runtime Sweep Ownership

Status: planned only.

Move dirty-cash runtime safety sweep mutation only after Phase 1 through Phase 3 are clean.

Requirements:

- no startup map-load slowdown
- no full-map scan during procgen or pre-interactive loading
- bounded per-turn work
- clear summary logs
- config fallback to GameplayTweaks sweep behavior

## Phase 5 - Player Legal Backroom Consumers

Status: planned only.

Move player legal business consumer execution cautiously.

This phase must avoid repeating the previous hot-path regression where bridge delegation caused major board-load slowdown.

Requirements:

- local cheap candidate cache
- no reflection in hot loops
- no full-map scan on each consumer check
- no mutation until diagnostics show parity

## Phase 6 - Dirty Cash Storage And Routing

Status: planned only.

Own dirty-cash storage decisions in our system.

Check:

- safehouse inventory behavior
- dirty-cash backroom inventory behavior
- vehicle and crew carried cash interactions
- front/business resource routing
- external save data expectations

This is a large phase and should get its own sub-plan before code.

## Phase 7 - Income Conversion And Laundering

Status: planned only.

Move dirty-cash income conversion and laundering ticks after storage/routing is stable.

Requirements:

- clear clean-vs-dirty cash rules
- clean payment routing
- dirty income source rules
- laundering rate and capacity rules
- save/load compatibility
- rollback config

## Phase 8 - Spending And Overdraw Protection

Status: planned only.

Move dirty-cash spending rules and trade overdraw protection.

This touches shop transactions, scheduled trades, route orders, and potentially player cash validation, so it should remain late.

No route-shop order mutation should move here unless route ownership has already been split.

## Phase 9 - Backroom Install Validation

Status: planned only.

Move illegal/legal backroom install validation only after module visibility and dirty-cash storage are stable.

Check:

- install confirmation popup
- module slot validation
- add-module and upgrade paths
- duplicate module variants
- city-specific module variants
- player vehicle backrooms
- repair bay and garage visibility

## Phase 10 - Territory And AOE Effects

Status: planned only.

Keep territory and AOE outside the first dirty-cash replacement pass.

If dirty-cash backrooms affect respect, territory ownership, or visual color, treat that as a separate territory phase and coordinate with the existing territory conversion plan.

Requirements:

- no distant corner claims
- no incorrect owner color stretches
- no full rebuild during hot UI or turn loops
- bounded refresh scheduling

## Phase 11 - External DLL Removal Gate

Status: planned only.

Only remove the external Dirty Cash requirement after these are true:

- our DLL owns storage, income, laundering, spending, module visibility, and install validation
- live logs show no external-only behavior is still needed
- old saves load without missing data errors
- new saves work without the external DLL installed
- disabling our new owner restores a safe fallback or clean failure message
- public guide explains that external Dirty Cash is no longer required

## Phase 12 - Cleanup And Packaging

Status: planned only.

Remove obsolete compatibility code only after several clean live runs.

Update:

- public guide
- changelog
- install instructions
- compatibility matrix
- release DLL list
- troubleshooting notes

## Test Saves

Use several saves:

- current large save with many backrooms and crew
- a new game with no external Dirty Cash DLL installed
- a save that previously used external Dirty Cash
- a save with legal fronts, illegal backrooms, garages, repair bays, and player legal consumers
- a save with routes and shop orders active

## Stop Conditions

Stop and investigate if:

- map loading time jumps sharply
- startup diagnostics scan during procgen
- player garages or repair bays disappear from module choices
- legal fronts stop consuming or producing
- dirty cash turns into clean cash without laundering
- clean cash payments use dirty cash unexpectedly
- route shop orders fail or duplicate
- owned-business popups lose module choices
- territory color changes unexpectedly
- old saves require the external DLL after a supposed replacement phase

## Return-To-Work Note

Finish robbery heat and weapon stance stabilization first.

When returning to this prompt, start with Phase 0 and Phase 1 only. Do not remove the external Dirty Cash DLL requirement until the later removal gate is satisfied.
