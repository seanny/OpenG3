using StarterAssets;
using UnityEngine;

namespace GTA3Unity.Vehicles
{
    [RequireComponent(typeof(Rigidbody))]
    public class CarAcceleration : MonoBehaviour
    {
        private const int ReverseGear = 0;
        private const float ReverseSpeedRatio = 0.2f;
        private const float ShiftUpFraction = 2.0f / 3.0f;
        private const float ShiftDownFraction = 0.42f;
        private const float StopSpeed = 0.1f;
        private const float InputDeadZone = 0.001f;
        private const float HandbrakeTorque = 20_000.0f;
        private const float LowerGearSpeedMultiplier = 4.0f;
        private const float CoastingBrakeFraction = 0.1f;
        private const float MinimumCoastingDeceleration = 0.5f;

        [Header("GTA Acceleration State")]
        [SerializeField] private Vector3 m_MovementSpeed;
        [SerializeField] private float m_fGasPedal;
        [SerializeField] private float m_fBrakePedal;
        [SerializeField] private int m_CurrentGear = 1;

        [SerializeField] private bool m_UseAngularVelocity;

        private Rigidbody m_RigidBody;
        private WheelCollider[] m_Wheels = new WheelCollider[0];
        private HandlingData m_HandlingData;
        private float m_WheelRadius;
        private float m_RequestedPedal;
        private bool m_HandBrake;
        private int m_DrivenWheelCount;
        private bool m_IsInitialized;

        private void Awake()
        {
            m_RigidBody = GetComponent<Rigidbody>();
        }

        internal void Initialize(
            WheelCollider[] wheels,
            float wheelRadius,
            HandlingData handlingData)
        {
            if (wheels == null || wheelRadius <= 0.0f || handlingData == null)
            {
                m_IsInitialized = false;
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
            m_IsInitialized = m_DrivenWheelCount > 0;

            Debug.Log(
                $"[CarAcceleration] Initialize: " +
                $"wheels={m_Wheels.Length}, drivenWheels={m_DrivenWheelCount}, " +
                $"initialized={m_IsInitialized}, " +
                $"driveType={m_HandlingData.TransmissionData.DriveType}, " +
                $"gears={GetGearCount()}, " +
                $"maxVelocity={m_HandlingData.TransmissionData.MaxVelocity:R}, " +
                $"engineAcceleration={m_HandlingData.TransmissionData.EngineAcceleration:R}, " +
                $"brakeDeceleration={m_HandlingData.BrakeDeceleration:R}, " +
                $"brakeBias={m_HandlingData.BrakeBias:R}, " +
                $"wheelRadius={m_WheelRadius:R}, " +
                $"rigidbodyMass={m_RigidBody?.mass:R}, " +
                $"isKinematic={m_RigidBody?.isKinematic}",
                this);

            for (int i = 0; i < m_Wheels.Length; i++)
            {
                WheelCollider wheel = m_Wheels[i];
                Debug.Log(
                    $"[CarAcceleration] Wheel {i}: " +
                    $"exists={wheel != null}, driven={wheel != null && IsDrivenWheel(i)}, " +
                    $"enabled={wheel != null && wheel.enabled}, " +
                    $"position={(wheel != null ? wheel.transform.position.ToString("R") : "<null>")}, " +
                    $"forward={(wheel != null ? wheel.transform.forward.ToString("R") : "<null>")}",
                    this);
            }
        }

        public void OnInput(StarterAssetsInputs input)
        {
            if (input == null)
            {
                return;
            }

            // Input is sampled here and consumed from FixedUpdate. The input
            // object itself is never changed while calculating braking.
            m_RequestedPedal = Mathf.Clamp(input.move.y, -1.0f, 1.0f);
            m_HandBrake = input.handBrake;
        }

        private void FixedUpdate()
        {
            if (!m_IsInitialized || m_RigidBody == null)
            {
                return;
            }

            if(m_UseAngularVelocity)
            {
                m_MovementSpeed = m_RigidBody.angularVelocity;
            }
            else
            {
                m_MovementSpeed = m_RigidBody.linearVelocity;
            }
            Vector3 vehicleForward = GetVehicleForward();
            float forwardSpeed = Vector3.Dot(m_MovementSpeed, vehicleForward);

            CalculatePedals(forwardSpeed);
            UpdateGear(forwardSpeed);

            float driveAcceleration = CalculateDriveAcceleration(forwardSpeed);
            ApplyWheelForces(vehicleForward, forwardSpeed, driveAcceleration);
        }

        private void CalculatePedals(float forwardSpeed)
        {
            m_fGasPedal = m_RequestedPedal;
            m_fBrakePedal = 0.0f;
            float absoluteForwardSpeed = Mathf.Abs(forwardSpeed);
            Debug.Log(
                $"[CarAcceleration] Pedals: requested={m_RequestedPedal:R}, " +
                $"gasPedal={m_fGasPedal:R}, " +
                $"brakePedal={m_fBrakePedal:R}, " +
                $"AbsoluteForwardSpeed={absoluteForwardSpeed:R}, " +
                $"forwardSpeed={forwardSpeed:R}, " +
                $"handBrake={m_HandBrake}, " +
                $"worldVelocity={m_MovementSpeed.ToString("R")}",
                this);

            // GTA brakes before changing direction. Once almost stationary,
            // the same input is allowed to select reverse or first gear.
            if (absoluteForwardSpeed > StopSpeed &&
                forwardSpeed * m_RequestedPedal < 0.0f)
            {
                m_fGasPedal = 0.0f;
                m_fBrakePedal = Mathf.Abs(m_RequestedPedal);
            }

            if (m_fBrakePedal > 0.0f)
            {
                Debug.Log(
                    $"[CarAcceleration] Direction change converted to braking: " +
                    $"gasPedal={m_fGasPedal:R}, brakePedal={m_fBrakePedal:R}, " +
                    $"forwardSpeed={forwardSpeed:R}",
                    this);
            }
        }

        private void UpdateGear(float forwardSpeed)
        {
            int gearCount = GetGearCount();
            int gearBeforeUpdate = m_CurrentGear;
            if (Mathf.Abs(forwardSpeed) <= StopSpeed)
            {
                if (m_fGasPedal > InputDeadZone)
                {
                    m_CurrentGear = 1;
                }
                else if (m_fGasPedal < -InputDeadZone)
                {
                    m_CurrentGear = ReverseGear;
                }

                LogGearState(forwardSpeed, gearBeforeUpdate, gearCount);
                return;
            }

            if (m_CurrentGear == ReverseGear)
            {
                LogGearState(forwardSpeed, gearBeforeUpdate, gearCount);
                return;
            }

            if (m_fGasPedal < -InputDeadZone)
            {
                LogGearState(forwardSpeed, gearBeforeUpdate, gearCount);
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
        }

        private float CalculateDriveAcceleration(float forwardSpeed)
        {
            float gearVelocity = GetGearTargetVelocity(m_CurrentGear);
            float speedMultiplier = GetGearSpeedMultiplier(m_CurrentGear);
            float targetVelocity = gearVelocity * speedMultiplier;
            float driveDirection = m_CurrentGear == ReverseGear ? -1.0f : 1.0f;
            float speedError = driveDirection * (targetVelocity - forwardSpeed);

            Debug.Log(
                $"[CarAcceleration] Drive calculation inputs: " +
                $"gasPedal={m_fGasPedal:R}, brakePedal={m_fBrakePedal:R}, " +
                $"handBrake={m_HandBrake}, drivenWheels={m_DrivenWheelCount}, " +
                $"gear={m_CurrentGear}, gearVelocity={gearVelocity:R}, " +
                $"speedMultiplier={speedMultiplier:R}, targetVelocity={targetVelocity:R}, " +
                $"driveDirection={driveDirection:R}, speedError={speedError:R}, " +
                $"engineAcceleration={m_HandlingData.TransmissionData.EngineAcceleration:R}",
                this);

            if (m_fGasPedal == 0.0f ||
                m_fBrakePedal > 0.0f ||
                m_HandBrake ||
                m_DrivenWheelCount == 0)
            {
                Debug.Log(
                    "[CarAcceleration] Drive acceleration blocked by pedal, brake, handbrake, or drivetrain state.",
                    this);
                return 0.0f;
            }

            if (speedError <= 0.0f)
            {
                Debug.Log(
                    $"[CarAcceleration] Drive acceleration blocked because speedError={speedError:R} <= 0. " +
                    "Check MaxVelocity, current gear, and vehicle-forward direction.",
                    this);
                return 0.0f;
            }

            float engineAcceleration = Mathf.Max(
                0.0f,
                m_HandlingData.TransmissionData.EngineAcceleration);
            float targetMagnitude = Mathf.Max(Mathf.Abs(targetVelocity), 0.01f);
            float gearSpeedLimit = Mathf.Abs(gearVelocity);
            if (Mathf.Abs(forwardSpeed) >= gearSpeedLimit)
            {
                Debug.Log(
                    $"[CarAcceleration] Drive acceleration blocked at gear speed limit: " +
                    $"speed={Mathf.Abs(forwardSpeed):R}, limit={gearSpeedLimit:R}",
                    this);
                return 0.0f;
            }

            float driveAcceleration = driveDirection *
                Mathf.Abs(m_fGasPedal) *
                speedError *
                engineAcceleration /
                targetMagnitude;

            Debug.Log(
                $"[CarAcceleration] Drive acceleration result: " +
                $"speedMultiplier={speedMultiplier:R}, " +
                $"targetMagnitude={targetMagnitude:R}, " +
                $"driveAcceleration={driveAcceleration:R}",
                this);

            return driveAcceleration;
        }

        private void ApplyWheelForces(
            Vector3 vehicleForward,
            float forwardSpeed,
            float driveAcceleration)
        {
            float vehicleMass = Mathf.Max(1.0f, m_RigidBody.mass);
            float driveTorque = driveAcceleration * vehicleMass * m_WheelRadius /
                Mathf.Max(1, m_DrivenWheelCount);

            float brakeAcceleration = Mathf.Max(0.0f, m_HandlingData.BrakeDeceleration) *
                Mathf.Clamp01(m_fBrakePedal);
            float coastingBrakeAcceleration = CalculateCoastingBrakeAcceleration(forwardSpeed);
            brakeAcceleration += coastingBrakeAcceleration;
            float brakeForce = brakeAcceleration * vehicleMass;
            float brakeBias = Mathf.Clamp01(m_HandlingData.BrakeBias);
            float frontBrakeTorque = brakeForce * brakeBias * m_WheelRadius / 2.0f;
            float rearBrakeTorque = brakeForce * (1.0f - brakeBias) * m_WheelRadius / 2.0f;
            if (m_HandBrake)
            {
                rearBrakeTorque = Mathf.Max(rearBrakeTorque, HandbrakeTorque);
            }

            Debug.Log(
                $"[CarAcceleration] Wheel force application: " +
                $"driveAcceleration={driveAcceleration:R}, driveTorque={driveTorque:R}, " +
                $"coastingBrakeAcceleration={coastingBrakeAcceleration:R}, " +
                $"frontBrakeTorque={frontBrakeTorque:R}, rearBrakeTorque={rearBrakeTorque:R}, " +
                $"rigidbodyVelocity={(m_UseAngularVelocity ? m_RigidBody.angularVelocity.ToString("R") : m_RigidBody.linearVelocity.ToString("R"))}, " +
                $"vehicleForward={vehicleForward.ToString("R")}",
                this);

            for (int i = 0; i < m_Wheels.Length; i++)
            {
                WheelCollider wheel = m_Wheels[i];
                if (wheel == null)
                {
                    continue;
                }

                bool braking = brakeAcceleration > 0.0f || m_HandBrake;
                float driveSign = GetWheelDriveSign(wheel, vehicleForward);
                wheel.motorTorque = !braking && IsDrivenWheel(i)
                    ? driveTorque * driveSign
                    : 0.0f;
                wheel.brakeTorque = i < 2 ? frontBrakeTorque : rearBrakeTorque;

                Debug.Log(
                    $"[CarAcceleration] Wheel {i} force: " +
                    $"driven={IsDrivenWheel(i)}, grounded={wheel.isGrounded}, " +
                    $"enabled={wheel.enabled}, driveSign={driveSign:R}, " +
                    $"motorTorque={wheel.motorTorque:R}, brakeTorque={wheel.brakeTorque:R}, " +
                    $"radius={wheel.radius:R}, suspensionDistance={wheel.suspensionDistance:R}",
                    this);
            }
        }

        private float CalculateCoastingBrakeAcceleration(float forwardSpeed)
        {
            if (Mathf.Abs(forwardSpeed) <= StopSpeed ||
                Mathf.Abs(m_fGasPedal) > InputDeadZone ||
                m_fBrakePedal > 0.0f ||
                m_HandBrake)
            {
                return 0.0f;
            }

            float handlingBrakeDeceleration = Mathf.Max(
                0.0f,
                m_HandlingData.BrakeDeceleration);
            float coastingDeceleration = Mathf.Max(
                MinimumCoastingDeceleration,
                handlingBrakeDeceleration * CoastingBrakeFraction);
            float decelerationNeededToStopThisStep =
                Mathf.Abs(forwardSpeed) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);

            return Mathf.Min(coastingDeceleration, decelerationNeededToStopThisStep);
        }

        private float GetGearSpeedMultiplier(int gear)
        {
            int gearCount = GetGearCount();
            if (gear <= ReverseGear || gearCount <= 1 || gear >= gearCount)
            {
                return 1.0f;
            }

            // This matches re3's integer-division behavior in
            // cTransmission::CalculateDriveAcceleration: lower forward gears
            // use a four-times target-speed multiplier, while top gear uses 1.
            return LowerGearSpeedMultiplier;
        }

        private void LogGearState(float forwardSpeed, int gearBeforeUpdate, int gearCount)
        {
            Debug.Log(
                $"[CarAcceleration] Gear state: " +
                $"before={gearBeforeUpdate}, after={m_CurrentGear}, count={gearCount}, " +
                $"targetVelocity={GetGearTargetVelocity(m_CurrentGear):R}, " +
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
            if (gear == ReverseGear)
            {
                return -Mathf.Max(0.0f, m_HandlingData.TransmissionData.MaxVelocity) *
                    ReverseSpeedRatio;
            }

            return Mathf.Max(0.0f, m_HandlingData.TransmissionData.MaxVelocity) *
                gear /
                GetGearCount();
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
                ShiftDownFraction);
        }

        private bool IsDrivenWheel(int wheelIndex)
        {
            bool isFrontWheel = wheelIndex < 2;
            EDriveType driveType = m_HandlingData.TransmissionData.DriveType;

            return driveType == EDriveType.BothWheel ||
                (isFrontWheel && driveType == EDriveType.FrontWheel) ||
                (!isFrontWheel && driveType == EDriveType.BackWheel);
        }

        private static float GetWheelDriveSign(
            WheelCollider wheel,
            Vector3 vehicleForward)
        {
            return Vector3.Dot(wheel.transform.forward, vehicleForward) < 0.0f
                ? -1.0f
                : 1.0f;
        }

        private Vector3 GetVehicleForward()
        {
            // The imported DFF model has its own basis rotation, so the car
            // root's -transform.forward is not guaranteed to be the wheel
            // rolling direction. Rear mounts are not affected by steering and
            // already contain the wheel-frame basis correction.
            Vector3 forward = Vector3.zero;
            for (int i = 2; i < m_Wheels.Length; i++)
            {
                if (m_Wheels[i] != null)
                {
                    forward += m_Wheels[i].transform.forward;
                }
            }

            if (forward.sqrMagnitude > 0.0001f)
            {
                return forward.normalized;
            }

            return -transform.forward;
        }
    }
}
