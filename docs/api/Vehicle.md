# Vehicle API

Last Updated: 14 August 2026


## Vehicle
`Vehicle` is the base class that contains common functionality shared between `Car` and other types that will derive from `Vehicle` (not yet implemented)

`Vehicle` is derived from `GtaObject`.

### Vehicle Identifier
The vehicle identifier corresponds to the HandlingId of the vehicle.

### Driver
Driver is a PedObject that is the driver of the vehicle.

### HandlingData
HandingData represents the handling data of the vehicle and is populated through handing.cfg

### VehicleState
Vehicle state represents the present state of the vehicle which can be:
- Player: Controlled by the player
- AiSimple: Controlled by the AI not using the WheelColliders
- AiPhysics: Controlled by the AI using the WheelColliders
- Abandoned: Vehicle has no AI or player occupant
- Wrecked: Vehicle can no longer be driven and renders the wrecked variant of its model