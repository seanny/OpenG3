using System;
using System.IO;
using UnityEngine;

namespace GTA3Unity.Vehicles
{
    [Serializable]
    public class VehicleData
    {
        public int ReverseGear;
        public float ReverseSpeedRatio;
        public float ShiftDownFraction;
        public float StopSpeed;
        public float InputDeadZone;
        public float HandbrakeTorque;
        public float LowerGearSpeedMultiplier;
        public float CoastingBrakeFraction;
        public float MinimumCoastingDeceleration;
        public float BrakeForceMultiplier;
        public float WheelGripMultiplier;
    }

    public static class VehicleManager
    {
        public static VehicleData VehicleData { get; private set; }

        public static bool Init(string pathToVehicleJson)
        {
            if(!File.Exists(pathToVehicleJson))
            {
                return false;
            }

            VehicleData = JsonUtility.FromJson<VehicleData>(File.ReadAllText(pathToVehicleJson));
            return true;
        }
    }
}
