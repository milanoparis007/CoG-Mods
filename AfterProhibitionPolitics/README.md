# AfterProhibitionPolitics

Standalone After Prohibition politics plugin.

The plugin will own politics-only diagnostics and staged migration work. It owns no UI, economy, compatibility, family, route, combat, robbery, territory, or safehouse relocation behavior.

Initial purpose:

- prove the plugin loads independently from `GameplayTweaks`
- provide a clean politics log prefix for later migration work
- keep future political starter quest, bribe, judge, law-office, campaign, and election fixes out of unrelated systems

Phase 1 is a scaffold only. It logs a delayed read-only baseline and does not patch or mutate gameplay behavior.

Version `0.2.0` adds a read-only startup politics audit. The audit waits for a session with a populated building cache, then logs politics manager availability, ward/politician counts, election state, New York starter quest known-state, safehouse ward politician validity, fallback starter politician validity, and law-office template/module counts. It does not start quests, pay bribes, change elections, mutate law-office modules, change UI, or move save data.

Version `0.3.0` adds the guarded New York political starter quest bridge:

- `PoliticsManager.OnSystemTurn`
- `PoliticsManager.IsTimeToTutorialize`
- `PlayerInfo.OnPlayerTurnStarted`

The bridge starts only the vanilla New York politics starter quest when the map is New York, procgen is ready, the quest is not already active/waiting/completed, and a valid politician can be resolved. It does not move new-game politician setup, bribes, judge/law-office systems, campaign/election behavior, UI buttons, or save data.

Version `0.4.0` adds read-only political bribe state classification. It reads existing `GameplayTweaks` bribe state through reflection and logs global human mayor bribe state, AI mayor bribe counts, per-crew mayor/judge bribe counts, and expired-state counts. `GameplayTweaks` still owns bribe payments, political favor execution, stale cleanup, UI, and save data.

Version `0.5.0` adds read-only judge, law-office, and lawyer-retainer classification. It logs existing `GameplayTweaks` judge bribe state, local heat/legal risk counts, retainer totals, confirmed/unconfirmed retainers, law-office template/module counts, and player-owned law-office counts. `GameplayTweaks` still owns judge payments, retainer payments, retainer upkeep, trial outcomes, UI, and save data.

Version `0.6.0` adds read-only campaign and election classification. It logs active election wards, election stages, candidate/stat counts, human-sponsored candidate counts, vote totals, pending AI action entries, election event counts, and campaign action definition counts. Vanilla politics systems, `GameplayTweaks`, and compatibility plugins still own campaign actions, vote changes, politician control, election timing, external election-cheat detection, UI, and save data.

Version `0.6.1` hardens startup audit readiness so new-game logs wait for populated politician data instead of logging against empty ward shells during safehouse/start-location setup.

Version `0.6.2` defers politics startup audits until the session is interactive. This keeps ward, bribe, judge/law-office, and campaign/election scans out of pre-interactive map/player setup.

Version `0.6.3` makes full startup politics diagnostics opt-in behind `Features.EnableStartupPoliticsDiagnostics=false` by default. The New York starter quest bridge and read-only bridge methods remain available.

Planned migration areas are documented in:

`docs\ai\after-prohibition-politics-phase-prompts.md`
