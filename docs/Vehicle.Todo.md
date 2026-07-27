# Vehicle To-do

This status is based on the current source. "Completed" means that the code path exists and is wired for a manually placed `Car`; it does not mean that GTA III parity or runtime QA is complete.

## Completed

- **Vehicle foundation**
  - `Vehicle` stores a vehicle identifier and handling data, loads a model through `FileLoader`, requires a `Rigidbody`, and provides the common input/driver API.
  - Vehicle model mesh colliders are disabled so dynamic vehicles can use wheel physics. A convex `chassis_hi` collider is restored for the body when available.
  - A driver can be attached to a vehicle, parented to it, and have their `CharacterController` temporarily disabled to avoid physics impulses.

- **GTA data loading and lookup**
  - `HandlingManager` parses the 32-field `handling.cfg` records, including drive/engine types, handling flags, suspension values, traction, braking, and transmission data.
  - Handling maximum velocity is converted from GTA/re3 units and applies the current cruise-speed/headroom calculation.
  - `FileLoader` reads car IDE definitions and can resolve a car by its handling identifier, then load its model and wheel model.
  - `VehicleManager` has a line-oriented parser for the currently recognised global vehicle tuning values.

- **Car initialisation**
  - `Car` waits for `FileLoader`, validates its identifier, handling record, IDE record, wheel scale, model, and four required wheel frames.
  - The car model is assigned to the vehicle layer, an optional `_hi`/`_vlo` `LODGroup` is created, and the rigidbody is configured from handling mass, centre of mass, dimensions, damping, interpolation, and collision mode.
  - `VehicleBody` and `VehicleWheel` layers are present in the project settings.

- **Wheels and steering**
  - Four `WheelCollider`s are created at `wheel_lf_dummy`, `wheel_rf_dummy`, `wheel_lb_dummy`, and `wheel_rb_dummy`.
  - IDE wheel scale/model data and handling suspension, grip, driven-wheel type, and wheel-layer exclusions are applied.
  - Wheel visual models follow the collider poses, and smoothed steering input is applied to the two front wheels.

- **Basic player driving**
  - The player can find the nearest `Vehicle` with `F`, become its driver, and forward movement and handbrake input while in the driving state.
  - `CarAcceleration` implements a baseline for throttle, direction-change braking, forward/reverse selection, automatic gear changes, drive-type selection, engine acceleration, service brakes, and handbrake torque.

- **Door/model assembly**
  - Known bonnet, boot, and door dummy frames are detected.
  - `CarDoorAssembler` creates `VehicleDoor` components, applies the van/bus and bonnet/boot flag setup, places intact meshes, and hides the damaged mesh variants.

- **Manual test setup**
  - `SampleScene` and `TestScene` contain manually placed `LANDSTAL` `Car` objects with rigidbodies and `CarAcceleration` components.

## Work in progress

- **The global settings file and parser are out of sync.** `vehicle_settings.dat` contains `EnableDiagnostics`, `DiagnosticIntervalFrames`, `LogWheelDiagnostics`, and `LogEveryInputSample`, but `VehicleData` does not define or load them. Unknown setting names are silently accepted. `ReverseGear` is read by the driving code but is not present in the current settings file, so it remains the default value.

- **Diagnostics need finishing.** `CarAcceleration.m_DiagnosticIntervalFrames` is never used; the periodic threshold is hard-coded, and the force summary and wheel diagnostics can log every physics frame when diagnostics are enabled. The build also reports obsolete `GetInstanceID` warnings.

- **Runtime car integration is incomplete.** A `Car` can be placed in a scene and initialised, but the world loader only spawns `IdeObj` instances. It does not turn car IDE records or IPL instances into `Car` objects, so cars are not populated into the game world automatically.

- **Driving needs runtime validation and handling tuning.** The current propulsion is a Unity `WheelCollider` approximation. It has no vehicle-specific test coverage, and the wheel-frame direction/sign, grip, suspension, acceleration, gear thresholds, and braking values still need in-game validation against GTA III behaviour.

- **Driver lifecycle is only partially implemented.** Entry is present, but there is no player exit path that calls `ClearDriver`, restores the player to an appropriate exit position, or returns the player to the on-foot state. `SeatOffsetDistance` is parsed but not used; the driver is teleported to the vehicle root rather than a defined seat position.

- **Door runtime behaviour is not wired.** `VehicleDoor.OnUpdate` and `VehicleDoor.Open` have no callers. Door angles are not applied to the door transform, there is no player door-open/close interaction, and the damaged meshes are never swapped in. The current code assembles the parts but does not provide visible working door animation.

- **Several parsed values have no runtime consumer yet.** This includes `PercentSubmerged`, `CollisionDamageMultiplier`, `SeatOffsetDistance`, `FrontLights`, `RearLights`, the unused handling flags (such as `NoDoors`, `HasNoRoof`, and `DoubleExhaust`), and car IDE metadata such as frequency, level, and the IDE LOD model.

## Not started

- Vehicle types other than `Car` (`boat`, `bike`, `train`, `plane`, and `helicopter`). `Car` is currently the only `Vehicle` implementation.
- Vehicle population, parked cars, ambient traffic, traffic AI/path following, NPC drivers, police/mission vehicle control, and vehicle streaming/cleanup.
- Passenger seats, passenger entry, NPC occupants, and seat/door selection.
- Vehicle health, collision damage, deformation, fire/explosion states, destruction, repair, and the full damaged-model system.
- Headlights, brake/reverse lights, indicators, sirens, horns, exhaust effects, and other vehicle visual effects. The model converter recognises some related dummy names, but no runtime system drives them.
- Engine, transmission, tyre, collision, and other vehicle audio.
- Water buoyancy, submergence/floating behaviour, and vehicle recovery from water or rollovers.
- GTA III's complete vehicle physics/handling parity, including the remaining handling flags and effects that are not represented by the current baseline `WheelCollider` implementation.
