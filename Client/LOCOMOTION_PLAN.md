# Client Locomotion Plan

## Purpose

This document is the working plan for the Unity client player controller rewrite.

Read this file first when continuing the locomotion/traversal/weapon work in a new chat.

Scope:
- player locomotion architecture
- traversal and parkour pipeline
- weapon state expansion
- animation integration for an action RPG

Non-goals for the first phase:
- full combat system
- final network replication for every locomotion action
- IK polish
- full animation authoring pass

---

## Current Project Context

Current movement entry point:
- `Assets/Script/Main/Character/PlayerController.cs`

Current known issues:
- `PlayerController` still owns too many responsibilities:
  - input subscription
  - movement calculation
  - rotation
  - gravity
  - grounded checks
  - camera target rotation
  - animation parameter writes
- `OnEnable()` can run before VContainer initialization completes, so DI-dependent logic must not rely on `OnEnable()`.

Current DI context:
- `Assets/Script/VContainer/Installers/Scenes/MainLifetimeScope.cs`
- `Assets/Script/VContainer/Initializer/MainSceneInitializer.cs`

Current design direction agreed in chat:
- build the system with SOLID
- split state by axes instead of making one giant state machine
- expand step by step
- prioritize architecture that supports traversal animations and multiple weapon types

---

## Core Design Rules

1. Do not keep growing `PlayerController` as a monolith.
2. Separate locomotion, weapon, and action into different axes.
3. Keep Unity-specific components thin and move reusable logic into plain C# classes where practical.
4. Use interfaces between modules that will likely vary:
   - input
   - motor
   - sensors
   - state factories
   - traversal action resolution
5. Animator should express state, not own game logic.
6. Traversal should be split into:
   - detection
   - action resolution
   - animation execution
   - motion correction
7. Weapon handling should be data-driven where possible.

---

## Target Architecture

### State Axes

- `LocomotionStateMachine`
  - `Idle`
  - `Move`
  - `Sprint`
  - `Jump`
  - `Fall`
  - `Landing`
  - `Traversal`
  - later: `Climb`

- `ActionStateMachine`
  - `None`
  - `Attack`
  - `Skill`
  - `Dodge`
  - `Interact`
  - `TraversalLock`

- `WeaponStateMachine`
  - `Unarmed`
  - `OneHand`
  - `TwoHand`
  - `Bow`
  - `Staff`

### Main Runtime Modules

- `PlayerStateCoordinator`
  - coordinates locomotion/action/weapon axes
  - resolves priority and locking rules

- `PlayerInputReader`
  - owns input callbacks
  - exposes normalized input state
  - must avoid DI timing bugs caused by `OnEnable()`

- `CharacterMotor`
  - wraps `CharacterController`
  - applies movement and rotation

- `LocomotionContext`
  - shared runtime data used by states

- `GroundSensor`
- `WallSensor`
- `LedgeSensor`
- `ObstacleSensor`

- `TraversalActionResolver`
  - converts sensor results into traversal action selection

- `PlayerAnimationBridge`
  - converts gameplay state into animator parameters

- `TraversalMotionController`
  - handles root motion + start/end correction

### Data Objects

- `WeaponDefinition`
- `WeaponAnimationProfile`
- `WeaponLocomotionProfile`
- later: `TraversalProfile`

---

## Suggested Folder Layout

Create under `Assets/Script/Main`:

- `Locomotion/Core`
- `Locomotion/States`
- `Locomotion/Sensors`
- `Locomotion/Traversal`
- `Locomotion/Animation`
- `Weapons/Core`
- `Weapons/Data`
- `Actions/Core`

Keep temporary compatibility code small inside:
- `Character/PlayerController.cs`

---

## Step-by-Step Roadmap

## Phase 1 - Foundation Refactor

Goal:
- remove monolithic ownership from `PlayerController`
- make initialization safe
- prepare for state-machine-driven expansion

Tasks:
- extract input handling into `PlayerInputReader`
- move input subscription away from `OnEnable()`
- extract grounded logic into `GroundSensor`
- extract `CharacterController` access into `CharacterMotor`
- create `LocomotionContext`
- reduce `PlayerController` to orchestration only

Exit criteria:
- no DI timing issue around input setup
- player can still move and rotate
- behavior matches current baseline

## Phase 2 - Base Locomotion

Goal:
- replace ad hoc movement logic with a real locomotion state machine

Tasks:
- create `ILocomotionState`
- implement:
  - `IdleState`
  - `MoveState`
  - `SprintState`
  - `JumpState`
  - `FallState`
  - `LandingState`
- move movement/rotation/gravity rules into states

Exit criteria:
- stable state transitions
- animator receives:
  - `MoveSpeed`
  - `Grounded`
  - `VerticalSpeed`

## Phase 3 - Action Axis

Goal:
- separate temporary gameplay actions from locomotion

Tasks:
- create `ActionStateMachine`
- define:
  - `None`
  - `Dodge`
  - `Attack`
  - `Interact`
- add state locks and transition guards

Exit criteria:
- actions no longer need to be hard-coded inside locomotion states

## Phase 4 - Weapon Axis

Goal:
- support multiple weapon stances without duplicating locomotion states

Tasks:
- create `WeaponDefinition`
- create `WeaponStateMachine`
- define locomotion modifiers per weapon:
  - movement multiplier
  - turn behavior
  - animation profile
  - traversal policy
- add basic weapon switching support

Exit criteria:
- same locomotion states work with different weapon contexts
- weapon-specific animation differences can be driven without branching the whole controller

## Phase 5 - Animator Structure Upgrade

Goal:
- make animation scalable for action RPG gameplay

Tasks:
- split animator responsibilities:
  - Base layer: locomotion
  - Upper body layer: weapon pose and combat
  - Additive layer: aim/reaction/recoil if needed
- centralize animator parameter writes in `PlayerAnimationBridge`

Suggested parameters:
- `MoveSpeed`
- `Grounded`
- `VerticalSpeed`
- `WeaponType`
- `ActionState`
- `TraversalType`
- `IsAiming`
- `IsLocked`

Exit criteria:
- lower-body locomotion and upper-body weapon logic are no longer tightly coupled

## Phase 6 - Traversal Detection

Goal:
- detect traversal opportunities before animating them

Tasks:
- implement:
  - `WallSensor`
  - `LedgeSensor`
  - `ObstacleSensor`
  - `TraversalQuery`
- implement `TraversalActionResolver`
- start with two actions only:
  - `Vault`
  - `Mantle`

Exit criteria:
- debug gizmos clearly show detection results
- resolver can distinguish low obstacle vs climbable ledge

## Phase 7 - Traversal State

Goal:
- connect traversal detection to actual state transitions

Tasks:
- add `TraversalState`
- introduce action objects:
  - `VaultTraversalAction`
  - `MantleTraversalAction`
- lock locomotion/action flow during traversal execution

Exit criteria:
- player can enter and exit traversal reliably
- state returns to `Move`, `Idle`, or `Fall` after traversal

## Phase 8 - Traversal Animation Integration

Goal:
- make traversal look natural instead of just functionally correct

Tasks:
- align player before animation start
- support root motion where needed
- correct end position and rotation
- add animation events or state machine behaviours for timing hooks

Exit criteria:
- traversal start and finish do not visibly drift
- hands and body line up with obstacle well enough before IK

## Phase 9 - Climbing Expansion

Goal:
- extend traversal into longer vertical movement

Tasks:
- add:
  - `LedgeHang`
  - `ClimbUp`
  - `Shimmy`
  - `DropDown`
- define climb transition rules and exit rules

Exit criteria:
- ledge hang and climb-up loop are stable

## Phase 10 - Weapon + Traversal Rules

Goal:
- support action RPG constraints for gear and traversal

Tasks:
- create traversal policy per weapon
- define whether each weapon:
  - can traverse directly
  - must auto-sheathe
  - is blocked from certain traversal types

Exit criteria:
- traversal behavior changes correctly depending on equipped weapon

## Phase 11 - Combat and Movement Coordination

Goal:
- make the controller feel like an action RPG, not separate systems glued together

Tasks:
- add cancel windows
- define priority rules
- define movement restrictions during attack/skill/dodge/traversal
- define hit reaction policy

Exit criteria:
- movement, actions, and weapon stance no longer conflict unpredictably

## Phase 12 - Polish

Tasks:
- hand IK
- foot IK
- slope adaptation
- camera damping by state
- stamina integration
- network sync strategy for traversal state
- debug HUD for state inspection

---

## Implementation Order

Use this order unless a strong reason appears to change it:

1. Phase 1
2. Phase 2
3. Phase 3
4. Phase 4
5. Phase 5
6. Phase 6
7. Phase 7
8. Phase 8
9. Phase 9
10. Phase 10
11. Phase 11
12. Phase 12

Do not start with climbing, full parkour, or weapon-specific locomotion variants before the base locomotion pipeline is stable.

---

## Immediate Next Work

Recommended next coding task:

1. Refactor `PlayerController` into:
   - `PlayerInputReader`
   - `CharacterMotor`
   - `GroundSensor`
   - `LocomotionContext`
2. Make input initialization safe under VContainer lifecycle timing.
3. Introduce `LocomotionStateMachine` with:
   - `Idle`
   - `Move`
   - `Sprint`
   - `Jump`
   - `Fall`
   - `Landing`

Do not begin traversal implementation before this step is complete.

---

## Resume Checklist For New Chat

When continuing this work in a new chat:

1. Read this file.
2. Inspect current `PlayerController` and any newly added locomotion files.
3. Confirm which phase is in progress.
4. Continue from the current phase instead of jumping ahead.
5. Keep all new work aligned with the architecture in this file unless the document is intentionally updated.

If the architecture changes materially, update this file in the same change set.

---

## Files Expected To Change Early

- `Assets/Script/Main/Character/PlayerController.cs`
- `Assets/Script/Main/Inputs/PlayerInputActions.cs` if input asset changes are required
- `Assets/Script/VContainer/Installers/Scenes/MainLifetimeScope.cs`
- `Assets/Script/VContainer/Initializer/MainSceneInitializer.cs`

Likely new files:
- `Assets/Script/Main/Locomotion/Core/*`
- `Assets/Script/Main/Locomotion/States/*`
- `Assets/Script/Main/Locomotion/Sensors/*`
- `Assets/Script/Main/Locomotion/Animation/*`
- `Assets/Script/Main/Weapons/*`

---

## Notes

- Use the Sunny Valley cleaner-code direction as a reference:
  - composition over monolithic controllers
  - state pattern for movement extensions
  - interface-based expansion
- Keep the first working version simple.
- Expand only after the previous phase is stable and testable.
