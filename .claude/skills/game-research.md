# Skill: game-research — Investigate Game Internals

Use this skill when you need to understand how a game system works before writing a patch. This is the critical first step before any mod work.

## Research Workflow

### Step 1: Check decompiled source first
```bash
# Search decompiled source for relevant types/methods
grep -r "keyword" decompiled/ --include="*.cs"
```
The `decompiled/` folder contains ILSpy output of `Assembly-CSharp.dll`. Always check here before decompiling fresh.

### Step 2: Decompile specific types if needed
```bash
# Decompile a specific type from the game DLL
ilspycmd "C:/Program Files (x86)/Steam/steamapps/common/City of Gangsters/CoG_Data/Managed/Assembly-CSharp.dll" -t "Game.Session.Sim.SomeType"
```

### Step 3: Map the system
Before patching, document:
- **Entry points**: What methods trigger the behavior?
- **Data flow**: What fields/properties hold the state?
- **Dependencies**: What other systems does it call into?
- **Access modifiers**: Is it public, internal, or private? (determines reflection needs)

## Common Game Namespaces

| Namespace | Contains |
|-----------|----------|
| `Game.Core` | Core types, GameClock |
| `Game.Session.Data` | Data definitions, entity data |
| `Game.Session.Entities` | Entity types (people, buildings, etc.) |
| `Game.Session.Player` | Player management, AI players |
| `Game.Session.Player.AI` | AI decision-making |
| `Game.Session.Sim` | Simulation systems |
| `Game.Session.Sim.Modules` | Individual sim modules |
| `Game.Session.Board` | Map/board systems |
| `Game.Session.Heatmaps` | Territory/heat systems |
| `Game.Session.Setup` | Game setup/initialization |
| `Game.Services` | Service layer |
| `Game.UI.Session.Picks` | UI pick/selection systems |
| `SomaSim.Util` | Utility types from game framework |

## Tips

- The game uses `dynamic` and reflection-heavy patterns internally — types may not be what they appear
- Many game collections use custom types from `SomaSim.Util`
- Entity IDs are `ulong` — watch for type mismatches
- The `Game.Game.ctx` static field is the root of most game state access
- When a method is too complex to prefix/postfix, check if a smaller helper method can be patched instead
