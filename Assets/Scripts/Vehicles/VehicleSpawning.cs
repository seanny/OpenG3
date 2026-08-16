using System;
using System.Collections.Generic;
using GTA3Unity.Core;
using UnityEngine;

namespace OpenG3.Vehicles
{
    public enum EVehicleClass
    {
        PoorFamily = 0,
        RichFamily,
        Executive,
        Worker,
        Special,
        Big,
        Taxi,
        Ignore, // Used for gang vehicles and emergency vehicles
        TotalVehicleClasses
    };

    public static class VehicleSpawning
    {
        public class VehicleDefinition
        {
            public int VehicleId;
            public string HandlingId;
            public string GameName;
            public EVehicleClass VehicleClass;
            public int Frequency;
            public int ModelIndex;
        }

        public static List<VehicleDefinition> Vehicles { get; private set; } = new();
        public static Dictionary<int, Vehicle> SpawnedVehicles { get; private set; } = new();
        private static int NextVehicleId = 0;

        private static int AllocateVehicleId()
        {
            return NextVehicleId++;
        }

        internal static void RegisterSpawnedVehicle(Vehicle vehicle)
        {
            if(vehicle == null)
            {
                return;
            }

            int runtimeId = AllocateVehicleId();
            if(vehicle.SetRuntimeId(runtimeId))
            {
                SpawnedVehicles.Add(runtimeId, vehicle);
            }
        }

        public static void AddVehicle(RenderWareIo.Structs.Ide.Car ideCar)
        {
            VehicleDefinition definition = new()
            {
                VehicleId = ideCar.Id,
                HandlingId = ideCar.HandlingId,
                GameName = ideCar.GameName,
                VehicleClass = System.Enum.Parse<EVehicleClass>(ideCar.Class, true),
                Frequency = ideCar.Frequency
            };
            if(string.Equals(ideCar.Type, "car", System.StringComparison.OrdinalIgnoreCase))
            {
                definition.ModelIndex = ideCar.Id;
            }
            Vehicles.Add(definition);
        }

        /// <summary>
        /// Spawn a random vehicle within the specified vehicle class
        /// </summary>
        /// <param name="vehicleClass">Vehicle class to spawn</param>
        /// <param name="position">Position to spawn at</param>
        /// <returns></returns>
        public static Vehicle SpawnRandomVehicle(EVehicleClass vehicleClass, Vector3 position, Quaternion? rotation = null)
        {
            if (rotation == null)
            {
                rotation = Quaternion.identity;
            }

            List<VehicleDefinition> validDefinitions = new();
            foreach (var veh in Vehicles)
            {
                if (veh.VehicleClass != vehicleClass)
                {
                    continue;
                }
                validDefinitions.Add(veh);
            }

            if (validDefinitions.Count < 1)
            {
                Debug.LogError($"SpawnRandomVehicle: No vehicle of type {vehicleClass} exists.");
                return null;
            }


            int randIndex = 0;
            if (validDefinitions.Count > 1)
            {
                randIndex = UnityEngine.Random.Range(0, validDefinitions.Count);
            }

            GameObject gameObject = new GameObject();
            gameObject.name = $"{validDefinitions[randIndex].GameName}_{gameObject.GetEntityId()}";
            // Set the pose before adding the component. Adding Car also adds its
            // Rigidbody and runs Awake, so the object must not be initialized at
            // the default origin first.
            gameObject.transform.SetPositionAndRotation(position, (Quaternion)rotation);

            if (validDefinitions[randIndex].ModelIndex > 0)
            {
                Car car = gameObject.AddComponent<Car>();
                car.SetVehicleIdentifier(validDefinitions[randIndex].HandlingId);
                car.SetModel(validDefinitions[randIndex].ModelIndex);
                RegisterSpawnedVehicle(car);
                return car;
            }
            return null;
        }

        /// <summary>
        /// Removes all non-mission distant (distance from player >100m) or wrecked (destroyed > 60 seconds).
        /// </summary>
        /// <param name="player"></param>
        /// <param name="deltaTime"></param>
        public static void RemoveNonMissionDistantOrWreckedSpawnedVehicles(PlayerController player, float deltaTime)
        {
            List<int> vehiclesToRemove = new();
            foreach(var vehicle in SpawnedVehicles)
            {
                if(vehicle.Value == null)
                {
                    continue;
                }
                Vehicle instance = vehicle.Value;
                if(instance.Driver != null && instance.Driver.IsMissionPed == true) // Do a similar check on passengers once they're implemented
                {
                    // Prevent vehicles being driven by mission peds from being despawned
                    continue;
                }

                if(instance.VehicleType == EVehicleType.Mission)
                {
                    // Prevent mission vehicles from being despawned
                    continue;
                }

                float distance = Vector3.Distance(player.transform.position, instance.transform.position);
                if(distance > 50.0f || instance.DeathTime >= 60f) // Despawn vehicles if we are more than 100m away or its been destroyed for 60+ seconds
                {
                    vehiclesToRemove.Add(vehicle.Key);
                }
            }
            if(vehiclesToRemove.Count > 0)
            {
                foreach(var vehicleToRemove in vehiclesToRemove)
                {
                    GameObject.Destroy(SpawnedVehicles[vehicleToRemove].gameObject);
                    SpawnedVehicles.Remove(vehicleToRemove);
                }
                Debug.Log($"Vehicles to remove: {vehiclesToRemove.Count}");
            }
        }

        /// <summary>
        /// Convert all mission vehicles into normal vehicles
        /// </summary>
        public static void ReleaseMissionVehicles()
        {
            foreach(var vehicle in SpawnedVehicles)
            {
                if(vehicle.Value == null)
                {
                    continue;
                }
                Vehicle instance = vehicle.Value;

                if(instance.VehicleType == EVehicleType.Mission)
                {
                    instance.SetVehicleType(EVehicleType.Normal);
                }
            }
        }
    }
}
