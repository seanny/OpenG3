using System.Collections.Generic;
using System.Globalization;
using GTA3Unity.Dat;
using RenderWareIo.Structs.Ide;
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
                randIndex = Random.Range(0, validDefinitions.Count);
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
                //car.SetHandlingData(vehicle.VehicleIdentifier);
                return car;
            }
            return null;
        }
    }
}
