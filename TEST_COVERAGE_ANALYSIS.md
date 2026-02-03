# Test Coverage Analysis Report

## Executive Summary

The CoG-Mods repository contains **7 BepInEx mods** for City of Gangsters with approximately **5,300+ lines of C# code** across 8 source files. Currently, **there is no test infrastructure in place** - the codebase has **0% test coverage**.

## Current State

### Codebase Metrics

| Metric | Value |
|--------|-------|
| Total Projects | 7 mods |
| C# Source Files | 8 |
| Lines of Code | ~5,300+ |
| Test Files | 0 |
| Test Coverage | 0% |
| CI/CD Pipelines | None |

### Project Breakdown by Complexity

| Project | File | LOC | Complexity | Risk Level |
|---------|------|-----|------------|------------|
| BossDeath | BossDeathPlugin.cs | ~660 | High | **Critical** |
| GameOptimizer | GameOptimizerPlugin.cs | ~3,500+ | Very High | **Critical** |
| GameplayTweaks | GameplayTweaksPlugin.cs | ~520 | Medium-High | High |
| OrgChartMod | OrgChartPlugin.cs | ~300 | Medium | Medium |
| BossBuildings | BossBuildingsPatch.cs | ~55 | Low | Medium |
| AutoLevelup | AutoLevelupPlugin.cs | ~50 | Low | Low |
| ClassLibrary1 | ModLauncherPlugin.cs | ~18 | Minimal | Low |

---

## Critical Areas Requiring Test Coverage

### 1. BossDeath Module (HIGH PRIORITY)

**Location:** `BossDeath/BossDeathPlugin.cs`

This module handles boss succession when the player's boss dies - a critical game mechanic that affects save game integrity.

**Areas needing tests:**

#### 1.1 GameReflection Class (Lines 27-294)
- **Risk:** Heavy reflection-based access to 30+ game types/fields
- **What to test:**
  - `GetCtx()`, `GetServ()`, `GetHuman()` - null safety
  - `CreateLabel()` - correct Label instantiation
  - `GetXpForCrewAssignment()` - null propagation through entity chain
  - `CallAddPopup()` - generic method invocation
- **Example test cases:**
  ```csharp
  [Test] void GetHuman_WhenCtxIsNull_ReturnsNull()
  [Test] void GetXpForCrewAssignment_WhenPeepIsNull_ReturnsNull()
  [Test] void CreateLabel_WithValidId_ReturnsLabel()
  ```

#### 1.2 BossPromotion Class (Lines 296-463)
- **Risk:** Modifies crew state and game progression
- **What to test:**
  - `TryPromoteNextBoss()` - promotion logic correctness
  - `HasLivingUnderboss()` - crew role detection
  - `CountCrewWithRole()` - role counting accuracy
  - `GetLivingCrewCount()` - alive crew counting
- **Example test cases:**
  ```csharp
  [Test] void TryPromoteNextBoss_WhenBossIsAlive_ReturnsFalse()
  [Test] void TryPromoteNextBoss_WhenNoUnderboss_ReturnsFalse()
  [Test] void TryPromoteNextBoss_WithValidUnderboss_PromotesAndReturnsTrue()
  [Test] void HasLivingUnderboss_WhenUnderbossIsDead_ReturnsFalse()
  ```

#### 1.3 Harmony Patches (Lines 466-658)
- `ShowCrewDiedPatch` - intercepts boss death to allow continuation
- `HandleBossDeathPatch` - backup promotion trigger
- `BossSchemeRoleTurnPatch` / `BossSchemeRoleInitPatch` - boss role maintenance
- **Integration test needed:** Full boss death -> succession flow

---

### 2. GameOptimizer Module (HIGH PRIORITY)

**Location:** `GameOptimizer/GameOptimizerPlugin.cs`

The largest and most complex module with 15+ optimization patches affecting game performance and correctness.

**Areas needing tests:**

#### 2.1 Configuration Validation
- 30+ configuration entries that affect game behavior
- **What to test:**
  - Default values are sensible
  - Edge cases (0, negative, extreme values)
  - Configuration loading/binding

#### 2.2 ClockResetPatch (Lines 199-251)
- **Risk:** Float precision issues can cause game instability
- **What to test:**
  ```csharp
  [Test] void ClockReset_WhenBelowThreshold_DoesNotReset()
  [Test] void ClockReset_WhenAboveThreshold_ResetsToZero()
  [Test] void ClockReset_WhenDisabled_SkipsReset()
  ```

#### 2.3 GoonBFSSkipPatch (Lines 317-398)
- **Risk:** Incorrect skip logic could break AI behavior
- **What to test:**
  - Harassment type detection
  - BFS skip conditions
  - Fallback to original behavior

#### 2.4 Throttle Patches (Lines 401-661)
- PickManagerThrottlePatch, HeatRespectThrottlePatch, HeatPropagationThrottlePatch
- **What to test:**
  - Throttle timing logic
  - Counter reset behavior
  - Initial/non-initial differentiation

#### 2.5 AICrewLevelupPatch (Lines 667-748)
- **Risk:** Modifies AI crew progression
- **What to test:**
  ```csharp
  [Test] void AILevelup_ForHumanPlayer_DoesNothing()
  [Test] void AILevelup_WhenCrewHasXP_GrantsLevelups()
  [Test] void AILevelup_RespectsMaxPerTurnLimit()
  [Test] void AILevelup_WhenNoAvailableLevelups_Stops()
  ```

#### 2.6 Map Loading Optimizations (Lines 750+)
- HeightmapOptimizationPatch, TerrainDecoOptimizationPatch, TerrainMeshOptimizationPatch
- **What to test:**
  - Resolution scaling calculations
  - Batch size logic
  - Coroutine flow control

---

### 3. GameplayTweaks Module (MEDIUM PRIORITY)

**Location:** `GameplayTweaks/GameplayTweaksPlugin.cs`

**Areas needing tests:**

#### 3.1 SpouseEthnicity Logic (Lines 163-195)
- `ForceSameEthnicity` flag behavior
- `ScoreCandidate` scoring algorithm
- **What to test:**
  ```csharp
  [Test] void ScoreCandidate_WhenForceSameEthnicity_ReturnsZeroForDifferent()
  [Test] void ScoreCandidate_AgeScoreCalculation()
  ```

#### 3.2 HireableAgePatch (Lines 200-246)
- MonoMod detour-based patching
- **What to test:**
  - Age threshold enforcement
  - Eligibility criteria (alive, unassigned, not politician)
  - Fallback to original when disabled

#### 3.3 Gambling Dialog UI (Lines 248-519)
- Button creation and state management
- Family hiring logic
- Marriage/child scheduling
- **What to test:**
  ```csharp
  [Test] void FindEligibleFamily_OnlyReturnsValidRelationTypes()
  [Test] void OnMarryChildClick_WhenHasSpouse_SchedulesChild()
  [Test] void OnMarryChildClick_WhenNoSpouse_MarriesMatch()
  ```

---

### 4. OrgChartMod Module (MEDIUM PRIORITY)

**Location:** `OrgChartMod/OrgChartPlugin.cs`

**Areas needing tests:**

#### 4.1 SetupUnderbossRow (Lines 107-153)
- UI element cloning and positioning
- **What to test:**
  - Section creation when doesn't exist
  - Section reuse when exists
  - Sibling index positioning

#### 4.2 SetupSpecialistsRow (Lines 155-247)
- Layout scaling calculations
- **What to test:**
  ```csharp
  [Test] void CalculateScale_WhenUnder930px_ReturnsOne()
  [Test] void CalculateScale_WhenOver930px_ScalesDown()
  [Test] void ApplyColumnScaling_SetsCorrectLocalScale()
  ```

#### 4.3 OrgChartInitRoleColumnPatch (Lines 254-301)
- Underboss slot limit enforcement
- **What to test:**
  ```csharp
  [Test] void InitRoleColumn_WhenNotUnderboss_DoesNotDisableButton()
  [Test] void InitRoleColumn_WhenUnderbossSlotFilled_DisablesButton()
  ```

---

### 5. BossBuildings Module (LOW PRIORITY)

**Location:** `BossBuildings/BossBuildingsPatch.cs`

**Areas needing tests:**
- Traverse field manipulation
- Condition checking logic (isBoss, noBuildings, canShow)

---

### 6. AutoLevelup Module (LOW PRIORITY)

**Location:** `AutoLevelup/AutoLevelupPlugin.cs`

**Areas needing tests:**
- `CanShowLevelupPopup()` condition handling
- Exception handling in postfix

---

## Recommended Testing Strategy

### Phase 1: Unit Test Infrastructure Setup

1. **Create test project structure:**
   ```
   CoG-Mods.Tests/
   ├── CoG-Mods.Tests.csproj
   ├── BossDeath/
   │   ├── BossPromotionTests.cs
   │   └── GameReflectionTests.cs
   ├── GameOptimizer/
   │   ├── ThrottlePatchTests.cs
   │   └── AICrewLevelupTests.cs
   └── GameplayTweaks/
       ├── SpouseEthnicityTests.cs
       └── HireableAgeTests.cs
   ```

2. **Add NuGet packages:**
   - NUnit 3.x or xUnit
   - Moq or NSubstitute (for mocking)
   - FluentAssertions (for readable assertions)

3. **Create mock/stub infrastructure for:**
   - BepInEx types (BaseUnityPlugin, ManualLogSource, ConfigEntry)
   - Unity types (GameObject, MonoBehaviour)
   - Game assembly types (Entity, EntityID, PlayerInfo, etc.)

### Phase 2: Extract Testable Logic

Current code is tightly coupled to game types and reflection. Refactor to extract pure logic:

```csharp
// Before (hard to test)
public static bool TryPromoteNextBoss()
{
    var human = GameReflection.GetHuman();
    // ... complex logic mixed with reflection
}

// After (testable)
public interface ICrewAccessor
{
    object GetHuman();
    IList GetRawCrew(object crew);
    bool IsDead(object crewAssignment);
    // ...
}

public class BossPromotionLogic
{
    private readonly ICrewAccessor _accessor;

    public bool TryPromoteNextBoss()
    {
        // Logic using interface, can be mocked
    }
}
```

### Phase 3: Integration Tests

For patches that modify game behavior, create integration tests that:
1. Load a mock game context
2. Apply the patch
3. Verify expected behavior changes

### Phase 4: CI/CD Pipeline

Add GitHub Actions workflow:
```yaml
name: Tests
on: [push, pull_request]
jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
      - run: dotnet test
```

---

## Priority Ranking for Test Implementation

| Priority | Module | Reason |
|----------|--------|--------|
| 1 | BossDeath | Game-breaking if broken; affects save integrity |
| 2 | GameOptimizer.AICrewLevelup | Modifies AI progression; balance impact |
| 3 | GameOptimizer.Throttles | Timing bugs hard to detect manually |
| 4 | GameplayTweaks.SpouseEthnicity | Scoring logic is non-trivial |
| 5 | OrgChartMod.Scaling | Math-heavy layout calculations |
| 6 | GameplayTweaks.HireableAge | Simple conditional logic |
| 7 | BossBuildings | Minimal logic, low risk |
| 8 | AutoLevelup | Single simple postfix |

---

## Estimated Effort

| Task | Effort |
|------|--------|
| Test infrastructure setup | 2-4 hours |
| Mock/stub creation | 4-8 hours |
| BossDeath unit tests | 4-6 hours |
| GameOptimizer unit tests | 6-10 hours |
| GameplayTweaks unit tests | 3-5 hours |
| OrgChartMod unit tests | 2-3 hours |
| Integration test framework | 4-6 hours |
| CI/CD pipeline | 1-2 hours |
| **Total** | **26-44 hours** |

---

## Challenges & Mitigations

### Challenge 1: Reflection-Heavy Code
**Mitigation:** Create wrapper interfaces around reflection calls; test the reflection setup separately from business logic.

### Challenge 2: Unity/Game Dependencies
**Mitigation:** Use conditional compilation or mock frameworks to stub Unity types during testing.

### Challenge 3: Harmony Patches
**Mitigation:**
- Test prefix/postfix logic in isolation
- Use Harmony's `PatchAll` return value to verify patch application
- Consider integration tests against a minimal mock game context

### Challenge 4: UI Code (OrgChartMod, GameplayTweaks)
**Mitigation:** Extract layout calculations into pure functions; mock Unity UI components.

---

## Conclusion

The codebase would significantly benefit from test coverage, particularly in:
1. **BossDeath** - Critical game mechanic with complex state management
2. **GameOptimizer** - Many patches with subtle timing/conditional logic
3. **GameplayTweaks** - Scoring algorithms and eligibility checks

Starting with unit tests for extractable pure logic, then building toward integration tests, would provide the best return on investment while establishing a foundation for long-term code quality.
