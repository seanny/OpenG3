# Vehicle API

Last Updated: 16 August 2026


## Vehicle
`Vehicle` is the base class that contains common functionality shared between `Car` and other types that will derive from `Vehicle` (not yet implemented)

`Vehicle` is derived from `GtaObject`.

### Vehicle Identifier
The vehicle identifier corresponds to the HandlingId of the vehicle.

### Driver
Driver is a PedObject that is the driver of the vehicle.

### HandlingData
HandingData represents the handling data of the vehicle and is populated through handing.cfg

### ControlState
`ControlState` identifies who currently controls the vehicle:
- None: No controller is assigned
- PlayerControlled: Controlled by the player
- AiControlled: Controlled by vehicle AI
- MissionControlled: Controlled by mission logic

Assigning a controller transitions the vehicle lifecycle to `Active`.

### LifecycleState
`LifecycleState` identifies the vehicle's world state:
- Active: The vehicle is currently controlled or otherwise active
- Parked: The vehicle has no controller and is parked
- Abandoned: The vehicle has no controller and has been left in the world
- Wrecked: The vehicle is destroyed and cannot be controlled

`Wrecked` is terminal until a future repair/reset system is implemented. State transitions update the existing vehicle instance and do not recreate its GameObject, model, driver, or runtime identifier.
