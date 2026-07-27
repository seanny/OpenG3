using System;
using UnityEngine;
using IdeCar = RenderWareIo.Structs.Ide.Car;

namespace GTA3Unity.Vehicles
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
        }

        internal WheelCollider[] Wheels { get; private set; } = Array.Empty<WheelCollider>();

        internal float WheelRadius { get; private set; }

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

                Wheels[wheelIndex] = wheel;
                CreateWheelVisual(
                    wheelIndex,
                    wheelAnchor.transform,
                    ideCar.WheelModelId,
                    ideCar.WheelScale);
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
                extremumSlip = 0.4f,
                extremumValue = 1.0f,
                asymptoteSlip = 0.8f,
                asymptoteValue = 0.9f,
                stiffness = stiffness
            };
        }
    }
}
