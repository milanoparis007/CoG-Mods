# Project Map

## Solution
- `ClassLibrary1.sln`

## Projects and ownership

- `GameplayTweaks` (`com.mods.gameplaytweaks`)
  - Core systems: crew state, pacts, gang ops, dirty cash, jail/wanted, major UI patches, save/load hooks.
  - First stop for most gameplay or UI behavior changes.

- `CopKilling` (`com.mods.copkilling`)
  - Cop-war and cop retaliation behavior.
  - Depends on GameplayTweaks state/helpers.

- `GameOptimizer` (`com.mods.gameoptimizer`)
  - Performance/optimization patches only.

- `OrgChartMod` (`com.mods.orgchart`)
  - Org chart display-specific behavior.

- `BossBuildings` (`com.mods.bossbuildings`)
- `BossDeath` (`com.mods.bossdeath`)
- `AutoLevelup` (`com.mods.autolevelup`)
  - Focused feature mods; edit only when change is explicitly in their feature area.

- `ClassLibrary1/ModLauncher` (`com.mods.modlauncher`)
  - Bridge/launcher compatibility.

## Useful repo docs
- `CLAUDE.md` - high-level architecture and build/deploy notes.
- `docs/compatibility-matrix.md` - stable-core compatibility expectations and defaults.
- `references/log-triage.md` - start here when a warning/error exists but project ownership is not obvious.
- `references/patch-reliability.md` - Harmony/reflection hardening rules.
- `references/performance-playbook.md` - performance-only optimization workflow.

## Default build strategy
1. Build changed project first.
2. Build solution when shared contracts or cross-project calls changed.
