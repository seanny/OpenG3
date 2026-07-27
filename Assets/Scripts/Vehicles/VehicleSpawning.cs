using System.Collections.Generic;
using System.Globalization;
using GTA3Unity.Dat;
using RenderWareIo.Structs.Ide;
using UnityEngine;

namespace GTA3Unity.Vehicles
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
            public Vehicle Vehicle;
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
                definition.Vehicle = new Car();
                definition.Vehicle.SetVehicleIdentifier(ideCar.HandlingId);
                definition.Vehicle.ModelIndex = ideCar.Id;
                Debug.Log($"Added car '{definition.VehicleId}': {definition.HandlingId}, {definition.GameName}, {definition.VehicleClass}, {definition.Frequency}");
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

            Vehicle vehicle = validDefinitions[randIndex].Vehicle;
            if (validDefinitions[randIndex].Vehicle is Car)
            {
                Car car = gameObject.AddComponent<Car>();
                car.SetVehicleIdentifier(validDefinitions[randIndex].HandlingId);
                car.SetModel(vehicle.ModelIndex);
                //car.SetHandlingData(vehicle.VehicleIdentifier);
                car.transform.SetPositionAndRotation(position, (Quaternion)rotation);
                return car;
            }
            return null;
        }
    }
}
