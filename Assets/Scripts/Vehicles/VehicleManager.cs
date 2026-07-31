using System;
using System.Globalization;
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
        public float BrakeForceMultiplier;
        public float WheelGripMultiplier;
        public float GameSpeedToMetersPerSecond;
        public float VehicleShaderBurntMax;
        public float VehicleBlowUpUpwardForce;
    }

    public static class VehicleManager
    {
        public static VehicleData VehicleData { get; private set; }

        public static bool Init(string pathToVehicleSettingsDat)
        {
            if(!File.Exists(pathToVehicleSettingsDat))
            {
                return false;
            }

            VehicleData vehicleData = new();

            string[] lines = File.ReadAllLines(pathToVehicleSettingsDat);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                int commentIndex = line.IndexOf(';');
                if (commentIndex >= 0)
                {
                    line = line.Substring(0, commentIndex);
                }

                line = line.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                string[] settings = line.Split(
                    new[] { ' ', '\t' },
                    StringSplitOptions.RemoveEmptyEntries);
                if (settings.Length != 2)
                {
                    return false;
                }

                string settingName = settings[0];
                string value = settings[1];
                bool parsed = settingName switch
                {
                    "ReverseGear" => int.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out vehicleData.ReverseGear),
                    "ReverseSpeedRatio" => float.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out vehicleData.ReverseSpeedRatio),
                    "ShiftDownFraction" => float.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out vehicleData.ShiftDownFraction),
                    "StopSpeed" => float.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out vehicleData.StopSpeed),
                    "InputDeadZone" => float.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out vehicleData.InputDeadZone),
                    "HandbrakeTorque" => float.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out vehicleData.HandbrakeTorque),
                    "LowerGearSpeedMultiplier" => float.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out vehicleData.LowerGearSpeedMultiplier),
                    "BrakeForceMultiplier" => float.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out vehicleData.BrakeForceMultiplier),
                    "WheelGripMultiplier" => float.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out vehicleData.WheelGripMultiplier),
                    "GameSpeedToMetersPerSecond" => float.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out vehicleData.GameSpeedToMetersPerSecond),
                    "VehicleShaderBurntMax" => float.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out vehicleData.VehicleShaderBurntMax),
                    "VehicleBlowUpUpwardForce" => float.TryParse(
                        value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out vehicleData.VehicleBlowUpUpwardForce),
                    _ => true
                };

                if (!parsed)
                {
                    return false;
                }
            }

            VehicleData = vehicleData;
            return true;
        }
    }
}
