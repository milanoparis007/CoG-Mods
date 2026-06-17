# Boss Death Relationship Succession Phased Plan

## Goal

When the player's boss dies and a living underboss becomes boss, the player should keep the relationship state that reasonably belongs to the outfit: shop favors, gang favors, relationship high score, active buffs, conversation counts, trade memory, flags, and relevant social history.

The promoted boss should become the new `PlayerSocial.PlayerPeepId`, but the outfit should not feel socially reset just because the old boss peep is dead.

## Current Evidence

Live log around the current save shows this sequence:

- `After Prohibition Family` dead relationship cleanup removes the dead boss relationship list: `peep=4295029748 sourceListRemoved=True outgoing=327 incoming=350 emptiedIncomingLists=6`.
- `Boss Death Continuation` then logs boss succession: `BossPromotion: Successfully promoted new boss old=4295029748 new=4295019107`.
- Later `GameplayTweaks` applies boss-killed relationship buffs against the new boss identity.

The current BossDeath transfer code already tries to migrate relationships:

- `BossDeath\BossDeathPlugin.cs`
- `BossPromotion.TryPromoteNextBoss`
- `TransferBossRelationships(oldBossPeep.Id, newBossPeep.Id)`
- `CopyRelationshipState`

However `CopyRelationshipState` only copies:

- relationship type
- high relationship value
- conversation count
- buffs

It does not currently copy these relationship fields:

- `Relationship.milestone`, which stores favor tickets through `avail` and `spent`
- `Relationship.trades`
- `Relationship.flags`
- `Relationship.socialhistory`

Vanilla favor checks use the current player peep ID:

- `PlayerSocial.GetSocialTicketsAvailable`
- `PlayerSocial.GetSocialTicketsSpent`
- `PlayerSocial.SpendTickets`
- `PlayerSocial.GrantFreebieTickets`

So if `PlayerPeepId` changes to the promoted boss and the old boss relationship list is scrubbed, any missing migration of `milestone` will look like all shop and gang favors were lost.

## Likely Cause

The bug is likely an incomplete relationship-state migration during boss succession, not a shop-specific bug.

The dead relationship cleanup is doing its job by removing relationships attached to a dead peep. The problem is that the promotion transfer does not preserve all relationship state before that old boss identity disappears from normal gameplay lookups.

There is also a timing risk: both `AfterProhibitionFamily` and `GameplayTweaks` have dead relationship cleanup patches. The promotion system must either migrate before cleanup removes the old list, or the cleanup systems must be able to defer scrubbing for the just-promoted boss handoff.

## Ownership

Primary implementation owner:

- `BossDeath`

Likely coordination points:

- `AfterProhibitionFamily\DeadRelationshipCleanupPatch.cs`
- `GameplayTweaks\Features\Relationships\GameplayTweaksPlugin.DeadRelationshipCleanupPatch.cs`

Avoid moving this into the weapon stance work. This is a boss succession and relationship persistence issue.

## Phase 1 - Diagnostics Only

Add focused boss succession diagnostics before changing behavior.

Log before promotion:

- old boss peep ID
- new boss peep ID
- current `PlayerSocial.PlayerPeepId`
- old boss outgoing relationship count
- incoming relationship count targeting the old boss
- total available/spent/granted favor tickets across old boss relationships
- counts for buffs, trade stats, flags, and social history

Log after `SetBossInfo`, after `TransferBossRelationships`, and after dead relationship cleanup:

- current `PlayerSocial.PlayerPeepId`
- new boss outgoing relationship count
- incoming relationship count targeting the new boss
- total available/spent/granted favor tickets across new boss relationships
- number of relationships migrated, skipped, merged, or missing reverse links

Suggested markers:

- `[BossSuccession][Before]`
- `[BossSuccession][Transfer]`
- `[BossSuccession][After]`
- `[BossSuccession][CleanupInteraction]`

Pass criteria:

- We can see whether favors exist before the boss death.
- We can see whether favors are missing immediately after transfer or only after cleanup.
- We can identify whether old boss relationship data is already gone before `TransferBossRelationships` runs.

## Phase 2 - Complete Relationship State Copy

Extend `CopyRelationshipState` or a new helper to copy all safe relationship payloads:

- `milestone`
- `trades`
- `flags`
- `socialhistory`
- current existing `type`, `high`, `convos`, and `buffs`

Merge rules should be conservative:

- preserve the higher relationship type if one side is family and the other is acquaintance
- preserve max `high`
- preserve max `convos`
- merge buffs by ID using the strongest or latest state
- preserve ticket availability and spent count, not just granted total
- avoid double-granting tickets when both old and new relationships already have milestones
- preserve trade/history state without duplicating identical entries if the destination already has data

Pass criteria:

- A boss death with existing favors keeps available and spent favor counts.
- Existing new-boss personal relationships are not overwritten with weaker old-boss data.
- Relationship UI and favor actions use the promoted boss without needing reload.

## Phase 3 - Cleanup Ordering Guard

If diagnostics show cleanup removes old boss relationship state before BossDeath can copy it, add a narrow guard.

Options:

- perform relationship snapshot capture before the old boss is marked dead
- defer dead relationship cleanup for the old boss until `BossPromotion.TryPromoteNextBoss` completes
- expose a tiny compatibility hook from BossDeath that cleanup patches can consult

The guard should be limited to human boss succession only. Normal dead peep cleanup should still remove stale family/NPC relationships.

Pass criteria:

- Cleanup still removes ordinary dead peep relationships.
- The old boss handoff is migrated once, then old boss links are scrubbed.
- No dead boss peep remains as the active human `PlayerPeepId`.

## Phase 4 - Save/Load Recovery

Add a recovery check for saves already affected by this issue.

Possible recovery data:

- a short-lived succession ledger with old boss ID, new boss ID, promotion day, and migration result
- if old relationship state is already gone, do not fabricate favors
- if old relationship state still exists in save data, migrate once and mark complete

Pass criteria:

- Loading after a boss death does not re-run migration repeatedly.
- Saves from before the fix can recover if the old data is still present.
- Saves where the old data was already scrubbed report `unrecoverable-old-data-missing` instead of making up favors.

## Phase 5 - UI And Gameplay Verification

Test with a save that has known shop and gang favors before boss death.

Matrix:

- shop owner favors available before death
- gang boss or gang contact favors available before death
- spent favors remain spent after promotion
- available favors remain available after promotion
- active relationship buffs remain visible and functional
- trade history still affects buy/sell relationship logic
- boss-killed hostility buffs still apply
- underboss becomes boss and old group name remains
- no relationship lists point to invalid dead boss as the player's active social identity

Suggested test log checks:

- no large favor drop from old boss totals to new boss totals
- no duplicate ticket inflation
- no repeated migration on later turns
- dead relationship cleanup still logs ordinary peep scrubs

## Blast Radius

Estimated blast radius: medium.

Reasons:

- This touches core relationship persistence and favor tickets.
- It coordinates with dead relationship cleanup, which is intentionally broad.
- It affects shops, gang conversations, social introductions, trade relationship history, and relationship-based respect.

Containment rules:

- keep first pass diagnostic-only
- change only BossDeath unless cleanup timing proves otherwise
- do not change favor formulas or shop requirements
- do not disable dead relationship cleanup globally
- do not fabricate lost favors if old relationship state is already gone

