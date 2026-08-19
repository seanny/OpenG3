using System;
using System.Collections.Generic;
using UnityEngine;
using IdeCar = RenderWareIo.Structs.Ide.Car;

namespace OpenG3.Vehicles
{
    /// <summary>
    /// Builds and updates the wheel colliders and their visual models for a car.
    /// </summary>
    internal sealed class CarWheelSystem
    {
        private const int WheelCount = 4;
        private const int FrontWheelCount = 2;
        private const float MinimumSuspensionTravel = 0.05f;
        private const float MinSuspensionSpring = 10_000.0f;
        private const float MaxSuspensionSpring = 25_000.0f;
        private const float MinSuspensionDamper = 2_000.0f;
        private const float MaxSuspensionDamper = 5_000.0f;
        private const float WheelMass = 20.0f;
        private const float ExtremumSlip = 0.3f;
        private const float AsymptoteSlip = 0.6f;

        private static readonly Quaternion s_WheelColliderRotationCorrection =
            Quaternion.Euler(0.0f, 180.0f, 0.0f);

        private static readonly string[] s_WheelFrameNames =
        {
            "wheel_lf_dummy",
            "wheel_rf_dummy",
            "wheel_lb_dummy",
            "wheel_rb_dummy"
        };

        private readonly Transform m_VehicleTransform;
        private readonly Rigidbody m_RigidBody;
        private readonly LayerMask m_VehicleBodyMask;
        private readonly int m_VehicleWheelLayer;
        private readonly Func<int, GameObject> m_ModelFactory;
        private readonly Transform[] m_WheelVisuals = new Transform[WheelCount];
        private readonly Quaternion[] m_WheelVisualRotationOffsets = new Quaternion[WheelCount];
        private readonly WheelFrictionCurve[] m_BaseForwardFriction = new WheelFrictionCurve[WheelCount];
        private readonly WheelFrictionCurve[] m_BaseSidewaysFriction = new WheelFrictionCurve[WheelCount];
        private readonly EWheelState[] m_WheelStates = new EWheelState[WheelCount];
        private readonly EWheelDamageState[] m_WheelDamageStates = new EWheelDamageState[WheelCount];
        private readonly bool[] m_DrivenWheels = new bool[WheelCount];
        private readonly WheelSnapshot[] m_WheelSnapshots = new WheelSnapshot[WheelCount];

        internal CarWheelSystem(
            Transform vehicleTransform,
            Rigidbody rigidBody,
            LayerMask vehicleBodyMask,
            int vehicleWheelLayer,
            Func<int, GameObject> modelFactory)
        {
            m_VehicleTransform = vehicleTransform;
            m_RigidBody = rigidBody;
            m_VehicleBodyMask = vehicleBodyMask;
            m_VehicleWheelLayer = vehicleWheelLayer;
            m_ModelFactory = modelFactory;
            WheelSnapshots = Array.AsReadOnly(m_WheelSnapshots);
        }

        internal WheelCollider[] Wheels { get; private set; } = Array.Empty<WheelCollider>();

        internal float WheelRadius { get; private set; }

        internal bool IsInitialized => Wheels.Length == WheelCount;

        internal int WheelArrayLength => Wheels.Length;

        internal IReadOnlyList<WheelSnapshot> WheelSnapshots { get; private set; } =
            Array.Empty<WheelSnapshot>();

        internal static bool TryFindWheelFrames(
            Transform modelRoot,
            out Transform[] wheelFrames,
            out string missingFrameName)
        {
            wheelFrames = new Transform[WheelCount];
            missingFrameName = string.Empty;

            for (int wheelIndex = 0; wheelIndex < WheelCount; wheelIndex++)
            {
                wheelFrames[wheelIndex] = FindChildByName(modelRoot, s_WheelFrameNames[wheelIndex]);
                if (wheelFrames[wheelIndex] == null)
                {
                    missingFrameName = s_WheelFrameNames[wheelIndex];
                    wheelFrames = null;
                    return false;
                }
            }

            return true;
        }

        internal void CreateWheels(IdeCar ideCar, HandlingData handlingData, Transform[] wheelFrames)
        {
            WheelRadius = ideCar.WheelScale * 0.5f;
            Wheels = new WheelCollider[WheelCount];

            EDriveType driveType = handlingData.TransmissionData.DriveType;
            for (int wheelIndex = 0; wheelIndex < WheelCount; wheelIndex++)
            {
                bool isFrontWheel = wheelIndex < FrontWheelCount;
                m_DrivenWheels[wheelIndex] = driveType == EDriveType.BothWheel ||
                    (isFrontWheel && driveType == EDriveType.FrontWheel) ||
                    (!isFrontWheel && driveType == EDriveType.BackWheel);
                m_WheelStates[wheelIndex] = EWheelState.Normal;
                m_WheelDamageStates[wheelIndex] = EWheelDamageState.Intact;
                m_WheelSnapshots[wheelIndex] = CreateEmptySnapshot(wheelIndex, isFrontWheel);
            }

            float suspensionTravel = Mathf.Max(
                MinimumSuspensionTravel,
                Mathf.Abs(handlingData.SuspensionUpperLimit - handlingData.SuspensionLowerLimit));
            float suspensionTarget = Mathf.InverseLerp(
                handlingData.SuspensionUpperLimit,
                handlingData.SuspensionLowerLimit,
                0.0f);
            float suspensionSpring = Mathf.Clamp(
                Mathf.Abs(handlingData.SuspensionForceLevel) * 10_000.0f,
                MinSuspensionSpring,
                MaxSuspensionSpring);
            float suspensionDamper = Mathf.Clamp(
                Mathf.Abs(handlingData.SuspensionDampingLevel) * 20_000.0f,
                MinSuspensionDamper,
                MaxSuspensionDamper);

            float frontSuspensionShare = Mathf.Clamp01(handlingData.SuspensionBias);
            float rearSuspensionShare = 1.0f - frontSuspensionShare;
            float frontGripBias = Mathf.Max(0.01f, 2.0f * handlingData.TractionBias);
            float rearGripBias = Mathf.Max(0.01f, 2.0f - frontGripBias);

            for (int wheelIndex = 0; wheelIndex < WheelCount; wheelIndex++)
            {
                bool isFrontWheel = wheelIndex < FrontWheelCount;
                GameObject wheelAnchor = new GameObject(
                    $"WheelCollider_{s_WheelFrameNames[wheelIndex]}");
                wheelAnchor.transform.SetParent(m_VehicleTransform, false);

                if (m_VehicleWheelLayer >= 0)
                {
                    wheelAnchor.layer = m_VehicleWheelLayer;
                }

                wheelAnchor.transform.SetPositionAndRotation(
                    wheelFrames[wheelIndex].position,
                    wheelFrames[wheelIndex].rotation * s_WheelColliderRotationCorrection);

                WheelCollider wheel = wheelAnchor.AddComponent<WheelCollider>();
                wheel.radius = WheelRadius;
                wheel.mass = WheelMass;
                wheel.suspensionDistance = suspensionTravel;
                wheel.wheelDampingRate = 1.0f;
                wheel.sprungMass = Mathf.Max(
                    1.0f,
                    m_RigidBody.mass * (isFrontWheel ? frontSuspensionShare : rearSuspensionShare) * 0.5f);
                wheel.excludeLayers = m_VehicleBodyMask;

                JointSpring suspension = wheel.suspensionSpring;
                suspension.spring = suspensionSpring;
                suspension.damper = suspensionDamper;
                suspension.targetPosition = Mathf.Clamp01(suspensionTarget);
                wheel.suspensionSpring = suspension;

                float axleGripBias = isFrontWheel ? frontGripBias : rearGripBias;
                float wheelGripMultiplier = Mathf.Max(
                    0.01f,
                    VehicleManager.VehicleData.WheelGripMultiplier);
                wheel.forwardFriction = CreateFrictionCurve(
                    Mathf.Max(0.01f, handlingData.TractionLoss) *
                    axleGripBias *
                    wheelGripMultiplier);
                wheel.sidewaysFriction = CreateFrictionCurve(
                    Mathf.Max(0.01f, handlingData.TractionMultiplier) *
                    axleGripBias *
                    wheelGripMultiplier);

                m_BaseForwardFriction[wheelIndex] = wheel.forwardFriction;
                m_BaseSidewaysFriction[wheelIndex] = wheel.sidewaysFriction;

                Wheels[wheelIndex] = wheel;
                CreateWheelVisual(
                    wheelIndex,
                    wheelAnchor.transform,
                    ideCar.WheelModelId,
                    ideCar.WheelScale);
            }
        }

        internal bool TryGetWheelSnapshot(int wheelIndex, out WheelSnapshot snapshot)
        {
            if (!IsInitialized || wheelIndex < 0 || wheelIndex >= WheelCount)
            {
                snapshot = default;
                return false;
            }

            snapshot = m_WheelSnapshots[wheelIndex];
            return true;
        }

        internal bool TrySetWheelDamageState(
            int wheelIndex,
            EWheelDamageState damageState)
        {
            if (!IsInitialized || wheelIndex < 0 || wheelIndex >= WheelCount)
            {
                return false;
            }

            if (!IsValidDamageState(damageState))
            {
                return false;
            }

            EWheelDamageState currentState = m_WheelDamageStates[wheelIndex];
            if (currentState == damageState)
            {
                return true;
            }

            if (currentState != EWheelDamageState.Intact ||
                damageState == EWheelDamageState.Intact)
            {
                return false;
            }

            m_WheelDamageStates[wheelIndex] = damageState;
            ApplyWheelDamageState(wheelIndex);
            RefreshSnapshots();
            return true;
        }

        internal void RefreshSnapshots()
        {
            if (!IsInitialized)
            {
                return;
            }

            for (int wheelIndex = 0; wheelIndex < WheelCount; wheelIndex++)
            {
                WheelCollider wheel = Wheels[wheelIndex];
                WheelHit hit = default;
                bool hasContact = wheel != null && wheel.enabled &&
                    wheel.GetGroundHit(out hit);
                bool isGrounded = hasContact && wheel.isGrounded;
                Vector3 contactPoint = Vector3.zero;
                Vector3 contactNormal = Vector3.up;
                float contactForwardSpeed = 0.0f;
                float contactLateralSpeed = 0.0f;
                float forwardSlip = 0.0f;
                float sidewaysSlip = 0.0f;

                if (hasContact)
                {
                    contactPoint = hit.point;
                    contactNormal = hit.normal;
                    forwardSlip = hit.forwardSlip;
                    sidewaysSlip = hit.sidewaysSlip;

                    if (m_RigidBody != null)
                    {
                        Vector3 contactVelocity = m_RigidBody.GetPointVelocity(hit.point);
                        contactForwardSpeed = Vector3.Dot(contactVelocity, hit.forwardDir);
                        contactLateralSpeed = Vector3.Dot(contactVelocity, hit.sidewaysDir);
                    }
                }

                EWheelState state = DetermineWheelState(
                    wheelIndex,
                    hasContact,
                    contactForwardSpeed,
                    forwardSlip,
                    sidewaysSlip,
                    wheel != null ? wheel.rpm : 0.0f,
                    wheel != null ? wheel.motorTorque : 0.0f,
                    wheel != null ? wheel.brakeTorque : 0.0f);
                m_WheelStates[wheelIndex] = state;
                m_WheelSnapshots[wheelIndex] = new WheelSnapshot(
                    wheelIndex,
                    wheelIndex < FrontWheelCount,
                    isGrounded,
                    hasContact,
                    contactPoint,
                    contactNormal,
                    contactForwardSpeed,
                    contactLateralSpeed,
                    forwardSlip,
                    sidewaysSlip,
                    wheel != null ? wheel.rpm : 0.0f,
                    wheel != null ? wheel.motorTorque : 0.0f,
                    wheel != null ? wheel.brakeTorque : 0.0f,
                    state,
                    m_WheelDamageStates[wheelIndex]);
            }
        }

        internal void UpdateVisuals()
        {
            for (int wheelIndex = 0; wheelIndex < WheelCount; wheelIndex++)
            {
                WheelCollider wheel = Wheels[wheelIndex];
                Transform wheelVisual = m_WheelVisuals[wheelIndex];
                if (wheel == null || wheelVisual == null)
                {
                    continue;
                }

                wheel.GetWorldPose(out Vector3 position, out Quaternion rotation);
                wheelVisual.SetPositionAndRotation(
                    position,
                    rotation * m_WheelVisualRotationOffsets[wheelIndex]);
            }
        }

        private void CreateWheelVisual(
            int wheelIndex,
            Transform wheelAnchor,
            int wheelModelId,
            float wheelScale)
        {
            if (wheelModelId <= 0)
            {
                Debug.LogWarning("Vehicle has no wheel model.");
                return;
            }

            GameObject wheelVisual = m_ModelFactory(wheelModelId);
            if (wheelVisual == null)
            {
                Debug.LogWarning($"Could not load wheel model {wheelModelId}.");
                return;
            }

            Quaternion visualRotation = wheelVisual.transform.localRotation;
            wheelVisual.transform.SetParent(wheelAnchor, false);
            wheelVisual.transform.localPosition = Vector3.zero;
            wheelVisual.transform.localRotation = visualRotation;
            wheelVisual.transform.localScale = Vector3.one * Mathf.Max(0.01f, wheelScale);

            m_WheelVisuals[wheelIndex] = wheelVisual.transform;
            m_WheelVisualRotationOffsets[wheelIndex] = visualRotation;
        }

        private void ApplyWheelDamageState(int wheelIndex)
        {
            WheelCollider wheel = Wheels[wheelIndex];
            if (wheel == null)
            {
                return;
            }

            EWheelDamageState damageState = m_WheelDamageStates[wheelIndex];
            if (damageState == EWheelDamageState.Missing)
            {
                wheel.motorTorque = 0.0f;
                wheel.brakeTorque = 0.0f;
                wheel.enabled = false;
                return;
            }

            wheel.enabled = true;
            float tractionMultiplier = GetTractionMultiplier(
                damageState,
                VehicleManager.VehicleData.WheelBurstTractionMultiplier);

            WheelFrictionCurve forwardFriction = m_BaseForwardFriction[wheelIndex];
            forwardFriction.stiffness *= tractionMultiplier;
            wheel.forwardFriction = forwardFriction;

            WheelFrictionCurve sidewaysFriction = m_BaseSidewaysFriction[wheelIndex];
            sidewaysFriction.stiffness *= tractionMultiplier;
            wheel.sidewaysFriction = sidewaysFriction;
        }

        internal static float GetTractionMultiplier(
            EWheelDamageState damageState,
            float configuredBurstMultiplier)
        {
            return damageState == EWheelDamageState.Burst
                ? Mathf.Clamp01(configuredBurstMultiplier)
                : 1.0f;
        }

        internal static bool IsValidDamageState(EWheelDamageState damageState)
        {
            return damageState == EWheelDamageState.Intact ||
                damageState == EWheelDamageState.Burst ||
                damageState == EWheelDamageState.Missing;
        }

        private EWheelState DetermineWheelState(
            int wheelIndex,
            bool hasContact,
            float contactForwardSpeed,
            float forwardSlip,
            float sidewaysSlip,
            float rpm,
            float motorTorque,
            float brakeTorque)
        {
            if (m_WheelDamageStates[wheelIndex] == EWheelDamageState.Missing)
            {
                return EWheelState.Normal;
            }

            return WheelStateClassifier.Classify(
                m_WheelStates[wheelIndex],
                hasContact,
                m_DrivenWheels[wheelIndex],
                contactForwardSpeed,
                forwardSlip,
                sidewaysSlip,
                rpm,
                motorTorque,
                brakeTorque,
                VehicleManager.VehicleData.WheelStateSlipEnterThreshold,
                VehicleManager.VehicleData.WheelStateSlipExitThreshold,
                VehicleManager.VehicleData.WheelSpinContactSpeedThreshold,
                VehicleManager.VehicleData.WheelLockContactSpeedThreshold,
                VehicleManager.VehicleData.WheelLockRpmThreshold);
        }

        private static WheelSnapshot CreateEmptySnapshot(int wheelIndex, bool isFrontWheel)
        {
            return new WheelSnapshot(
                wheelIndex,
                isFrontWheel,
                false,
                false,
                Vector3.zero,
                Vector3.up,
                0.0f,
                0.0f,
                0.0f,
                0.0f,
                0.0f,
                0.0f,
                0.0f,
                EWheelState.Normal,
                EWheelDamageState.Intact);
        }

        private static Transform FindChildByName(Transform root, string targetName)
        {
            if (string.Equals(root.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                Transform match = FindChildByName(root.GetChild(childIndex), targetName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static WheelFrictionCurve CreateFrictionCurve(float stiffness)
        {
            return new WheelFrictionCurve
            {
                // Keep the peak of the tire curve at a lower slip value so
                // the car settles into a corner instead of skating first.
                extremumSlip = ExtremumSlip,
                extremumValue = 1.0f,
                asymptoteSlip = AsymptoteSlip,
                asymptoteValue = 0.95f,
                stiffness = stiffness
            };
        }
    }
}
