# Performance Playbook

Use this when the task is about turn-time spikes, map generation slowness, coroutine churn, rendering overhead, or avoidable allocations.

The default goal is measurable reduction in work without changing gameplay behavior.

## Performance workflow

1. Confirm the owning project, usually `GameOptimizer` for performance-only work.
2. Search the current implementation and log output first.
3. Identify the likely hot path before rewriting:
   - per-frame,
   - per-turn,
   - large-map startup,
   - coroutine batch processing,
   - repeated UI refresh.
4. Add or use targeted diagnostics if the hotspot is still uncertain.
5. Optimize the smallest hot path that explains the cost.

## Allocation and hot-path rules

- Avoid reflection inside hot loops.
- Avoid closure captures and anonymous delegates in frequently rebuilt UI or repeated coroutine batches when a cached/static handler is viable.
- Avoid boxing in frequently called paths.
- Reuse lists, arrays, dictionaries, and buffers where safe.
- Avoid repeated string formatting in high-frequency logs.
- Guard diagnostics behind config flags and keep them off by default.

Unity guidance for this is straightforward: aim to reduce temporary allocations and drive hot paths toward zero avoidable bytes per frame or per iteration where practical.

## Preferred optimization patterns in this repo

- Cache-per-turn results when values are stable for the turn.
- Batch coroutine work to reduce yield overhead during large setup flows.
- Debounce expensive rebuilds when multiple triggers can collapse into one refresh.
- Replace repeated broad scans with cached lookups keyed by stable ids.
- Keep null-guard and fail-soft wrappers around fragile optimized coroutines so runtime failures fall back cleanly.

## Use local examples

When deciding how to optimize, inspect `GameOptimizer` first for existing patterns such as:

- coroutine replacement for heavy setup flows,
- territory rebuild debouncing,
- cache-per-turn evaluation,
- renderer/update suppression,
- large-map batching and chunk-size tuning.

Follow the existing local style when it already solves the same class of problem.

## Measurement defaults

- Prefer profiling or targeted timing/logging before and after the change.
- If full profiling is not available, add narrow log timing around the suspected path, validate the effect, then keep the diagnostics guarded or remove them.
- Do not call an optimization complete unless the hot path and expected improvement are named explicitly.

## Sources

- Unity GC best practices: https://docs.unity3d.com/Manual/performance-garbage-collection-best-practices.html
