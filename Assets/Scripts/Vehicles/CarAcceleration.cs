using System.Diagnostics;
using GTA3Unity.Debugging;
using StarterAssets;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GTA3Unity.Vehicles
{
    [RequireComponent(typeof(Rigidbody))]
    public class CarAcceleration : MonoBehaviour
    {
        private const float ShiftUpFraction = 2.0f / 3.0f;

        [Header("Acceleration State")]
        [SerializeField]
        private Vector3 m_MovementSpeed;

        [SerializeField]
        private float m_fGasPedal;

        [SerializeField]
        private float m_fBrakePedal;

        [SerializeField]
        private int m_CurrentGear = 1;

        [SerializeField]
        private bool m_UseAngularVelocity;

        [Header("Diagnostics")]
        [SerializeField]
        private bool m_EnableDiagnostics = true;

        [SerializeField]
        [Min(1)]
        [Tooltip("Number of FixedUpdate calls between full motion and wheel snapshots.")]
        private int m_DiagnosticIntervalFrames = 10;

        [SerializeField]
        [Tooltip("Include RPM, slip, contact-force and alignment data for every wheel.")]
        private bool m_LogWheelDiagnostics = true;

        [SerializeField]
        [Tooltip("Log every OnInput call instead of only input changes.")]
        private bool m_LogEveryInputSample;

        private Rigidbody m_RigidBody;
        private WheelCollider[] m_Wheels = new WheelCollider[0];
        private HandlingData m_HandlingData;
        private float m_WheelRadius;
        private float m_RequestedPedal;
        private bool m_HandBrake;
        private int m_DrivenWheelCount;
        private bool m_IsInitialized;

        private int m_FixedUpdateCount;
        private float m_LastLoggedRequestedPedal = float.NaN;
        private bool m_LastLoggedHandBrake;
        private int m_LastGroundedWheelCount = -1;
        private bool m_WasDirectionChangeBraking;
        private bool m_HasLoggedAngularVelocityWarning;
        private bool m_LastUseAngularVelocity;
        private string m_LastDriveState = string.Empty;

        private bool IsPeriodicDiagnosticFrame =>
            m_EnableDiagnostics && m_FixedUpdateCount > 10;

        private float m_CalculatePedals;

        private void Awake()
        {
            m_RigidBody = GetComponent<Rigidbody>();

            if (m_EnableDiagnostics)
            {
                Debug.Log(
                    $"[CarAcceleration] Awake: componentId={GetInstanceID()}, " +
                    $"rigidbodyId={(m_RigidBody != null ? m_RigidBody.GetInstanceID() : 0)}, " +
                    $"object='{name}', active={gameObject.activeInHierarchy}, enabled={enabled}, " +
                    $"position={transform.position.ToString("R")}, " +
                    $"rotation={transform.rotation.eulerAngles.ToString("R")}",
                    this);
            }
        }

        internal void Initialize(
            WheelCollider[] wheels,
            float wheelRadius,
            HandlingData handlingData)
        {
            if (wheels == null || wheelRadius <= 0.0f || handlingData == null)
            {
                m_IsInitialized = false;

                Debug.LogError(
                    $"[CarAcceleration] Initialize failed: wheelsNull={wheels == null}, " +
                    $"wheelCount={(wheels != null ? wheels.Length : 0)}, " +
                    $"wheelRadius={wheelRadius:R}, handlingDataNull={handlingData == null}, " +
                    $"rigidbodyNull={m_RigidBody == null}",
                    this);
                return;
            }

            m_Wheels = wheels;
            m_WheelRadius = wheelRadius;
            m_HandlingData = handlingData;
            m_DrivenWheelCount = 0;

            for (int i = 0; i < m_Wheels.Length; i++)
            {
                if (m_Wheels[i] != null && IsDrivenWheel(i))
                {
                    m_DrivenWheelCount++;
                }
            }

            m_CurrentGear = 1;
            m_fGasPedal = 0.0f;
            m_fBrakePedal = 0.0f;
            m_RequestedPedal = 0.0f;
            m_HandBrake = false;
            m_FixedUpdateCount = 0;
            m_LastGroundedWheelCount = -1;
            m_LastDriveState = string.Empty;
            m_IsInitialized = m_DrivenWheelCount > 0;

            if (!m_IsInitialized)
            {
                Debug.LogError(
                    "[CarAcceleration] Initialize found no driven wheels. " +
                    "Check wheel ordering and TransmissionData.DriveType.",
                    this);
            }
        }

        public void OnInput(StarterAssetsInputs input)
        {
            if (input == null)
            {
                Debug.LogWarning(
                    "[CarAcceleration] OnInput received a null StarterAssetsInputs reference.",
                    this);
                return;
            }

            float rawPedal = input.move.y;
            float requestedPedal = Mathf.Clamp(rawPedal, -1.0f, 1.0f);
            bool handBrake = input.handBrake;

            bool pedalChanged =
                float.IsNaN(m_LastLoggedRequestedPedal) ||
                !Mathf.Approximately(requestedPedal, m_LastLoggedRequestedPedal);
            bool handBrakeChanged = handBrake != m_LastLoggedHandBrake;

            if (pedalChanged || handBrakeChanged)
            {
                m_LastLoggedRequestedPedal = requestedPedal;
                m_LastLoggedHandBrake = handBrake;
                PerformanceMonitor.SetValue("m_LastLoggedRequestedPedal", m_LastLoggedRequestedPedal);
                PerformanceMonitor.SetValue("m_LastLoggedHandBrake", m_LastLoggedHandBrake);
            }

            // Input is sampled here and consumed from FixedUpdate. The input
            // object itself is never changed while calculating braking.
            m_RequestedPedal = requestedPedal;
            m_HandBrake = handBrake;
        }

        private void FixedUpdate()
        {
            if (!m_IsInitialized || m_RigidBody == null)
            {
                return;
            }

            m_FixedUpdateCount++;

            if (m_UseAngularVelocity != m_LastUseAngularVelocity)
            {
                Debug.LogWarning(
                    $"[CarAcceleration] Velocity source changed: " +
                    $"useAngularVelocity={m_UseAngularVelocity}. " +
                    "Angular velocity is measured in radians per second and should not normally " +
                    "be used as vehicle linear speed.",
                    this);

                m_LastUseAngularVelocity = m_UseAngularVelocity;
            }

            if (m_UseAngularVelocity && !m_HasLoggedAngularVelocityWarning)
            {
                Debug.LogWarning(
                    "[CarAcceleration] m_UseAngularVelocity is enabled. " +
                    "Forward speed, gear changes and acceleration limits are therefore based on " +
                    "Rigidbody.angularVelocity instead of Rigidbody.linearVelocity.",
                    this);

                m_HasLoggedAngularVelocityWarning = true;
            }

            Vector3 linearVelocity = m_RigidBody.linearVelocity;
            Vector3 angularVelocity = m_RigidBody.angularVelocity;
            m_MovementSpeed = m_UseAngularVelocity ? angularVelocity : linearVelocity;

            Vector3 vehicleForward = GetVehicleForward();
            float forwardSpeed = Vector3.Dot(m_MovementSpeed, vehicleForward);
            float linearForwardSpeed = Vector3.Dot(linearVelocity, vehicleForward);

            CalculatePedals(forwardSpeed);
            UpdateGear(forwardSpeed);

            float driveAcceleration = CalculateDriveAcceleration(forwardSpeed);
            ApplyWheelForces(vehicleForward, forwardSpeed, driveAcceleration);
        }

        private void CalculatePedals(float forwardSpeed)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            m_fGasPedal = m_RequestedPedal;
            m_fBrakePedal = 0.0f;
            float absoluteForwardSpeed = Mathf.Abs(forwardSpeed);

            bool directionChangeBraking =
                absoluteForwardSpeed > VehicleManager.VehicleData.StopSpeed &&
                forwardSpeed * m_RequestedPedal < 0.0f;

            // GTA brakes before changing direction. Once almost stationary,
            // the same input is allowed to select reverse or first gear.
            if (directionChangeBraking)
            {
                m_fGasPedal = 0.0f;
                m_fBrakePedal = Mathf.Abs(m_RequestedPedal);
            }

            m_WasDirectionChangeBraking = directionChangeBraking;
            stopwatch.Stop();
            PerformanceMonitor.SetValue("CalculatePedals", stopwatch.Elapsed.TotalMilliseconds);
        }

        private void UpdateGear(float forwardSpeed)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            int gearCount = GetGearCount();
            int gearBeforeUpdate = m_CurrentGear;

            if (Mathf.Abs(forwardSpeed) <= VehicleManager.VehicleData.StopSpeed)
            {
                if (m_fGasPedal > VehicleManager.VehicleData.InputDeadZone)
                {
                    m_CurrentGear = 1;
                }
                else if (m_fGasPedal < -VehicleManager.VehicleData.InputDeadZone)
                {
                    m_CurrentGear = VehicleManager.VehicleData.ReverseGear;
                }

                LogGearState(forwardSpeed, gearBeforeUpdate, gearCount);
                stopwatch.Stop();
                PerformanceMonitor.SetValue("UpdateGear", stopwatch.Elapsed.TotalMilliseconds);
                return;
            }

            if (m_CurrentGear == VehicleManager.VehicleData.ReverseGear)
            {
                LogGearState(forwardSpeed, gearBeforeUpdate, gearCount);
                stopwatch.Stop();
                PerformanceMonitor.SetValue("UpdateGear", stopwatch.Elapsed.TotalMilliseconds);
                return;
            }

            if (m_fGasPedal < -VehicleManager.VehicleData.InputDeadZone)
            {
                LogGearState(forwardSpeed, gearBeforeUpdate, gearCount);
                stopwatch.Stop();
                PerformanceMonitor.SetValue("UpdateGear", stopwatch.Elapsed.TotalMilliseconds);
                return;
            }

            if (m_CurrentGear < gearCount &&
                forwardSpeed >= GetShiftUpSpeed(m_CurrentGear))
            {
                m_CurrentGear++;
            }
            else if (m_CurrentGear > 1 &&
                forwardSpeed <= GetShiftDownSpeed(m_CurrentGear))
            {
                m_CurrentGear--;
            }

            LogGearState(forwardSpeed, gearBeforeUpdate, gearCount);
            stopwatch.Stop();
            PerformanceMonitor.SetValue("UpdateGear", stopwatch.Elapsed.TotalMilliseconds);
        }

        private float CalculateDriveAcceleration(float forwardSpeed)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            float gearVelocity = GetGearTargetVelocity(m_CurrentGear);
            float speedMultiplier = GetGearSpeedMultiplier(m_CurrentGear);
            float targetVelocity = gearVelocity * speedMultiplier;
            float driveDirection = m_CurrentGear == VehicleManager.VehicleData.ReverseGear ? -1.0f : 1.0f;
            float speedError = driveDirection * (targetVelocity - forwardSpeed);

            if (Mathf.Abs(m_fGasPedal) <= VehicleManager.VehicleData.InputDeadZone)
            {
                stopwatch.Stop();
                PerformanceMonitor.SetValue("CalculateDriveAcceleration", stopwatch.Elapsed.TotalMilliseconds);
                return 0.0f;
            }

            if (m_fBrakePedal > 0.0f)
            {
                stopwatch.Stop();
                PerformanceMonitor.SetValue("CalculateDriveAcceleration", stopwatch.Elapsed.TotalMilliseconds);
                return 0.0f;
            }

            if (m_HandBrake)
            {
                stopwatch.Stop();
                PerformanceMonitor.SetValue("CalculateDriveAcceleration", stopwatch.Elapsed.TotalMilliseconds);
                return 0.0f;
            }

            if (m_DrivenWheelCount == 0)
            {
                stopwatch.Stop();
                PerformanceMonitor.SetValue("CalculateDriveAcceleration", stopwatch.Elapsed.TotalMilliseconds);
                return 0.0f;
            }

            float maxVelocity = GetMaxVelocityMetersPerSecond();
            if (forwardSpeed > maxVelocity)
            {
                stopwatch.Stop();
                PerformanceMonitor.SetValue("CalculateDriveAcceleration", stopwatch.Elapsed.TotalMilliseconds);
                return 0.0f;
            }

            if (speedError <= 0.0f)
            {
                stopwatch.Stop();
                PerformanceMonitor.SetValue("CalculateDriveAcceleration", stopwatch.Elapsed.TotalMilliseconds);
                return 0.0f;
            }

            float engineAcceleration = Mathf.Max(0.0f, m_HandlingData.TransmissionData.EngineAcceleration);
            float targetMagnitude = Mathf.Max(Mathf.Abs(targetVelocity), 0.01f);
            float driveAcceleration = driveDirection * Mathf.Abs(m_fGasPedal) * speedError * engineAcceleration / targetMagnitude;

            stopwatch.Stop();
            PerformanceMonitor.SetValue("CalculateDriveAcceleration", stopwatch.Elapsed.TotalMilliseconds);
            return driveAcceleration;
        }

        private void ApplyWheelForces(
            Vector3 vehicleForward,
            float forwardSpeed,
            float driveAcceleration)
        {
            float vehicleMass = Mathf.Max(1.0f, m_RigidBody.mass);
            float driveTorque = driveAcceleration * vehicleMass * m_WheelRadius / Mathf.Max(1, m_DrivenWheelCount);

            float brakeAcceleration = Mathf.Max(0.0f, m_HandlingData.BrakeDeceleration) * Mathf.Clamp01(m_fBrakePedal);

            float brakeForce = brakeAcceleration * vehicleMass;
            float brakeBias = Mathf.Clamp01(m_HandlingData.BrakeBias);
            float frontBrakeTorque = brakeForce * brakeBias * m_WheelRadius * VehicleManager.VehicleData.BrakeForceMultiplier;
            float rearBrakeTorque = brakeForce * (1.0f - brakeBias) * m_WheelRadius * VehicleManager.VehicleData.BrakeForceMultiplier;

            if (m_HandBrake)
            {
                // GTA's handbrake is a very aggressive stop. Apply it to both
                // axles so the rear wheels cannot carry the entire braking
                // load and turn the vehicle into a slide.
                frontBrakeTorque = Mathf.Max(frontBrakeTorque, VehicleManager.VehicleData.HandbrakeTorque);
                rearBrakeTorque = Mathf.Max(rearBrakeTorque, VehicleManager.VehicleData.HandbrakeTorque);
            }

            int groundedWheelCount = 0;

            for (int i = 0; i < m_Wheels.Length; i++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                WheelCollider wheel = m_Wheels[i];
                if (wheel == null)
                {
                    if (m_EnableDiagnostics && IsPeriodicDiagnosticFrame)
                    {
                        Debug.LogWarning($"[CarAcceleration] Wheel {i} is null during force application.", this);
                    }
                    continue;
                }

                if (wheel.isGrounded)
                {
                    groundedWheelCount++;
                }

                bool braking = brakeAcceleration > 0.0f || m_HandBrake;
                bool driven = IsDrivenWheel(i);
                float driveSign = GetWheelDriveSign(wheel, vehicleForward);

                wheel.motorTorque = !braking && driven ? driveTorque * driveSign : 0.0f;
                wheel.brakeTorque = i < 2 ? frontBrakeTorque : rearBrakeTorque;
                stopwatch.Stop();
                PerformanceMonitor.SetValue("ApplyWheelForces", stopwatch.Elapsed.TotalMilliseconds);
            }
        }

        private void LogMotionSnapshot(
            Vector3 vehicleForward,
            float selectedForwardSpeed,
            float linearForwardSpeed,
            Vector3 linearVelocity,
            Vector3 angularVelocity,
            float driveAcceleration)
        {
            Vector3 localLinearVelocity = transform.InverseTransformDirection(linearVelocity);
            Vector3 localAngularVelocity = transform.InverseTransformDirection(angularVelocity);
            Vector3 rootForward = transform.forward;
            Vector3 rootNegativeForward = -transform.forward;

            Debug.Log(
                $"[CarAcceleration] Motion snapshot #{m_FixedUpdateCount}: " +
                $"requested={m_RequestedPedal:R}, gas={m_fGasPedal:R}, " +
                $"brake={m_fBrakePedal:R}, handBrake={m_HandBrake}, gear={m_CurrentGear}, " +
                $"velocitySource={(m_UseAngularVelocity ? "angularVelocity" : "linearVelocity")}, " +
                $"selectedForwardSpeed={selectedForwardSpeed:R}, " +
                $"linearForwardSpeed={linearForwardSpeed:R}, " +
                $"linearSpeedMagnitude={linearVelocity.magnitude:R}, " +
                $"angularSpeedMagnitude={angularVelocity.magnitude:R}, " +
                $"linearVelocity={linearVelocity.ToString("R")}, " +
                $"localLinearVelocity={localLinearVelocity.ToString("R")}, " +
                $"angularVelocity={angularVelocity.ToString("R")}, " +
                $"localAngularVelocity={localAngularVelocity.ToString("R")}, " +
                $"vehicleForward={vehicleForward.ToString("R")}, " +
                $"rootForward={rootForward.ToString("R")}, " +
                $"rootNegativeForward={rootNegativeForward.ToString("R")}, " +
                $"vehicleVsRootForwardDot={Vector3.Dot(vehicleForward, rootForward):R}, " +
                $"vehicleVsRootNegativeForwardDot={Vector3.Dot(vehicleForward, rootNegativeForward):R}, " +
                $"driveAcceleration={driveAcceleration:R}, " +
                $"position={m_RigidBody.position.ToString("R")}, " +
                $"rotation={m_RigidBody.rotation.eulerAngles.ToString("R")}",
                this);
        }

        private void LogWheelSnapshot(
            int wheelIndex,
            WheelCollider wheel,
            Vector3 vehicleForward,
            bool driven,
            float driveSign)
        {
            bool hasGroundHit = wheel.GetGroundHit(out WheelHit hit);
            Vector3 wheelRollingDirection = GetWheelRollingDirection(wheel);
            float wheelRollingAlignment =
                Vector3.Dot(wheelRollingDirection, vehicleForward);

            string contactDetails = hasGroundHit
                ? $"contactPoint={hit.point.ToString("R")}, " +
                  $"contactNormal={hit.normal.ToString("R")}, " +
                  $"contactForce={hit.force:R}, " +
                  $"forwardSlip={hit.forwardSlip:R}, sidewaysSlip={hit.sidewaysSlip:R}, " +
                  $"hitForward={hit.forwardDir.ToString("R")}, " +
                  $"hitSideways={hit.sidewaysDir.ToString("R")}"
                : "contact=<none>";

            Debug.Log(
                $"[CarAcceleration] Wheel {wheelIndex} snapshot: " +
                $"object='{wheel.name}', driven={driven}, grounded={wheel.isGrounded}, " +
                $"hasGroundHit={hasGroundHit}, enabled={wheel.enabled}, " +
                $"rpm={wheel.rpm:R}, motorTorque={wheel.motorTorque:R}, " +
                $"brakeTorque={wheel.brakeTorque:R}, steerAngle={wheel.steerAngle:R}, " +
                $"driveSign={driveSign:R}, " +
                $"wheelForward={wheel.transform.forward.ToString("R")}, " +
                $"wheelRollingDirection={wheelRollingDirection.ToString("R")}, " +
                $"wheelRollingAlignment={wheelRollingAlignment:R}, " +
                $"radius={wheel.radius:R}, sprungMass={wheel.sprungMass:R}, " +
                $"suspensionDistance={wheel.suspensionDistance:R}, " +
                contactDetails,
                this);
        }

        private void LogDriveState(string state, string details)
        {
            if (!m_EnableDiagnostics)
            {
                return;
            }

            if (state == m_LastDriveState && !IsPeriodicDiagnosticFrame)
            {
                return;
            }

            Debug.Log(
                $"[CarAcceleration] Drive state: {state}; {details}",
                this);

            m_LastDriveState = state;
        }

        private float GetGearSpeedMultiplier(int gear)
        {
            int gearCount = GetGearCount();

            if (gear <= VehicleManager.VehicleData.ReverseGear || gearCount <= 1 || gear >= gearCount)
            {
                return 1.0f;
            }

            return VehicleManager.VehicleData.LowerGearSpeedMultiplier;
        }

        private void LogGearState(float forwardSpeed, int gearBeforeUpdate, int gearCount)
        {
            if (!m_EnableDiagnostics)
            {
                return;
            }

            bool gearChanged = gearBeforeUpdate != m_CurrentGear;
            if (!gearChanged && !IsPeriodicDiagnosticFrame)
            {
                return;
            }

            Debug.Log(
                $"[CarAcceleration] Gear state: " +
                $"before={gearBeforeUpdate}, after={m_CurrentGear}, changed={gearChanged}, " +
                $"count={gearCount}, targetVelocity={GetGearTargetVelocity(m_CurrentGear):R}, " +
                $"forwardSpeed={forwardSpeed:R}, " +
                $"shiftUpSpeed={(m_CurrentGear < gearCount ? GetShiftUpSpeed(m_CurrentGear) : 0.0f):R}, " +
                $"shiftDownSpeed={(m_CurrentGear > 1 ? GetShiftDownSpeed(m_CurrentGear) : 0.0f):R}",
                this);
        }

        private int GetGearCount()
        {
            return Mathf.Max(1, m_HandlingData.TransmissionData.NumberOfGears);
        }

        private float GetGearTargetVelocity(int gear)
        {
            float maxVelocity = GetMaxVelocityMetersPerSecond();

            if (gear == VehicleManager.VehicleData.ReverseGear)
            {
                return -maxVelocity *
                    VehicleManager.VehicleData.ReverseSpeedRatio;
            }

            return maxVelocity *
                gear /
                GetGearCount();
        }

        private float GetMaxVelocityMetersPerSecond()
        {
            return Mathf.Max(0.0f, m_HandlingData.TransmissionData.MaxVelocity) * VehicleManager.VehicleData.GameSpeedToMetersPerSecond;
        }

        private float GetShiftUpSpeed(int gear)
        {
            float lowerGearSpeed = gear == 1
                ? 0.0f
                : GetGearTargetVelocity(gear - 1);

            return Mathf.Lerp(
                lowerGearSpeed,
                GetGearTargetVelocity(gear),
                ShiftUpFraction);
        }

        private float GetShiftDownSpeed(int gear)
        {
            float lowerGearSpeed = GetGearTargetVelocity(gear - 1);

            return Mathf.Lerp(
                lowerGearSpeed,
                GetGearTargetVelocity(gear),
                VehicleManager.VehicleData.ShiftDownFraction);
        }

        private bool IsDrivenWheel(int wheelIndex)
        {
            bool isFrontWheel = wheelIndex < 2;
            EDriveType driveType = m_HandlingData.TransmissionData.DriveType;

            return driveType == EDriveType.BothWheel ||
                (isFrontWheel && driveType == EDriveType.FrontWheel) ||
                (!isFrontWheel && driveType == EDriveType.BackWheel);
        }

        private static float GetWheelDriveSign(WheelCollider wheel, Vector3 vehicleForward)
        {
            // WheelCollider's positive motor torque drives the contact patch
            // opposite to the collider transform's forward axis. The logs
            // confirm that hit.forwardDir is -wheel.transform.forward for
            // these imported wheel frames. Compare the actual rolling
            // direction so the motor torque sign matches vehicleForward.
            Vector3 wheelRollingDirection = GetWheelRollingDirection(wheel);
            return Vector3.Dot(wheelRollingDirection, vehicleForward) < 0.0f ? -1.0f : 1.0f;
        }

        private static Vector3 GetWheelRollingDirection(WheelCollider wheel)
        {
            return -wheel.transform.forward;
        }

        private Vector3 GetVehicleForward()
        {
            // WheelCollider contact forward is opposite to the collider
            // transform's forward axis for the imported DFF wheel frames.
            // Use the actual rolling direction so the input convention,
            // linear velocity and motor torque all share the same forward.
            Vector3 forward = Vector3.zero;

            for (int i = 2; i < m_Wheels.Length; i++)
            {
                if (m_Wheels[i] != null)
                {
                    forward += GetWheelRollingDirection(m_Wheels[i]);
                }
            }

            if (forward.sqrMagnitude > 0.0001f)
            {
                return forward.normalized;
            }

            return transform.forward;
        }
    }
}
