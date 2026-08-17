# Vehicle System — GTA III Playability Checklist

## Priority Definitions

**P0 — Story Critical**
Required for the player to reasonably play GTA III's main story from beginning to end.

**P1 — Full GTA III Critical**
Required to meet the project's target of supporting the main game and side content.

**P2 — Parity / Polish**
Desirable for GTA III accuracy, but must not hold up a playable implementation.

The goal is **not** to reproduce every detail of the original vehicle implementation before vehicles become usable. Implement the smallest compatible system first, then increase behavioural accuracy.

`[X]` means that a corresponding code path exists in the current baseline. It does not imply runtime validation, GTA III parity, or mission readiness.

---

# 1. Core Vehicle Architecture

* [X] **[P0]** Create a common `Vehicle` base type for functionality shared by all vehicles.
* [X] **[P0]** Create an automobile implementation for cars, vans, trucks and similar road vehicles.
* [ ] **[P0]** Create a separate boat implementation.
* [ ] **[P2]** Create a dedicated train implementation.
* [ ] **[P2]** Add special handling for aircraft/Dodo behaviour.
* [ ] **[P1]** Support remotely controlled vehicles such as the RC Bandit.
* [X] **[P0]** Give every spawned vehicle a stable runtime identifier.
* [ ] **[P0]** Allow mission/script systems to retain references to vehicles.
* [X] **[P0]** Detect references to vehicles that have been destroyed or despawned.
* [X] **[P0]** Define basic vehicle states such as:

  * Player controlled
  * AI controlled
  * Parked
  * Abandoned
  * Mission controlled
  * Wrecked
* [X] **[P0]** Allow transition between these states without recreating the vehicle.
* [X] **[P0]** Keep vehicle gameplay logic separate from rendering/model loading.
* [X] **[P0]** Make vehicle physics operate from `FixedUpdate` or an equivalent fixed simulation step.
* [X] **[P0]** Prevent frame rate from materially changing acceleration, steering, suspension or braking.
* [X] **[P0]** Allow vehicles to be explicitly marked as mission-owned.
* [X] **[P0]** Prevent normal world cleanup from removing mission-owned vehicles.
* [X] **[P0]** Provide an explicit cleanup path when missions release vehicles.

---

# 2. GTA III Vehicle Data

## IDE Vehicle Definitions

* [X] **[P0]** Parse GTA III vehicle definitions from the appropriate IDE data.
* [X] **[P0]** Resolve vehicle model ID.
* [X] **[P0]** Resolve model name.
* [X] **[P0]** Resolve texture dictionary.
* [X] **[P0]** Resolve handling ID.
* [X] **[P0]** Resolve vehicle type/class.
* [ ] **[P0]** Resolve vehicle animation group where required.
* [X] **[P0]** Resolve wheel scale.
* [ ] **[P0]** Resolve relevant vehicle flags.
* [X] **[P1]** Resolve vehicle frequency/classification data required by traffic spawning.
* [ ] **[P2]** Support obscure vehicle definition fields that have no gameplay effect yet.

## handling.cfg

* [X] **[P0]** Load GTA III `handling.cfg`.
* [X] **[P0]** Match handling entries to vehicle definitions.
* [X] **[P0]** Load mass.
* [ ] **[P0]** Load turn mass / rotational inertia equivalent.
* [ ] **[P0]** Load drag multiplier.
* [X] **[P0]** Load centre of mass.
* [X] **[P0]** Load percent-submerged/buoyancy-related values where applicable.
* [X] **[P0]** Load traction multiplier.
* [X] **[P0]** Load traction loss.
* [X] **[P0]** Load traction bias.
* [X] **[P0]** Load number of gears.
* [X] **[P0]** Load maximum velocity.
* [X] **[P0]** Load engine acceleration.
* [X] **[P0]** Load drive type:

  * Front-wheel drive
  * Rear-wheel drive
  * All-wheel drive
* [X] **[P0]** Load brake deceleration.
* [X] **[P0]** Load brake bias.
* [X] **[P0]** Load steering lock.
* [X] **[P0]** Load suspension force.
* [X] **[P0]** Load suspension damping.
* [X] **[P0]** Load suspension upper limit.
* [X] **[P0]** Load suspension lower limit.
* [X] **[P0]** Load suspension bias.
* [ ] **[P1]** Support handling flags that materially change gameplay.
* [ ] **[P2]** Support handling fields that only produce minor behavioural differences.

## Colours

* [ ] **[P0]** Parse `carcols.dat`.
* [ ] **[P0]** Associate allowed colours with each vehicle.
* [ ] **[P0]** Apply randomly selected primary/secondary colours at spawn.
* [ ] **[P0]** Allow scripts to override vehicle colours.
* [ ] **[P2]** Match GTA III's exact random colour selection behaviour.

---

# 3. Vehicle Model Setup

* [X] **[P0]** Spawn a DFF vehicle model as a usable Unity object.
* [X] **[P0]** Identify the chassis/root.
* [X] **[P0]** Identify front-left wheel.
* [X] **[P0]** Identify front-right wheel.
* [X] **[P0]** Identify rear-left wheel.
* [X] **[P0]** Identify rear-right wheel.
* [X] **[P0]** Determine wheel contact positions from model data.
* [X] **[P0]** Identify driver door.
* [X] **[P0]** Identify passenger doors where present.
* [ ] **[P0]** Identify seat/entry positions.
* [ ] **[P0]** Determine how many occupants the vehicle can carry.
* [X] **[P1]** Identify bonnet.
* [X] **[P1]** Identify boot.
* [ ] **[P1]** Identify bumpers and other damageable components where needed.
* [X] **[P1]** Identify headlights.
* [X] **[P1]** Identify rear lights.
* [ ] **[P1]** Identify siren/emergency light components.
* [ ] **[P1]** Identify special vehicle nodes such as Firetruck or Rhino components.
* [X] **[P0]** Attach the correct collision representation.
* [X] **[P0]** Calculate/configure appropriate Rigidbody mass.
* [X] **[P0]** Configure centre of mass.
* [ ] **[P0]** Configure rotational inertia sufficiently closely to GTA III behaviour.
* [X] **[P0]** Ensure visual model orientation matches physics orientation.

---

# 4. Automobile Suspension

The automobile simulation should eventually follow the same basic division used by GTA III: find wheel contact, calculate suspension/contact velocity, calculate longitudinal/lateral forces, constrain those forces by available traction, and apply them to the body.

* [X] **[P0]** Perform ground detection independently for each wheel.
* [X] **[P0]** Determine wheel contact point.
* [X] **[P0]** Determine wheel contact normal.
* [ ] **[P0]** Determine contacted surface.
* [X] **[P0]** Calculate suspension compression.
* [X] **[P0]** Calculate spring force.
* [X] **[P0]** Calculate damping force.
* [X] **[P0]** Clamp suspension travel between configured limits.
* [X] **[P0]** Apply suspension force at the wheel position rather than only at the centre of mass.
* [ ] **[P0]** Produce stable suspension while stationary.
* [ ] **[P0]** Prevent suspension forces from launching vehicles into the air.
* [ ] **[P0]** Correctly support vehicles on slopes.
* [X] **[P0]** Correctly transition wheels between grounded and airborne.
* [X] **[P0]** Allow individual wheels to lose contact.
* [ ] **[P0]** Maintain vehicle stability when driving over kerbs.
* [ ] **[P0]** Maintain vehicle stability when landing after a jump.
* [ ] **[P2]** Match GTA III suspension oscillation closely.
* [ ] **[P2]** Match model-specific anti-dive behaviour.

---

# 5. Wheel Physics

* [X] **[P0]** Calculate velocity at each wheel contact point.
* [X] **[P0]** Separate contact velocity into forward and lateral components.
* [X] **[P0]** Calculate force required to resist sideways sliding.
* [X] **[P0]** Calculate longitudinal engine force.
* [X] **[P0]** Calculate braking force.
* [X] **[P0]** Combine longitudinal and lateral forces.
* [X] **[P0]** Limit combined force by available tyre adhesion.
* [X] **[P0]** Apply resulting force at the wheel contact position.
* [X] **[P0]** Allow applied wheel force to generate body torque naturally.
* [X] **[P0]** Respect front/rear/all-wheel drivetrain configuration.
* [X] **[P0]** Respect front/rear brake bias.
* [ ] **[P0]** Apply handbrake primarily to the rear wheels.
* [X] **[P0]** Reduce lateral sliding under normal grip.
* [X] **[P0]** Allow controlled sliding once grip is exceeded.
* [X] **[P0]** Track wheel rotational speed.
* [X] **[P0]** Update visual wheel rotation from wheel speed.
* [X] **[P0]** Update front wheel visual steering angle.
* [ ] **[P1]** Track wheel states such as:

  * Normal
  * Spinning
  * Skidding
  * Locked
* [ ] **[P1]** Trigger tyre/skid effects from wheel state.
* [ ] **[P1]** Support burst tyres.
* [ ] **[P1]** Reduce traction for damaged/burst wheels.
* [ ] **[P2]** Match GTA III's exact wheel-slip thresholds.

GTA III processes individual wheel contacts with drivetrain-specific thrust, braking, traction and wheel damage rather than treating the vehicle as a single acceleration force.

---

# 6. Engine and Transmission

* [X] **[P0]** Accept a normalised throttle input.
* [X] **[P0]** Convert throttle into engine acceleration.
* [X] **[P0]** Apply engine power only to driven wheels.
* [X] **[P0]** Implement forward acceleration.
* [X] **[P0]** Implement reversing.
* [X] **[P0]** Distinguish braking from reversing.
* [X] **[P0]** Prevent instant transition from high forward speed into reverse.
* [X] **[P0]** Implement maximum forward velocity.
* [X] **[P0]** Implement lower reverse maximum velocity.
* [X] **[P0]** Implement basic automatic gears.
* [X] **[P0]** Shift upward as vehicle speed increases.
* [X] **[P0]** Shift downward as vehicle speed decreases.
* [X] **[P0]** Use gear state to affect available acceleration.
* [ ] **[P0]** Allow engine braking/coasting.
* [X] **[P0]** Allow vehicles with different handling data to feel materially different.
* [ ] **[P2]** Reproduce exact GTA III gearbox transition points.
* [ ] **[P2]** Reproduce unusual transmission quirks where confirmed.

---

# 7. Steering

* [X] **[P0]** Read player steering input.
* [X] **[P0]** Smooth steering input in a GTA III-like manner.
* [X] **[P0]** Clamp steering input.
* [X] **[P0]** Apply steering lock from handling data.
* [X] **[P0]** Steer front wheel direction.
* [ ] **[P0]** Reduce unstable steering behaviour at high speed.
* [X] **[P0]** Return steering toward centre when input is released.
* [X] **[P0]** Ensure steering behaviour remains stable across frame rates.
* [X] **[P0]** Allow steering while reversing.
* [ ] **[P0]** Preserve expected reversed steering response.
* [ ] **[P1]** Integrate steering with tyre slip/skidding.
* [ ] **[P2]** Reproduce GTA III's exact nonlinear steering response.

---

# 8. Player Vehicle Controls

* [X] **[P0]** Accelerate.
* [X] **[P0]** Brake.
* [X] **[P0]** Reverse.
* [X] **[P0]** Steer left/right.
* [X] **[P0]** Handbrake.
* [X] **[P0]** Enter vehicle.
* [X] **[P0]** Exit vehicle.
* [ ] **[P0]** Horn.
* [ ] **[P1]** Toggle/use siren.
* [ ] **[P1]** Look left.
* [ ] **[P1]** Look right.
* [ ] **[P0]** Look behind.
* [ ] **[P1]** Change radio station.
* [ ] **[P1]** Perform drive-by attack.
* [ ] **[P1]** Activate vehicle side mission.
* [ ] **[P1]** Fire special vehicle weapon.
* [ ] **[P1]** Manually detonate RC vehicle where applicable.

---

# 9. Entering Vehicles

* [X] **[P0]** Detect nearby enterable vehicles.
* [ ] **[P0]** Determine an appropriate entry door.
* [ ] **[P0]** Determine whether that door is accessible.
* [X] **[P0]** Reject entry when vehicle is too far away.
* [ ] **[P0]** Reject entry when the vehicle cannot legally be entered.
* [X] **[P0]** Support entering an empty vehicle.
* [ ] **[P0]** Support stealing a vehicle containing an AI driver.
* [ ] **[P0]** Remove/eject the existing driver during carjacking.
* [ ] **[P0]** Place Claude in the driver seat.
* [X] **[P0]** Parent or otherwise synchronise Claude with the moving vehicle during the enter sequence.
* [X] **[P0]** Disable normal on-foot movement while entering.
* [X] **[P0]** Transition player state to driving.
* [X] **[P0]** Transfer control to the vehicle.
* [ ] **[P0]** Transition camera to vehicle mode.
* [ ] **[P0]** Prevent two occupants from occupying the same seat.
* [ ] **[P0]** Support entering vehicles that already contain passengers.
* [ ] **[P0]** Support locked vehicles.
* [ ] **[P0]** Allow mission scripts to lock/unlock doors.
* [ ] **[P1]** Support alternative entry animations when doors are blocked.
* [ ] **[P1]** Support pulling occupants through the appropriate door.
* [ ] **[P2]** Match every original GTA III carjacking animation case.

---

# 10. Exiting Vehicles

* [ ] **[P0]** Exit through an appropriate door.
* [ ] **[P0]** Check for enough space to exit.
* [ ] **[P0]** Place Claude safely outside the vehicle.
* [X] **[P0]** Restore on-foot controller.
* [ ] **[P0]** Restore on-foot camera.
* [ ] **[P0]** Transition vehicle from player-controlled to abandoned/AI state.
* [X] **[P0]** Allow exit while vehicle is stationary.
* [X] **[P0]** Allow exit while moving slowly.
* [ ] **[P1]** Implement GTA III-like behaviour for exiting moving vehicles.
* [ ] **[P0]** Prevent exits where surrounding geometry makes them impossible.
* [ ] **[P0]** Try another door when the preferred exit is obstructed.
* [ ] **[P0]** Handle vehicles on steep slopes.
* [ ] **[P0]** Handle overturned vehicles.
* [ ] **[P0]** Handle emergency exit from burning vehicles.
* [ ] **[P1]** Support AI occupant exit using the same seat/door system.

---

# 11. Vehicle Doors and Moving Components

* [ ] **[P0]** Open driver door during entry.
* [ ] **[P0]** Close driver door after entry.
* [ ] **[P0]** Open door during exit.
* [ ] **[P0]** Close door after exit where appropriate.
* [X] **[P0]** Maintain door state:

  * Closed
  * Open
  * Swinging
* [X] **[P0]** Support door opening direction/rotation from model setup.
* [X] **[P0]** Keep door animation stable on moving vehicles.
* [ ] **[P1]** Allow doors to swing because of motion.
* [X] **[P1]** Allow doors to be damaged.
* [ ] **[P2]** Allow doors to detach.
* [ ] **[P2]** Animate damaged bonnet.
* [ ] **[P2]** Animate damaged boot.
* [ ] **[P2]** Support detachable bumpers and other panels.

---

# 12. Occupants and Seats

* [X] **[P0]** Associate a driver with a vehicle.
* [ ] **[P0]** Associate passengers with a vehicle.
* [ ] **[P0]** Track seat occupancy.
* [ ] **[P0]** Support front passenger.
* [ ] **[P0]** Support rear passengers where model permits.
* [ ] **[P0]** Allow mission NPCs to enter Claude's vehicle.
* [ ] **[P0]** Allow mission NPCs to exit Claude's vehicle.
* [ ] **[P0]** Allow AI drivers to enter/exit vehicles.
* [X] **[P0]** Keep occupants correctly positioned as vehicle moves.
* [X] **[P0]** Hide/disable inappropriate pedestrian movement while seated.
* [X] **[P0]** Keep seated pedestrian animation active.
* [ ] **[P0]** Clean occupant relationships if either the ped or vehicle is destroyed.
* [ ] **[P0]** Handle occupant death while inside a vehicle.
* [ ] **[P0]** Handle vehicle destruction while occupied.
* [ ] **[P1]** Support passengers fleeing damaged vehicles.
* [ ] **[P1]** Support hostile occupants.
* [ ] **[P1]** Support mission-controlled occupant behaviour.

---

# 13. Vehicle Collision

* [X] **[P0]** Vehicle versus world collision.
* [X] **[P0]** Vehicle versus vehicle collision.
* [X] **[P0]** Vehicle versus pedestrian collision.
* [X] **[P0]** Vehicle versus dynamic object collision.
* [X] **[P0]** Apply collision impulses using vehicle mass.
* [X] **[P0]** Prevent vehicles tunnelling through common world geometry.
* [ ] **[P0]** Prevent cars becoming permanently embedded in roads.
* [ ] **[P0]** Handle kerbs and small steps reasonably.
* [X] **[P0]** Allow vehicles to push lighter vehicles.
* [X] **[P0]** Make trucks/heavy vehicles noticeably harder to push.
* [X] **[P0]** Apply collision damage based on impact severity.
* [ ] **[P0]** Damage pedestrians hit by vehicles.
* [X] **[P0]** Allow collisions to create spin/rotation.
* [ ] **[P0]** Correctly handle rollovers.
* [ ] **[P1]** Generate collision sound based on severity/material.
* [ ] **[P1]** Generate particles from significant collisions.

---

# 14. Vehicle Damage and Health

* [X] **[P0]** Give every damageable vehicle health.
* [ ] **[P0]** Allow scripts to read vehicle health.
* [ ] **[P0]** Allow scripts to set vehicle health.
* [X] **[P0]** Apply collision damage.
* [ ] **[P0]** Apply bullet damage.
* [ ] **[P0]** Apply melee damage where appropriate.
* [ ] **[P0]** Apply explosive damage.
* [X] **[P0]** Apply fire damage.
* [X] **[P0]** Apply damage from other vehicles.
* [ ] **[P0]** Support bulletproof vehicles.
* [ ] **[P0]** Support fireproof vehicles.
* [ ] **[P0]** Support explosion-proof vehicles.
* [ ] **[P0]** Support collision-proof vehicles where scripts require it.
* [ ] **[P0]** Support generally invulnerable mission vehicles.
* [ ] **[P0]** Allow scripts to enable/disable damage.
* [X] **[P0]** Allow mission logic to query whether a vehicle is dead/destroyed.
* [ ] **[P1]** Track individual wheel damage.
* [X] **[P1]** Track door damage.
* [X] **[P1]** Track bonnet damage.
* [X] **[P1]** Track boot damage.
* [ ] **[P2]** Match GTA III's detailed component damage model.

The underlying GTA III vehicle implementation includes distinct proof states and damage paths, so mission-level invulnerability should be considered gameplay functionality rather than cosmetic vehicle damage work.

---

# 15. Fire, Explosion and Wrecks

* [X] **[P0]** Transition heavily damaged vehicle into burning state.
* [X] **[P0]** Run a burn/explosion timer.
* [X] **[P0]** Explode vehicle after critical damage.
* [ ] **[P0]** Allow immediate destruction from sufficiently powerful explosions.
* [ ] **[P0]** Damage nearby pedestrians during explosion.
* [ ] **[P0]** Damage nearby vehicles during explosion.
* [ ] **[P0]** Support chain-reaction vehicle explosions.
* [ ] **[P0]** Kill or appropriately damage occupants of exploding vehicles.
* [X] **[P0]** Transition destroyed vehicle into a wreck.
* [ ] **[P0]** Disable engine control on wrecked vehicles.
* [X] **[P0]** Keep wreck collision active.
* [ ] **[P0]** Mark wreck as invalid for normal entry.
* [X] **[P0]** Eventually clean non-mission wrecks from the world.
* [X] **[P1]** Spawn fire particles.
* [X] **[P1]** Spawn smoke particles.
* [ ] **[P1]** Spawn explosion effect.
* [X] **[P1]** Apply scorch/damaged visual state.
* [ ] **[P2]** Reproduce individual vehicle deformation/damage meshes accurately.

---

# 16. Water Interaction

## Road Vehicles

* [ ] **[P0]** Detect vehicle entering water.
* [ ] **[P0]** Apply buoyancy according to vehicle configuration.
* [ ] **[P0]** Allow ordinary vehicles to sink.
* [ ] **[P0]** Disable normal driving once sufficiently submerged.
* [ ] **[P0]** Handle occupants trapped in submerged vehicles.
* [ ] **[P0]** Mark fully submerged/destroyed vehicles appropriately.
* [ ] **[P1]** Produce splash effects.
* [ ] **[P1]** Produce water-entry sounds.

---

# 17. Boats

Boat support is **P0 for completing the story**, not an optional later vehicle experiment.

* [ ] **[P0]** Spawn GTA III boat models.
* [ ] **[P0]** Detect water surface.
* [ ] **[P0]** Calculate boat buoyancy.
* [ ] **[P0]** Keep boat stable while floating.
* [ ] **[P0]** Allow waves/water contact to influence boat orientation sufficiently for believable movement.
* [ ] **[P0]** Implement forward thrust.
* [ ] **[P0]** Implement reverse.
* [ ] **[P0]** Implement boat steering.
* [ ] **[P0]** Apply appropriate water resistance.
* [ ] **[P0]** Reduce sideways movement.
* [ ] **[P0]** Allow boat to collide with world geometry.
* [ ] **[P0]** Allow boat to collide with other vehicles.
* [ ] **[P0]** Detect when a boat is substantially on land.
* [ ] **[P0]** Allow player to enter a boat.
* [ ] **[P0]** Allow player to exit a boat.
* [ ] **[P0]** Support boat health.
* [ ] **[P0]** Support boat destruction.
* [ ] **[P0]** Allow missions to spawn and reference boats.
* [ ] **[P0]** Allow missions to determine whether player is in the required boat.
* [ ] **[P0]** Allow boats to collect mission pickups/checkpoints.
* [ ] **[P1]** Animate propellers.
* [ ] **[P1]** Produce wake effects.
* [ ] **[P1]** Produce spray effects.
* [ ] **[P1]** Implement AI-controlled boats if required by mission/world behaviour.
* [ ] **[P2]** Match GTA III's exact boat rocking/wake simulation.

Boats use their own `Update` path in GTA3 rather than the automobile wheel simulation.

---

# 18. Vehicle Camera

* [ ] **[P0]** Switch to vehicle camera after entering.
* [ ] **[P0]** Follow vehicle position.
* [ ] **[P0]** Follow vehicle heading.
* [ ] **[P0]** Smooth camera motion.
* [ ] **[P0]** Prevent normal vehicle acceleration from causing uncomfortable camera oscillation.
* [ ] **[P0]** Handle camera collision with world geometry.
* [ ] **[P0]** Support looking behind.
* [ ] **[P1]** Support left/right look.
* [ ] **[P1]** Support drive-by aiming view.
* [ ] **[P0]** Restore pedestrian camera on exit.
* [ ] **[P2]** Add GTA III cinematic vehicle camera.
* [ ] **[P2]** Closely reproduce GTA III speed-dependent camera behaviour.

---

# 19. Basic Traffic AI

* [ ] **[P0]** Spawn AI traffic.
* [ ] **[P0]** Assign an AI driver.
* [ ] **[P0]** Place traffic on valid road paths.
* [ ] **[P0]** Follow road path nodes.
* [ ] **[P0]** Steer toward path targets.
* [ ] **[P0]** Accelerate toward desired traffic speed.
* [ ] **[P0]** Brake for slower vehicles.
* [ ] **[P0]** Avoid directly driving into stopped vehicles where practical.
* [ ] **[P0]** Perform road turns.
* [ ] **[P0]** Follow the correct traffic direction.
* [ ] **[P0]** Despawn traffic sufficiently far from player.
* [ ] **[P0]** Avoid despawning vehicles visible to the player.
* [ ] **[P0]** Avoid despawning vehicles involved in missions.
* [ ] **[P0]** Allow abandoned AI cars to become enterable.
* [ ] **[P0]** Allow AI drivers to react after collisions.
* [ ] **[P1]** Stop at traffic lights.
* [ ] **[P1]** React to emergency vehicle sirens.
* [ ] **[P1]** Attempt to drive around obstructions.
* [ ] **[P1]** Recover from simple stuck situations.
* [ ] **[P1]** Spawn appropriate vehicle classes in appropriate areas.
* [ ] **[P1]** Respect traffic density settings.
* [ ] **[P2]** Match GTA III traffic population distribution exactly.
* [ ] **[P2]** Match original despawn distances exactly.

---

# 20. Mission Vehicle AI

This is more important than sophisticated ambient traffic AI.

* [ ] **[P0]** Tell an AI vehicle to drive to a location.
* [ ] **[P0]** Tell an AI vehicle to follow another entity.
* [ ] **[P0]** Tell an AI vehicle to chase the player.
* [ ] **[P0]** Tell an AI vehicle to flee from the player.
* [ ] **[P0]** Tell an AI vehicle to stop.
* [ ] **[P0]** Tell an AI vehicle to remain parked.
* [ ] **[P0]** Tell an AI vehicle to follow a scripted route.
* [ ] **[P0]** Specify desired speed.
* [ ] **[P0]** Specify driving aggressiveness/behaviour sufficiently for missions.
* [ ] **[P0]** Allow mission vehicles to ignore normal traffic cleanup.
* [ ] **[P0]** Allow script to replace/change driver.
* [ ] **[P0]** Detect arrival at destination.
* [ ] **[P0]** Detect if mission vehicle becomes stuck or destroyed.
* [ ] **[P0]** Allow scripted cars to pursue Claude.
* [ ] **[P0]** Allow scripted cars to escape from Claude.
* [ ] **[P1]** Support aggressive ramming behaviour.
* [ ] **[P1]** Support drive-by passengers where missions require them.
* [ ] **[P1]** Support scripted convoy behaviour.

---

# 21. Police Vehicles

* [ ] **[P0]** Spawn police cars.
* [ ] **[P0]** Spawn police drivers.
* [ ] **[P0]** Dispatch police cars according to wanted system requests.
* [ ] **[P0]** Pursue player's vehicle.
* [ ] **[P0]** Attempt to intercept player.
* [ ] **[P0]** Stop police vehicle sufficiently close for officers to exit.
* [ ] **[P0]** Allow police officers to exit and continue pursuit on foot.
* [ ] **[P0]** Support police car siren.
* [ ] **[P1]** Allow traffic to react to police siren.
* [ ] **[P1]** Support police roadblocks.
* [ ] **[P1]** Support mission-specific police pursuit configurations.
* [ ] **[P2]** Reproduce exact GTA III police ramming tactics.

---

# 22. Vehicle Script Interface

The mission system should not need to know anything about Unity physics internals.

* [ ] **[P0]** Create vehicle.
* [ ] **[P0]** Delete vehicle.
* [ ] **[P0]** Retrieve vehicle by script handle.
* [ ] **[P0]** Set vehicle position.
* [ ] **[P0]** Set vehicle heading.
* [ ] **[P0]** Read vehicle position.
* [ ] **[P0]** Read vehicle speed.
* [ ] **[P0]** Set vehicle speed where required.
* [ ] **[P0]** Check whether vehicle exists.
* [ ] **[P0]** Check whether vehicle is destroyed.
* [ ] **[P0]** Check whether vehicle is in an area.
* [ ] **[P0]** Check whether vehicle is near a point/entity.
* [ ] **[P0]** Check whether player is inside a specific vehicle.
* [ ] **[P0]** Check whether player is inside a vehicle type/model.
* [ ] **[P0]** Set vehicle health.
* [ ] **[P0]** Read vehicle health.
* [ ] **[P0]** Make vehicle immune to damage.
* [ ] **[P0]** Set individual proof flags.
* [ ] **[P0]** Lock/unlock doors.
* [ ] **[P0]** Set colours.
* [ ] **[P0]** Assign driver.
* [ ] **[P0]** Add passenger.
* [ ] **[P0]** Remove occupant.
* [ ] **[P0]** Assign AI driving objective.
* [ ] **[P0]** Set desired AI speed.
* [ ] **[P0]** Mark vehicle as mission-owned.
* [ ] **[P0]** Release vehicle from mission ownership.
* [ ] **[P0]** Prevent normal despawning.
* [ ] **[P0]** Force vehicle cleanup.
* [ ] **[P0]** Enable/disable collision where mission scripting needs it.
* [ ] **[P1]** Set siren state.
* [ ] **[P1]** Set engine state.
* [ ] **[P1]** Set alarm state.
* [ ] **[P1]** Query seat occupancy.
* [ ] **[P1]** Query number of passengers.
* [ ] **[P1]** Query whether vehicle is upside down/on its side.
* [ ] **[P1]** Query damage state.

---

# 23. Car Bombs

* [ ] **[P0]** Represent whether a vehicle has a bomb installed.
* [ ] **[P0]** Support mission-installed bombs.
* [ ] **[P0]** Support bomb shop installation.
* [ ] **[P0]** Support bomb arming.
* [ ] **[P0]** Support required GTA III bomb activation behaviour.
* [ ] **[P0]** Detonate bomb through the normal vehicle explosion system.
* [ ] **[P0]** Notify mission scripting when relevant vehicle is destroyed.
* [ ] **[P0]** Preserve bomb state while player enters/exits vehicle.
* [ ] **[P1]** Support all GTA III bomb shop variations accurately.
* [ ] **[P1]** Add appropriate audio/visual feedback.

---

# 24. Garages and Vehicle Services

## Mission Garages

* [ ] **[P0]** Detect vehicle entering mission garage.
* [ ] **[P0]** Determine vehicle model.
* [ ] **[P0]** Determine whether it is the required mission vehicle.
* [ ] **[P0]** Allow scripts to open/close garage.
* [ ] **[P0]** Prevent garage door from producing catastrophic physics interactions.
* [ ] **[P0]** Notify mission when requested vehicle is inside.

## Bomb Shops

* [ ] **[P0]** Detect valid player vehicle.
* [ ] **[P0]** Close garage.
* [ ] **[P0]** Install appropriate bomb.
* [ ] **[P0]** Reopen garage.
* [ ] **[P0]** Return control to player.

## Pay 'n' Spray

* [ ] **[P1]** Detect valid player vehicle.
* [ ] **[P1]** Repair vehicle.
* [ ] **[P1]** Restore vehicle health.
* [ ] **[P1]** Repair damaged wheels/components as appropriate.
* [ ] **[P1]** Change vehicle colour.
* [ ] **[P1]** Integrate with wanted-level clearing behaviour.

## Safehouse Garages

* [ ] **[P1]** Detect parked vehicles inside garage.
* [ ] **[P1]** Store eligible vehicles.
* [ ] **[P1]** Restore stored vehicles when game is loaded.
* [ ] **[P1]** Preserve model.
* [ ] **[P1]** Preserve colour.
* [ ] **[P1]** Preserve reasonable damage/health state if required.

---

# 25. Vehicle Side Missions

These are **P1 because the project's completion goal includes side content**, even though none should block the initial story-playable milestone.

## Taxi

* [ ] **[P1]** Detect player driving a Taxi/Cabbie.
* [ ] **[P1]** Start Taxi Driver activity.
* [ ] **[P1]** Spawn/select passenger.
* [ ] **[P1]** Passenger enters vehicle.
* [ ] **[P1]** Set destination.
* [ ] **[P1]** Detect arrival.
* [ ] **[P1]** Passenger exits.
* [ ] **[P1]** Award fare.
* [ ] **[P1]** Track completed fares.
* [ ] **[P1]** Handle failure/vehicle exit.

## Paramedic

* [ ] **[P1]** Detect player driving Ambulance.
* [ ] **[P1]** Start Paramedic activity.
* [ ] **[P1]** Spawn/select patients.
* [ ] **[P1]** Patients enter ambulance.
* [ ] **[P1]** Support multiple passenger seats.
* [ ] **[P1]** Detect hospital delivery.
* [ ] **[P1]** Patients exit.
* [ ] **[P1]** Track levels.
* [ ] **[P1]** Track timer.
* [ ] **[P1]** Handle failure.

## Vigilante

* [ ] **[P1]** Detect valid law-enforcement vehicle.
* [ ] **[P1]** Start Vigilante activity.
* [ ] **[P1]** Spawn/select criminal vehicle.
* [ ] **[P1]** Give target vehicle fleeing AI.
* [ ] **[P1]** Track target destruction/occupant death.
* [ ] **[P1]** Select subsequent target.
* [ ] **[P1]** Track timer and progress.
* [ ] **[P1]** Handle failure.

## Firefighter

* [ ] **[P1]** Detect player driving Firetruck.
* [ ] **[P1]** Start Firefighter activity.
* [ ] **[P1]** Select/create burning targets.
* [ ] **[P1]** Implement controllable water cannon.
* [ ] **[P1]** Detect water hitting target.
* [ ] **[P1]** Extinguish burning vehicle/pedestrian.
* [ ] **[P1]** Track completion.
* [ ] **[P1]** Handle failure.

---

# 26. RC Vehicles

RC Toyz missions use RC Bandits and count toward GTA III's completion content.

* [ ] **[P1]** Spawn RC Bandit.
* [ ] **[P1]** Transfer player control from Claude to RC vehicle.
* [ ] **[P1]** Keep Claude/TOYZ van in appropriate mission state.
* [ ] **[P1]** Implement RC acceleration.
* [ ] **[P1]** Implement RC steering.
* [ ] **[P1]** Implement RC suspension.
* [ ] **[P1]** Implement remote/manual detonation.
* [ ] **[P1]** Destroy nearby target vehicles.
* [ ] **[P1]** Track appropriate target vehicle models.
* [ ] **[P1]** Spawn replacement RC Bandit where required.
* [ ] **[P1]** Implement activity timer.
* [ ] **[P1]** Track score.
* [ ] **[P1]** End RC control cleanly.
* [ ] **[P1]** Restore normal player state/camera.

---

# 27. Off-Road Vehicle Challenges

* [ ] **[P1]** Recognise required challenge vehicle.
* [ ] **[P1]** Start challenge when appropriate.
* [ ] **[P1]** Track vehicle checkpoints.
* [ ] **[P1]** Allow checkpoint detection while airborne.
* [ ] **[P1]** Support timers.
* [ ] **[P1]** Ensure suspension/traction is sufficient for off-road challenge terrain.
* [ ] **[P1]** Support jumps without vehicle instability.
* [ ] **[P1]** Detect challenge completion/failure.

---

# 28. Import/Export, Crushers and Cranes

* [ ] **[P1]** Identify vehicle model reliably.
* [ ] **[P1]** Detect required import/export vehicles.
* [ ] **[P1]** Deliver vehicle to required location.
* [ ] **[P1]** Mark vehicle as delivered.
* [ ] **[P1]** Track completed vehicle lists.
* [ ] **[P1]** Support required emergency/special vehicle deliveries.
* [ ] **[P1]** Detect vehicle inside crusher.
* [ ] **[P1]** Remove crushed vehicle safely.
* [ ] **[P1]** Award appropriate gameplay result.
* [ ] **[P1]** Support crane interaction with vehicles sufficiently for GTA III content.

---

# 29. Special Road Vehicles

## Emergency Vehicles

* [ ] **[P1]** Police Car siren.
* [ ] **[P1]** Ambulance siren.
* [ ] **[P1]** Firetruck siren.
* [ ] **[P1]** Emergency lights.
* [ ] **[P1]** Traffic reaction to siren.

## Firetruck

* [ ] **[P1]** Rotatable water cannon.
* [ ] **[P1]** Fire water projectile/stream.
* [ ] **[P1]** Detect hits from water.
* [ ] **[P1]** Extinguish fires.

## Rhino

* [ ] **[P1]** Spawn and drive Rhino through automobile system where practical.
* [ ] **[P1]** Apply special mass/handling.
* [ ] **[P1]** Rotate/aim cannon as required.
* [ ] **[P1]** Fire cannon.
* [ ] **[P1]** Apply explosion damage.
* [ ] **[P1]** Apply cannon recoil if needed for gameplay.
* [ ] **[P1]** Implement Rhino-specific collision/destruction behaviour sufficiently for GTA III content.
* [ ] **[P2]** Match all original tank physics quirks.

---

# 30. Drive-By Shooting

* [ ] **[P1]** Determine whether current vehicle permits drive-by.
* [ ] **[P1]** Determine whether player has valid weapon.
* [ ] **[P1]** Allow left-side firing.
* [ ] **[P1]** Allow right-side firing.
* [ ] **[P1]** Aim from appropriate position.
* [ ] **[P1]** Spawn weapon projectiles/raycast from vehicle.
* [ ] **[P1]** Prevent shooting through inappropriate parts of vehicle.
* [ ] **[P1]** Play appropriate seated shooting animation.
* [ ] **[P1]** Integrate with vehicle camera.
* [ ] **[P1]** Allow mission logic involving vehicle shooting.

---

# 31. Car Generators and Parked Vehicles

* [ ] **[P0]** Load static/parked vehicle generator data.
* [ ] **[P0]** Spawn required parked vehicles.
* [ ] **[P0]** Respect required model.
* [ ] **[P0]** Respect position.
* [ ] **[P0]** Respect heading.
* [ ] **[P0]** Make generated vehicles enterable.
* [ ] **[P0]** Avoid duplicate spawning while existing generated vehicle remains nearby.
* [ ] **[P0]** Respawn appropriate vehicles after sufficient time/distance.
* [ ] **[P0]** Support mission-enabling/disabling generators where necessary.
* [ ] **[P1]** Respect configured colours/alarms/locked state where available.

---

# 32. Vehicle Rendering

* [X] **[P0]** Render chassis correctly.
* [ ] **[P0]** Render vehicle colours correctly.
* [X] **[P0]** Render wheels.
* [X] **[P0]** Rotate wheels according to speed.
* [X] **[P0]** Turn front wheels according to steering.
* [X] **[P0]** Move wheels according to suspension.
* [ ] **[P0]** Render moving doors correctly.
* [ ] **[P1]** Render headlights.
* [ ] **[P1]** Render tail lights.
* [ ] **[P1]** Render brake lights.
* [ ] **[P1]** Render reverse lights where GTA III uses them.
* [ ] **[P1]** Render emergency lights.
* [X] **[P1]** Render damaged/burned state.
* [ ] **[P2]** Support accurate environmental reflections.
* [ ] **[P2]** Match GTA III vehicle material effects exactly.
* [ ] **[P2]** Implement cosmetic detached vehicle components.

---

# 33. Vehicle Audio

* [ ] **[P0]** Engine idle sound.
* [ ] **[P0]** Engine acceleration/load sound.
* [ ] **[P0]** Adjust engine sound with vehicle speed/RPM approximation.
* [ ] **[P0]** Horn.
* [ ] **[P1]** Siren.
* [ ] **[P1]** Door opening sound.
* [ ] **[P1]** Door closing sound.
* [ ] **[P1]** Collision sounds.
* [ ] **[P1]** Tyre skid sound.
* [ ] **[P1]** Tyre burst sound.
* [ ] **[P1]** Vehicle fire sound.
* [ ] **[P0]** Vehicle explosion sound.
* [ ] **[P1]** Boat engine sound.
* [ ] **[P1]** Water/wake sounds.
* [ ] **[P1]** Hook player vehicle into radio system.
* [ ] **[P1]** Start/restore appropriate radio station on entry.
* [ ] **[P1]** Allow radio station switching.
* [ ] **[P2]** Closely reproduce original GTA III engine pitch calculations.

---

# 34. Surface Interaction and Weather

* [ ] **[P0]** Identify road/ground surface beneath each wheel.
* [X] **[P0]** Provide a default traction value for all normal surfaces.
* [ ] **[P1]** Support different grip for relevant surface types.
* [ ] **[P1]** Reduce grip on wet roads.
* [ ] **[P1]** Feed surface type into skid/dust effects.
* [ ] **[P1]** Feed surface type into tyre audio.
* [ ] **[P2]** Match GTA III's exact surface adhesion table.
* [ ] **[P2]** Match exact GTA III wet-road traction modifiers.

---

# 35. Vehicle Save/Load

* [ ] **[P1]** Save vehicles stored in safehouse garages.
* [ ] **[P1]** Save model ID.
* [ ] **[P1]** Save colours.
* [ ] **[P1]** Save position/garage slot as appropriate.
* [ ] **[P1]** Restore saved vehicle.
* [ ] **[P1]** Restore special vehicle properties where required.
* [ ] **[P1]** Ensure invalid models do not corrupt save loading.
* [ ] **[P1]** Ensure temporary traffic vehicles are not unnecessarily persisted.
* [ ] **[P0]** Restore mission/world car-generator state required by normal save progression.

---

# 36. Streaming and Cleanup

* [X] **[P0]** Vehicles may be spawned after models/textures become available.
* [X] **[P0]** Keep vehicle models loaded while vehicles using them exist.
* [X] **[P0]** Avoid releasing model resources still referenced by active vehicles.
* [X] **[P0]** Remove distant ambient traffic.
* [ ] **[P0]** Never automatically remove player's current vehicle.
* [X] **[P0]** Never automatically remove mission-owned vehicles.
* [X] **[P0]** Clean abandoned vehicles eventually.
* [X] **[P0]** Clean wrecks eventually.
* [ ] **[P0]** Clean associated occupants/references correctly.
* [ ] **[P0]** Ensure script vehicle handles become invalid safely after destruction.
* [ ] **[P1]** Use simplified simulation for sufficiently distant traffic if necessary.
* [ ] **[P2]** Reproduce GTA III's exact vehicle pool limits/cleanup heuristics.

---

# 37. Performance

* [ ] **[P0]** Avoid allocations in per-wheel physics loops.
* [X] **[P0]** Avoid `GetComponent` calls every physics frame.
* [X] **[P0]** Cache wheel/component references.
* [ ] **[P0]** Avoid logging per physics frame in release builds.
* [ ] **[P0]** Profile multiple simultaneous vehicles.
* [ ] **[P0]** Test realistic Liberty City traffic density.
* [ ] **[P0]** Ensure inactive parked vehicles are inexpensive.
* [ ] **[P0]** Ensure wrecks are inexpensive.
* [ ] **[P1]** Reduce distant traffic simulation frequency where practical.
* [ ] **[P1]** Pool temporary effects such as skid particles.
* [ ] **[P1]** Profile vehicle/pedestrian collision load.

---

# 38. Debugging Tools

* [ ] **[P0]** Debug command to spawn vehicle by model ID/name.
* [ ] **[P0]** Debug command to destroy current vehicle.
* [ ] **[P0]** Debug command to repair current vehicle.
* [ ] **[P0]** Display current speed.
* [ ] **[P0]** Display throttle.
* [ ] **[P0]** Display brake.
* [ ] **[P0]** Display steering input.
* [ ] **[P0]** Display current gear.
* [ ] **[P0]** Display grounded wheel count.
* [ ] **[P0]** Display individual suspension compression.
* [ ] **[P0]** Display individual wheel contact points.
* [ ] **[P0]** Display wheel forward/lateral velocity.
* [ ] **[P0]** Display wheel traction force.
* [ ] **[P0]** Display vehicle health.
* [ ] **[P0]** Display vehicle state.
* [ ] **[P0]** Display current driver/passengers.
* [ ] **[P0]** Visualise centre of mass.
* [ ] **[P0]** Visualise suspension rays/contact probes.
* [ ] **[P0]** Visualise AI path target.
* [ ] **[P0]** Visualise mission ownership.
* [ ] **[P1]** Display wheel state/skid state.
* [ ] **[P1]** Display current surface material.

---

# 39. Automobile Physics Acceptance Tests

## Stationary

* [ ] **[P0]** Vehicle can spawn above road and settle normally.
* [ ] **[P0]** Vehicle does not bounce continuously.
* [ ] **[P0]** Vehicle does not slowly sink through road.
* [ ] **[P0]** Vehicle remains approximately stationary with no input.
* [ ] **[P0]** Vehicle remains stable on moderate incline.

## Acceleration

* [ ] **[P0]** Vehicle accelerates from rest.
* [ ] **[P0]** Vehicle reaches sensible maximum speed.
* [ ] **[P0]** Vehicle does not exceed maximum velocity uncontrollably.
* [ ] **[P0]** FWD behaves correctly.
* [ ] **[P0]** RWD behaves correctly.
* [ ] **[P0]** AWD behaves correctly.
* [ ] **[P0]** Reverse behaves correctly.

## Braking

* [ ] **[P0]** Normal brake slows vehicle.
* [ ] **[P0]** Vehicle comes to rest.
* [ ] **[P0]** Brake does not cause vehicle instability.
* [ ] **[P0]** Handbrake locks/restricts rear wheels.
* [ ] **[P0]** Handbrake permits GTA-like slides.

## Steering

* [ ] **[P0]** Vehicle turns predictably at low speed.
* [ ] **[P0]** Vehicle remains controllable at high speed.
* [ ] **[P0]** Steering returns toward centre.
* [ ] **[P0]** Full left/right input does not cause unrealistic instantaneous rotation.

## Grip

* [ ] **[P0]** Vehicle resists sideways sliding under normal driving.
* [ ] **[P0]** Vehicle can lose traction.
* [ ] **[P0]** Vehicle can recover traction.
* [ ] **[P0]** Excessive throttle can produce wheel spin where appropriate.
* [ ] **[P0]** Hard braking can produce wheel lock/skid where appropriate.

## Terrain

* [ ] **[P0]** Drive over kerb.
* [ ] **[P0]** Drive down stairs without simulation failure.
* [ ] **[P0]** Drive over uneven road.
* [ ] **[P0]** Jump vehicle.
* [ ] **[P0]** Land vehicle.
* [ ] **[P0]** Roll vehicle.
* [ ] **[P0]** Drive on steep hill.
* [ ] **[P0]** Enter water.

## Collision

* [ ] **[P0]** Head-on car collision.
* [ ] **[P0]** Side-impact collision.
* [ ] **[P0]** Rear-end collision.
* [ ] **[P0]** Vehicle versus wall.
* [ ] **[P0]** Vehicle versus lamppost/prop.
* [ ] **[P0]** Vehicle versus pedestrian.
* [ ] **[P0]** Heavy vehicle versus light vehicle.

---

# 40. Representative Vehicle Testing

Do not test only one car.

* [ ] **[P0]** Typical light passenger car.
* [ ] **[P0]** Typical heavy passenger car.
* [ ] **[P0]** Sports car.
* [ ] **[P0]** Van.
* [ ] **[P0]** Truck.
* [ ] **[P0]** Police vehicle.
* [ ] **[P0]** Ambulance.
* [ ] **[P0]** Firetruck.
* [ ] **[P0]** Mission-specific vehicle.
* [ ] **[P0]** Boat.
* [ ] **[P1]** Taxi.
* [ ] **[P1]** Rhino.
* [ ] **[P1]** RC Bandit.
* [ ] **[P2]** Dodo.
* [ ] **[P2]** Train.

For every normal automobile:

* [ ] **[P0]** Model loads.
* [ ] **[P0]** Collision is valid.
* [ ] **[P0]** Wheels are located correctly.
* [ ] **[P0]** Handling entry resolves.
* [ ] **[P0]** Colours resolve.
* [ ] **[P0]** Vehicle can settle on road.
* [ ] **[P0]** Vehicle can accelerate.
* [ ] **[P0]** Vehicle can brake.
* [ ] **[P0]** Vehicle can steer.
* [ ] **[P0]** Vehicle can reverse.
* [ ] **[P0]** Vehicle can be entered.
* [ ] **[P0]** Vehicle can be exited.
* [ ] **[P0]** Vehicle can take damage.
* [ ] **[P0]** Vehicle can be destroyed.

---

# 41. Mission Compatibility Test Pass

Once the mission system exists, vehicle development should be driven by real GTA III mission failures.

* [ ] **[P0]** Start game and acquire first road vehicle normally.
* [ ] **[P0]** Complete early missions requiring ordinary driving.
* [ ] **[P0]** Complete missions requiring stealing a specific vehicle.
* [ ] **[P0]** Complete missions carrying NPC passengers.
* [ ] **[P0]** Complete missions following another vehicle.
* [ ] **[P0]** Complete missions chasing another vehicle.
* [ ] **[P0]** Complete missions where hostile vehicles chase the player.
* [ ] **[P0]** Complete missions requiring destruction of a vehicle.
* [ ] **[P0]** Complete missions where a target vehicle must survive.
* [ ] **[P0]** Complete missions involving locked vehicles.
* [ ] **[P0]** Complete missions involving mission garages.
* [ ] **[P0]** Complete missions involving vehicle bombs.
* [ ] **[P0]** Complete police pursuit sequences.
* [ ] **[P0]** Complete boat-dependent story content.
* [ ] **[P0]** Complete final mission using the normal vehicle system.

Then:

* [ ] **[P1]** Complete Taxi Driver.
* [ ] **[P1]** Complete Paramedic.
* [ ] **[P1]** Complete Vigilante.
* [ ] **[P1]** Complete Firefighter.
* [ ] **[P1]** Complete all RC Toyz activities.
* [ ] **[P1]** Complete all off-road vehicle challenges.
* [ ] **[P1]** Complete import/export vehicle lists.
* [ ] **[P1]** Complete crusher/crane vehicle content.
* [ ] **[P1]** Complete all optional missions involving vehicles.
* [ ] **[P1]** Verify vehicle-related requirements do not prevent 100% completion.

---

# 42. Milestone A — First Drivable Vehicle

Do not work on traffic, damage modelling or special vehicles until this works.

* [ ] Vehicle model loads.
* [ ] Handling data loads.
* [ ] Vehicle falls onto road correctly.
* [ ] Four-wheel suspension works.
* [ ] Vehicle accelerates.
* [ ] Vehicle brakes.
* [ ] Vehicle reverses.
* [ ] Vehicle steers.
* [ ] Handbrake works.
* [ ] Vehicle can collide with world.
* [ ] Player can enter.
* [ ] Player can drive.
* [ ] Player can exit.

**Gate:** Claude can steal a parked car and drive around Liberty City without major physics failures.

---

# 43. Milestone B — Functional GTA Traffic

* [ ] Ambient vehicles spawn.
* [ ] AI drivers exist.
* [ ] Vehicles follow roads.
* [ ] Vehicles collide correctly.
* [ ] Vehicles can be stolen.
* [ ] AI occupants react.
* [ ] Vehicles despawn safely.
* [ ] Police vehicles can pursue player.
* [ ] Vehicle damage works.
* [ ] Vehicles can burn/explode.

**Gate:** Liberty City functions as a GTA-style driving environment.

---

# 44. Milestone C — Mission-Ready Vehicles

* [ ] Full mission vehicle script interface.
* [ ] Mission vehicle ownership.
* [ ] Drivers/passengers.
* [ ] Vehicle objectives.
* [ ] Chasing/fleeing AI.
* [ ] Locked vehicles.
* [ ] Vehicle proof flags.
* [ ] Mission garages.
* [ ] Car bombs.
* [ ] Vehicle destruction detection.
* [ ] Mission cleanup.

**Gate:** vehicle-related script commands no longer require mission-specific hacks.

---

# 45. Milestone D — Main Story Vehicle Complete

* [ ] All normal automobile classes needed by story work.
* [ ] Boats work.
* [ ] Passenger missions work.
* [ ] Escorts work.
* [ ] Chases work.
* [ ] Police pursuit works.
* [ ] Mission garages work.
* [ ] Car bombing works.
* [ ] Vehicle damage/destruction works.
* [ ] Every mandatory GTA III mission can be completed without bypassing vehicle logic.

**Gate:** GTA III can be played from opening to final mission.

---

# 46. Milestone E — Full GTA III Vehicle Support

This is the actual vehicle-system completion target for the project's stated gameplay goal.

* [ ] Taxi activity complete.
* [ ] Paramedic activity complete.
* [ ] Vigilante activity complete.
* [ ] Firefighter activity complete.
* [ ] RC Toyz complete.
* [ ] Off-road challenges complete.
* [ ] Import/export complete.
* [ ] Crusher/crane vehicle functionality complete.
* [ ] Safehouse garage vehicles persist.
* [ ] Pay 'n' Spray works.
* [ ] Special required vehicles work.
* [ ] Rhino functionality sufficient for GTA III.
* [ ] Drive-by shooting works.
* [ ] All GTA III automobile models usable.
* [ ] All GTA III boat models usable.
* [ ] No vehicle requirement prevents optional mission completion.
* [ ] No vehicle requirement prevents 100% game completion.

---

# 47. Explicitly Deferred Until GTA III Is Playable

These should **not** block the milestones above.

* [ ] **[P2]** Pixel-perfect visual vehicle damage.
* [ ] **[P2]** Exact panel deformation.
* [ ] **[P2]** Every detachable component.
* [ ] **[P2]** Exact tyre smoke appearance.
* [ ] **[P2]** Exact suspension oscillation.
* [ ] **[P2]** Exact traffic densities.
* [ ] **[P2]** Exact AI lane-change behaviour.
* [ ] **[P2]** Exact engine pitch/RPM calculations.
* [ ] **[P2]** Exact GTA III collision impulse quirks.
* [ ] **[P2]** Exact wet-surface traction.
* [ ] **[P2]** Exact graphical reflections.
* [ ] **[P2]** Cinematic vehicle camera.
* [ ] **[P2]** Detailed train parity.
* [ ] **[P2]** Detailed Dodo flight parity.
* [ ] **[P2]** Bugs/quirks that have no mission or gameplay compatibility consequence.

---

# Final Vehicle System Definition of Done

The vehicle system should be considered **GTA III playable** when:

* [ ] Claude can steal, enter, drive and exit vehicles normally.
* [ ] GTA III handling data meaningfully determines how each vehicle drives.
* [ ] Four-wheel suspension and traction behave reliably throughout Liberty City.
* [ ] Vehicles interact correctly with pedestrians, traffic and world collision.
* [ ] Traffic exists and can navigate the road network.
* [ ] Police can pursue the player using vehicles.
* [ ] Mission scripts can create, control, inspect and destroy vehicles.
* [ ] Mission NPCs can act as drivers and passengers.
* [ ] Vehicle health, fire, explosions and wrecks function.
* [ ] Mission garages and car bombs function.
* [ ] Boats function sufficiently to complete boat-dependent story missions.
* [ ] Every mandatory story mission can be completed without vehicle-system workarounds.

The vehicle system should be considered **GTA III complete** when, additionally:

* [ ] Vehicle side missions work.
* [ ] RC Toyz missions work.
* [ ] Off-road challenges work.
* [ ] Import/export and other vehicle collection systems work.
* [ ] Required special vehicles work.
* [ ] Garaged vehicles save/load correctly.
* [ ] Every optional GTA III activity relying on a vehicle can be completed.
* [ ] Vehicle functionality no longer prevents legitimate 100% completion.

Only after these conditions are met should substantial development time be spent on vehicle behaviour that is purely parity, cosmetic, architectural generalisation, or support for games/content beyond GTA III.
