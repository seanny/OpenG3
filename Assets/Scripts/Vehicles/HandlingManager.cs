using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// https://projectcerbera.com/gta/3/tutorials/handling
// https://projectcerbera.com/gta/3-vc/tutorials/detailed-acceleration
// "heavy vehicle is given more power by the game than a light vehicle with the same Engine Acceleration rate. This is why some heavy trucks can reach a higher top speeds than the light sports cars."
namespace OpenG3.Vehicles
{
    [Flags]
    public enum EHandlingFlags
    {
        Boost1G = 1,
        Boost2G = 2,
        RevBonnet = 4,
        HangingBoot = 8,
        NoDoors = 0x10,
        IsVan = 0x20,
        IsBus = 0x40,
        IsLow = 0x80,
        DoubleExhaust = 0x100,
        TailGateBoot = 0x200,
        NoSwingBoot = 0x400,
        NonPlayerStabiliser = 0x800,
        NeutralHandling = 0x1000,
        HasNoRoof = 0x2000,
        IsBig = 0x4000,
        HalogenLights = 0x8000
    }

    public class TransmissionData
    {
        public int NumberOfGears; // Doesn't appear to do much outside of audio
        public float MaxVelocity;
        public float EngineAcceleration;
        public EDriveType DriveType;
        public EEngineType EngineType;
    }

    public class HandlingData
    {
        public string VehicleIdentifier; // Relates this data with default.ide and other files
        public float Mass; // Mass of the vehicle in kilograms
        public Vector3 Dimensions; // Width, Length & Height of the vehicle in metres, used for aerodynamic and motion effects
        public Vector3 CentreOfMass; // Distance from the centre of the car in metres
        public int PercentSubmerged; // Percentage of the vehicle height required to be submerged for the car to float
        public float TractionMultiplier; // Cornering grip of the vehicle as a multiplier of the tyre surface friction
        public float TractionLoss; // Accelerating/braking grip of the vehicle as a multiplier of the tyre surface friction
        public float TractionBias; // Ratio of front axle grip to rear axle grip; higher value shifts grip forwards
        public TransmissionData TransmissionData = new();
        public float BrakeDeceleration;
        public float BrakeBias;
        public float ABS; // Unused in GTA3, keeping for compatibility sake though
        public float SteeringLock;
        public float SuspensionForceLevel;
        public float SuspensionDampingLevel;
        public float SeatOffsetDistance;
        public float CollisionDamageMultiplier;
        public int MonetaryValue; // Apparently determines how much the crusher give the player
        public float SuspensionUpperLimit;
        public float SuspensionLowerLimit;
        public float SuspensionBias;
        public EHandlingFlags Flags; // Hex-encoded handling flags
        public int FrontLights; // 0 = long, 1 = small, 2 = big, 3 = tall
        public int RearLights; // 0 = long, 1 = small, 2 = big, 3 = tall

        public override string ToString()
        {
            return $"{VehicleIdentifier}, Mass: {Mass}kg, Dimensions: {Dimensions}, Centre of mass: {CentreOfMass}, " +
                $"Percent submerged: {PercentSubmerged}%, Traction multiplier: {TractionMultiplier}, " +
                $"Traction loss: {TractionLoss}, Traction bias: {TractionBias}, Gears: {TransmissionData.NumberOfGears}, " +
                $"Max velocity: {TransmissionData.MaxVelocity}, Engine acceleration: {TransmissionData.EngineAcceleration}, " +
                $"Drive type: {TransmissionData.DriveType}, Engine type: {TransmissionData.EngineType}, " +
                $"Brake deceleration: {BrakeDeceleration}, Brake bias: {BrakeBias}, ABS: {ABS}, " +
                $"Steering lock: {SteeringLock}, Suspension force: {SuspensionForceLevel}, " +
                $"Suspension damping: {SuspensionDampingLevel}, Seat offset: {SeatOffsetDistance}, " +
                $"Collision damage multiplier: {CollisionDamageMultiplier}, Monetary value: {MonetaryValue}, " +
                $"Suspension upper limit: {SuspensionUpperLimit}, Suspension lower limit: {SuspensionLowerLimit}, " +
                $"Suspension bias: {SuspensionBias}, Flags: 0x{Flags:X}, Front lights: {FrontLights}, Rear lights: {RearLights}";
        }

    }

    public static class HandlingManager
    {
        private const int HandlingFieldCount = 32;
        private const float GameFramesPerSecond = 50.0f;
        private const float KilometersPerHourToMetersPerSecond = 1000.0f / (60.0f * 60.0f);
        private static readonly Dictionary<string, HandlingData> s_HandlingData =
            new(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyDictionary<string, HandlingData> Data => s_HandlingData;

        public static bool TryGetHandlingData(
            string vehicleIdentifier,
            out HandlingData handlingData)
        {
            return s_HandlingData.TryGetValue(vehicleIdentifier, out handlingData);
        }

        public static void LoadHandlingData(string handlingCfg)
        {
            if (!File.Exists(handlingCfg))
            {
                Debug.LogError($"Failed to load \"{handlingCfg}\": File does not exist!");
                return;
            }

            s_HandlingData.Clear();

            string[] lines = File.ReadAllLines(handlingCfg);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                int commentIndex = line.IndexOf(';');
                if (commentIndex >= 0)
                {
                    line = line.Substring(0, commentIndex);
                }

                line = line.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != HandlingFieldCount)
                {
                    Debug.LogWarning($"Skipping handling.cfg line {lineIndex + 1}: " +$"expected {HandlingFieldCount} fields, got {parts.Length}.");
                    continue;
                }

                try
                {
                    float rawMaxVelocity = ParseFloat(parts[13]);
                    float rawEngineAcceleration = ParseFloat(parts[14]);
                    HandlingData handlingData = new()
                    {
                        VehicleIdentifier = parts[0],
                        Mass = ParseFloat(parts[1]),
                        Dimensions = new Vector3(ParseFloat(parts[2]), ParseFloat(parts[3]), ParseFloat(parts[4])),
                        CentreOfMass = new Vector3(ParseFloat(parts[5]), ParseFloat(parts[6]), ParseFloat(parts[7])),
                        PercentSubmerged = ParseInt(parts[8]),
                        TractionMultiplier = ParseFloat(parts[9]),
                        TractionLoss = ParseFloat(parts[10]),
                        TractionBias = ParseFloat(parts[11]),
                        TransmissionData = new TransmissionData
                        {
                            NumberOfGears = ParseInt(parts[12]),
                            MaxVelocity = rawMaxVelocity,
                            EngineAcceleration = rawEngineAcceleration,
                            DriveType = ParseDriveType(parts[15]),
                            EngineType = ParseEngineType(parts[16])
                        },
                        BrakeDeceleration = ParseFloat(parts[17]),
                        BrakeBias = ParseFloat(parts[18]),
                        ABS = ParseFloat(parts[19]),
                        SteeringLock = ParseFloat(parts[20]),
                        SuspensionForceLevel = ParseFloat(parts[21]),
                        SuspensionDampingLevel = ParseFloat(parts[22]),
                        SeatOffsetDistance = ParseFloat(parts[23]),
                        CollisionDamageMultiplier = ParseFloat(parts[24]),
                        MonetaryValue = ParseInt(parts[25]),
                        SuspensionUpperLimit = ParseFloat(parts[26]),
                        SuspensionLowerLimit = ParseFloat(parts[27]),
                        SuspensionBias = ParseFloat(parts[28]),
                        Flags = ParseFlags(parts[29]),
                        FrontLights = ParseInt(parts[30]),
                        RearLights = ParseInt(parts[31])
                    };

                    handlingData.TransmissionData.MaxVelocity =
                        ConvertMaxVelocityToGameUnits(
                            handlingData,
                            rawMaxVelocity,
                            rawEngineAcceleration);
                    s_HandlingData[handlingData.VehicleIdentifier] = handlingData;
                }
                catch (Exception exception) when (exception is FormatException || exception is OverflowException)
                {
                    Debug.LogWarning($"Skipping handling.cfg line {lineIndex + 1}: {exception.Message}");
                }
            }
        }

        private static float ConvertMaxVelocityToGameUnits(
            HandlingData handlingData,
            float rawMaxVelocity,
            float rawEngineAcceleration)
        {
            // re3 stores vehicle velocity in metres per 50 Hz game frame.
            float maxVelocity = rawMaxVelocity *
                KilometersPerHourToMetersPerSecond /
                GameFramesPerSecond;

            // HandlingMgr converts acceleration to game-frame units before
            // using it to derive the drag-limited cruise speed.
            float engineAcceleration = rawEngineAcceleration *
                0.4f /
                (GameFramesPerSecond * GameFramesPerSecond);
            float velocity = maxVelocity;
            float a = 0.0f;
            float b = 100.0f;
            float mass = Mathf.Max(1.0f, handlingData.Mass);
            float aerodynamicArea = Mathf.Abs(
                handlingData.Dimensions.x * handlingData.Dimensions.z);

            while (a < b && velocity > 0.0f)
            {
                velocity -= 0.01f;
                a = engineAcceleration / 6.0f;

                float dragAcceleration = 0.5f *
                    velocity *
                    velocity *
                    aerodynamicArea /
                    mass;
                b = -velocity *
                    (1.0f / (dragAcceleration + 1.0f) - 1.0f);
            }

            // The RC Bandit keeps the handling.cfg maximum velocity. Other
            // vehicles receive re3's 1.2x headroom over the cruise velocity.
            if (!string.Equals(
                    handlingData.VehicleIdentifier,
                    "RCBANDIT",
                    StringComparison.OrdinalIgnoreCase))
            {
                maxVelocity = velocity * 1.2f;
            }

            return Mathf.Max(0.0f, maxVelocity);
        }

        private static float ParseFloat(string value)
        {
            return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static int ParseInt(string value)
        {
            return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static EHandlingFlags ParseFlags(string value)
        {
            return (EHandlingFlags)uint.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private static EDriveType ParseDriveType(string value)
        {
            return value.ToUpperInvariant() switch
            {
                "F" => EDriveType.FrontWheel,
                "R" => EDriveType.BackWheel,
                "4" => EDriveType.BothWheel,
                _ => throw new FormatException($"Unknown drive type '{value}'.")
            };
        }

        private static EEngineType ParseEngineType(string value)
        {
            return value.ToUpperInvariant() switch
            {
                "P" => EEngineType.Petrol,
                "D" => EEngineType.Diesel,
                "E" => EEngineType.Electric,
                _ => throw new FormatException($"Unknown engine type '{value}'.")
            };
        }
    }
}
