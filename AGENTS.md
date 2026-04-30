# Mod Automation Instructions (ClassLibrary1)

These instructions are for coding agents working in this repository.

## Execution Mode

- Run in autonomous mode by default.
- Automate implementation work (search, edit, build, validate) by default.
- Do not ask the user for yes/no confirmation before normal file edits.
- Keep planning interactive when needed so the user can answer questions.
- Treat C# source edits as pre-approved implementation work.
- Assume approval to:
  - Inspect files and search code.
  - Edit source, docs, and scripts in this repo.
  - Edit any `*.cs` file needed to complete the requested task.
  - Run local builds and validation commands for touched projects.
  - Run a solution build when shared contracts/types change.

## Build Defaults

- Prefer targeted build first:
  - `dotnet build <Project>/<Project>.csproj -c Release`
- Run full solution build only when needed:
  - `dotnet build ClassLibrary1.sln -c Release`

## Public Release Packaging

- Stage public release files inside the repo at:
  - `Things To Have\Current After Prohibition Mod`
- Stage BepInEx release files under:
  - `Things To Have\Current After Prohibition Mod\BepInEx`
- For GameplayTweaks public releases, copy the built DLL from:
  - `GameplayTweaks\bin\Release\GameplayTweaks.dll`
  - to `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll`
- Keep release changelog and install/use guide in the public release root:
  - `Things To Have\Current After Prohibition Mod\CHANGELOG.md`
  - `Things To Have\Current After Prohibition Mod\GUIDE.md`
- Do not copy DLLs to the live City of Gangsters install by default. The user will copy live DLLs manually until the remaining symlinks are restored.

## Runtime Logs

- Do not rely on repo-root `Player.log` or `Player-prev.log` copies for current runtime triage.
- Use the live SomaSim app data logs instead:
  - `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player.log`
  - `C:\Users\User\AppData\LocalLow\SomaSim\City of Gangsters\Player-prev.log`
- Do not add `Player.log` or `Player-prev.log` to the repo.

## Decompiled Game Verification

- Use the checked-in `decompiled\...` sources first for normal game-side anchors.
- When a Harmony target, reflection signature, private overload, or live-game behavior is uncertain, verify against the live assembly with:
  - `C:\Users\User\Documents\Modding Programs\DNSPEX\dnSpy.Console.exe`
- Prefer targeted dnSpy console checks over broad re-decompiles. Examples:
  - `& "C:\Users\User\Documents\Modding Programs\DNSPEX\dnSpy.Console.exe" -t Game.Session.Entities.BizComponent "<Assembly-CSharp.dll>"`
  - `& "C:\Users\User\Documents\Modding Programs\DNSPEX\dnSpy.Console.exe" --md 0x06000123 "<Assembly-CSharp.dll>"`
- Use dnSpy verification before changing reflection binding flags or adding Harmony patches when the repo decompile may be stale.

## Automatic Workspace Command Execution

- Agents (including codex-style agents) MAY run workspace-defined commands and VS Code tasks automatically when it helps complete the requested work.
- Preferred behavior: use `run_task` for named VS Code tasks (or other workspace task runners) when available; prefer targeted builds (e.g. `dotnet build <Project>/<Project>.csproj -c Release`) over full-solution builds.
- Fallback behavior: use `run_in_terminal` for ad-hoc commands when a named task is not present.
- Safety rules: avoid destructive or irreversible commands (for example deleting user data or clearing plugin folders) without explicit user confirmation. Non-destructive build, test, and validation commands may be executed automatically.
- Logging: record which task/command was run and its outcome so results are visible in the agent's activity log.
- Detection: if the workspace contains task definitions (`.vscode/tasks.json`) or common project files (`*.csproj`, `package.json`), prefer the corresponding workspace tasks or the minimal project-level command.

## Subagent Thread Budget

- Prefer doing work locally. Only spawn subagents when there is clear sidecar work that can run in parallel without blocking the very next local step.
- Default budget: keep at most `2` active subagents in this repo at one time. Prefer `1` when the task is small or tightly coupled.
- Reuse existing subagents with `send_input` instead of spawning a new thread for each follow-up question.
- Default pattern: one reusable `explorer` for codebase questions and, when needed, one `worker` for a bounded implementation task with a disjoint write scope.
- Do not ask subagents to spawn their own subagents unless the user explicitly asks for deeper delegation.
- Close subagents as soon as their results are integrated. Do not leave completed or idle agents open.
- When using subagents, keep a tiny live ledger in your notes or updates with: agent name, purpose, owned files, and status. This prevents duplicate spawns.
- Use `wait_agent` only when blocked on the result. While subagents are running, continue with non-overlapping local work.
- If `spawn_agent` fails or mentions a thread limit, stop spawning new agents, finish or cancel the current delegation plan, and close unneeded agents before trying again.
- For this repo, avoid subagents for routine single-file patches, targeted builds, log triage, or straightforward decompiled-code lookups. Those are usually faster to do locally.

## Decision Defaults

- If requirements are underspecified, choose the safest minimal implementation that matches existing project patterns.
- When multiple valid options exist, pick the one with smallest blast radius and continue without asking.
- Complete tasks end-to-end (implement, validate, summarize) unless blocked by missing access.

## Planning Interaction

- If a request is clearly planning/brainstorming, ask concise clarifying questions first and wait for user answers.
- Show a short actionable plan before large changes when tradeoffs are meaningful.
- Once implementation starts, continue automatically without repeated confirmation prompts.

## CSharp Edit Automation

- For requests involving code changes, immediately edit relevant `*.cs` files instead of asking for edit permission.
- Bundle related C# edits in one pass when safe, then run the narrowest relevant build.
- Ask before editing only if the change is destructive, irreversible, or outside repository scope.

## Ask Only When Required

Only interrupt for explicit user input if one of these applies:

- A destructive action is required (for example deleting user data/history).
- Credentials, secrets, paid services, or external account access are required.
- There is an irreversible product decision with materially different outcomes.
- A legal/compliance/safety requirement requires direct user choice.
- The task is in planning mode and key requirements are still ambiguous.

## Output Expectations

- Report what changed, where, and what was validated.
- Include any untested risk explicitly.
- Keep responses concise and action-focused.
- When subagents were used, briefly report which ones were reused or closed if that mattered to task execution.
