# One Move, End to End

How a single held-direction move travels through the two layers described in
[GDD §7](GDD.md#7-technical-design). Use it as a map when implementing `InputReader`, `LevelController`,
`Simulation`, `MoveResolver` and the views.

> Some details below are not decided in the GDD yet. They are marked **[open]** and listed at the end.

## Scenario

Kestrel is active. A 3-cell teal L-block `d` stands on the floor directly to its right. The player
holds **→**.

```
before                 after one step
. . . . . . .          . . . . . . .
K K d d . . .          . K K d d . .
K K d . . . .          . K K d . . .
# # # # # # #          # # # # # # #
```

## Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Player
    participant IS as Input System
    participant IR as InputReader
    participant LC as LevelController
    box Rules layer (plain C#)
        participant SIM as Simulation
        participant GRAV as GravitySystem
        participant MR as MoveResolver
        participant GRID as GridModel
        participant CT as CarryTracker
        participant LT as LoadTracker
        participant O2 as CountdownTimer (oxygen)
        participant WIN as WinChecker
    end
    participant SV as ShipView (Kestrel)
    participant BV as BlockView (d)
    participant HUD as HudView

    Player->>IS: press →
    IS->>IR: Move.performed (1, 0)
    IR->>LC: SetHeldDirection(Right)

    Note over LC: Frame N — Update()
    LC->>SIM: SetHeldDirection(Right)
    LC->>SIM: Tick(dt)

    SIM->>GRAV: 1. Tick(dt, grid)
    GRAV->>GRID: SupportsOf(each block)
    GRAV-->>SIM: nothing falls

    SIM->>MR: 2. TryMove(kestrel, Right)
    MR->>GRID: build push chain → { d }, weight 3
    Note over MR: front clear, 3 ≤ pushCapacity 4 → accept
    MR->>GRID: Move(d, +1,0) · Move(kestrel, +1,0)
    Note over MR: stepCooldown = 0.16 / 2 = 0.08 s
    MR-->>SIM: MoveResult(accepted, moved blocks)

    SIM->>CT: 3. Update(grid)
    CT-->>SIM: d = Resting
    SIM->>LT: 4. Update(dt)
    LT-->>SIM: load 0, no countdown
    Note over SIM: 5. LivesAndRespawn — nothing to do
    SIM->>O2: 6. Tick(dt)
    Note over SIM: 7. failure check → no
    SIM->>WIN: 8. BothDocked?
    WIN-->>SIM: false

    Note over SIM: flush queued events (EventHub.Flush)
    SIM-)SV: ShipMoved(kestrel, from, to, 0.08 s)
    SIM-)BV: BlockMoved(d, from, to)
    Note over SIM,HUD: OxygenChanged only when the whole second changes (e.g. 90 → 89)

    SV->>SV: StartCoroutine(Slide + tilt)
    BV->>BV: StartCoroutine(Slide)

    Note over LC,SIM: Frames N+1 … — Tick(dt) each frame, cooldown counts down, no move events
    Note over SV,BV: coroutines keep sliding the sprites

    Note over LC,SIM: cooldown ≤ 0 and → still held → next step (back-to-back glide)

    Player->>IS: release →
    IS->>IR: Move.canceled
    IR->>LC: SetHeldDirection(null)
    Note over SIM: next expired cooldown does nothing
```

## Branches of the same flow

**Refused push** — if `d` weighed 5, `MoveResolver` changes nothing in the grid and the Simulation raises
`MoveRefused(kestrel, Right, TooHeavy, chain, Yellow)`: `ShipView` plays a bump, each `BlockView` in the chain
flashes the chain's colour class. Holding → keeps retrying silently; after `refusalHintSeconds` the Simulation
raises `RefusalHint` and the HUD shows a hint.

**Pushed off a ledge** — the push succeeds this tick. On the next tick, gravity (step 1) finds
`SupportsOf(d)` empty and starts its fall timer. Every `fallStepSeconds` it raises `BlockFell` (the view
slides it one cell down), then `BlockLanded`. Gravity runs before the ship move, so a falling block
beats a ship entering the same cell on the same tick.

## Who owns what

| Concern | Lives in | Why |
|---|---|---|
| Which key means "Move" | Input System action map | Device-independent; code only knows action names |
| Held direction → command | `InputReader` | The only place that touches input |
| Calling `Tick`, pausing | `LevelController` | The one bridge from Unity's frame loop into the rules |
| Step cooldown, fall timer | Rules layer | They decide races (ship vs. falling block), so they must be testable |
| Grid positions | `GridModel` | Changes the moment a step starts |
| Sliding, tilt, bump, flash | Views (coroutines) | Presentation only — a laggy slide can't change the outcome |

## Open questions

Questions 1–4 were decided in the #3 pair session (see the issue comment and GDD §7).

1. ~~**Event delivery**~~ — **queued, flushed once after step 8** (`EventHub`). Views never see a half-updated grid.
2. ~~**Step cooldown location**~~ — **per-ship state in the Simulation.** Use an accumulator
   (`timer -= stepSeconds`, not `= 0`) so ship speed doesn't depend on frame rate.
3. ~~**Sub-frame taps**~~ — **latched in the Simulation, not `InputReader`**: pressing a direction sets a
   "pressed since last step" flag that the next step consumes, even if the key was already released.
4. ~~**`ShipMoved` payload**~~ — **carries `stepSeconds`**; views never read ship speed from config.
5. ~~**Refused move and cooldown**~~ — **decided in #7:** a refusal uses up the step like a move, but only the
   first refusal of a hold raises `MoveRefused` (one bump); later retries are silent and succeed if the obstacle
   clears. After `refusalHintSeconds` of pushing, `RefusalHint` is raised once. Original question: — does a bump start a cooldown? Decides whether holding → against a wall
   bumps once or repeatedly.
