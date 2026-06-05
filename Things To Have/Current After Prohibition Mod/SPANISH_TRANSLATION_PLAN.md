# Spanish Translation Plan

This plan tracks Spanish localization for the public After Prohibition release. Keep `StreamingAssets` text first, then move to hardcoded DLL/plugin strings.

## Phase 1 - StreamingAssets Loc Baseline

Status: in progress.

Files:

- `CoG_Data/StreamingAssets/Loc/es.txt`
- `CoG_Data/StreamingAssets/Loc/es2.txt`
- later review: `CoG_Data/StreamingAssets/Loc/estut.txt`

Completed:

- Checked `es.txt` row count and basic untranslated-string candidates.
- Checked `es2.txt`; no simple Spanish-equals-English content candidates were found.
- Translated obvious English leftovers in `es.txt` while preserving keys, tabs, placeholders, tags, sprites, and proper names.
- Normalized nonblank Spanish-only or spacing-broken rows in `es.txt` to the expected three-column layout.

Remaining review:

- Manually spot-check mod-specific lines around Atlantic City, casino, protection, loans, legal businesses, and new resources.
- Decide whether flavor/proper resource names should stay as period terms or become fully localized. Current default keeps names like `Bourbon`, `Amaretto`, `Nalewka`, `Grappa`, and place/street names unchanged.
- Review `estut.txt` after main menu/gameplay strings, because tutorial text is high-volume and more fragile.

Validation checklist:

- File remains tab-delimited with exactly three columns: `Key`, `Spanish`, `English`.
- Keep placeholder tokens unchanged: `{name}`, `{num}`, `{$firstname}`, `<sprite ...>`, `<indent=...>`, `<b>`, `<i>`, and escaped `\n`.
- Load the Spanish language in-game and check menu, building, resource, conversation, and report screens.

## Phase 2 - Release Docs In Spanish

Files:

- `CHANGELOG.md`
- `GUIDE.md`
- `SYSTEMS_GUIDE.md`

Approach:

- Add Spanish companion files instead of replacing English release docs, for example `GUIDE.es.md` and `SYSTEMS_GUIDE.es.md`.
- Translate install steps first, then feature explanations, then changelog history.
- Keep file paths, DLL names, plugin names, config keys, and command examples in English.

## Phase 3 - DLL And Plugin Text Inventory

Targets:

- `GameplayTweaks/*.cs`
- Other plugin projects in this solution that display UI, logs, config labels, popup text, or conversation text.
- Built release plugins under `BepInEx/plugins` only after source strings are handled.

Approach:

- Search source for quoted player-facing strings.
- Classify each string as UI text, config text, log/debug text, or internal-only.
- Move player-facing strings behind a small localization helper or existing game localization lookup where practical.
- Avoid translating internal Harmony patch names, config keys, save keys, and diagnostic logs unless they appear to players.

Recommended source search:

```powershell
Get-ChildItem GameplayTweaks,GameOptimizer,CopKilling,BossBuildings,BossDeath,OrgChartMod,AutoLevelup,ModLauncher -Recurse -Include *.cs |
  Select-String -Pattern '"[^"]{3,}"'
```

## Phase 4 - DLL Localization Implementation

Approach:

- Prefer adding new keys to `Loc/es.txt` and `Loc/en.txt` over hardcoding Spanish strings in C#.
- For plugin-only UI, create a small key lookup with English fallback so missing translations do not break runtime.
- Keep all new localization keys namespaced, for example `ap.ui.*`, `ap.config.*`, or `gameplaytweaks.*`.
- Update Spanish and English together in the same change.

Validation:

- Build touched projects with targeted Release builds.
- Run the game in English and Spanish to verify fallback behavior and string fit.
- Check live SomaSim logs for missing localization keys or formatting exceptions.

## Phase 5 - Final Localization QA

Checklist:

- Search Spanish files again for English leftovers that are not proper nouns, street names, resource names intentionally kept in English, markup, or placeholders.
- Check UI text overflow in Spanish on compact panels.
- Verify accented characters render correctly in-game.
- Package updated files under `Things To Have/Current After Prohibition Mod`.
