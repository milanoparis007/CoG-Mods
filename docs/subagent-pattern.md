# Safer Subagent Pattern For This Repo

This repo benefits from delegation sometimes, but it is easy to hit platform thread limits if every question becomes a fresh subagent. Use this playbook to keep delegation useful without exhausting active threads.

## Default Stance

- Start locally.
- Only delegate sidecar work.
- Reuse agents before spawning new ones.
- Close agents immediately after their output is integrated.

If the next action is blocked on a result, prefer doing that work locally unless it is clearly separable and you can keep moving on something else.

## Recommended Budget

- Normal case: `0-1` active subagents.
- Upper bound for this repo: `2` active subagents.
- Only exceed `2` if the user explicitly asks for broad parallel delegation and the work scopes are clearly disjoint.

Recommended roles:

- `explorer`: one reusable codebase-question agent.
- `worker`: one bounded implementation or verification agent.

Avoid keeping multiple explorers open for overlapping questions. Avoid multiple workers unless file ownership is cleanly split.

## Reusable Pattern

1. Plan locally first.
   - Decide the immediate next local step before delegating anything.
   - Identify only sidecar tasks that can run in parallel.

2. Spawn narrowly.
   - Give the agent one concrete question or one bounded write scope.
   - Name the owned files or module area when assigning a worker.

3. Keep a mini ledger.
   - Track alias, purpose, owned files, and status.
   - Reuse the same agent for related follow-up instead of opening a new thread.

4. Continue local work.
   - Do not wait immediately by reflex.
   - Use the time to inspect nearby code, prep integration points, or run a targeted build.

5. Integrate and close.
   - Review the result quickly.
   - Merge or apply the useful parts.
   - Close the agent once it is no longer needed.

## Mini Ledger Template

Use a tiny scratch table in notes or interim updates:

| Alias | Type | Purpose | Owned files | Status |
|---|---|---|---|---|
| explorer-main | explorer | Find save/load entry points | `GameplayTweaks/*` | active |
| worker-ui | worker | Patch crew UI button gating | `GameplayTweaks/GameplayTweaksPlugin.UI.cs` | closed |

If an existing row already covers the new question, reuse that agent.

## Good Fits For Subagents

- Searching a separate module while you patch another one locally.
- Verifying a narrow risk in parallel with implementation.
- Making a bounded change in a disjoint file set.
- Answering one specific codebase question that would otherwise interrupt local progress.

## Bad Fits For Subagents

- Small single-file edits.
- Straightforward `rg` searches.
- Targeted builds.
- Reading one or two decompiled files.
- Urgent blocking work where you would only sit idle waiting for the answer.
- Opening a brand-new agent for each follow-up on the same topic.

## Failure Procedure When Thread Limit Hits

If the platform reports a thread or active-agent limit:

1. Stop spawning new agents.
2. Review which agents are still open.
3. Reuse an existing agent with `send_input` if one already owns the topic.
4. Close completed or idle agents first.
5. Wait only for the one result that is still blocking progress.
6. Continue locally until capacity is available again.

Do not respond to a thread-limit error by creating more narrowly scoped agents. That usually makes the problem worse.

## Prompting Pattern

Keep prompts compact and ownership-focused.

Explorer prompt shape:

```text
Find the save/load hooks used by GameplayTweaks. Report the exact classes and methods. Do not edit files.
```

Worker prompt shape:

```text
You own GameplayTweaks/GameplayTweaksPlugin.UI.cs only. Add button gating for X. Do not touch other files. You are not alone in the codebase, so do not revert others' edits.
```

## Repo-Specific Heuristic

For this solution, most tasks are faster locally because the expensive parts are usually:

- figuring out game internals from nearby decompiled references,
- editing one mod project at a time,
- running a targeted project build,
- checking the live `Player.log`.

That means subagents should stay exceptional here, not default.
