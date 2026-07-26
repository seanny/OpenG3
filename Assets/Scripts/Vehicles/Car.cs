using System;
using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using IdeCar = RenderWareIo.Structs.Ide.Car;

namespace GTA3Unity.Vehicles
{
    public partial class Car : Vehicle
    {
        private static readonly Quaternion s_WheelColliderRotationCorrection =
            Quaternion.Euler(0.0f, 180.0f, 0.0f);

        private const int WheelCount = 4;
        private const int FrontWheelCount = 2;
        private const float MinSuspensionSpring = 10_000.0f;
        private const float MaxSuspensionSpring = 25_000.0f;
        private const float MinSuspensionDamper = 2_000.0f;
        private const float MaxSuspensionDamper = 5_000.0f;
        private const float MinimumSuspensionTravel = 0.05f;
        private const float WheelMass = 20.0f;

        private static readonly string[] s_WheelFrameNames =
        {
            "wheel_lf_dummy",
            "wheel_rf_dummy",
            "wheel_lb_dummy",
            "wheel_rb_dummy"
        };

        private readonly WheelCollider[] m_Wheels = new WheelCollider[WheelCount];
        private readonly Transform[] m_WheelVisuals = new Transform[WheelCount];
        private readonly Quaternion[] m_WheelVisualRotationOffsets = new Quaternion[WheelCount];

        private float m_WheelRadius;
        private LayerMask m_LayerMaskVehicleBody;
        private int m_VehicleBodyLayer = -1;
        private int m_VehicleWheelLayer = -1;
        private bool m_IsInitialized;
        protected bool m_IsVan;
        protected bool m_IsBig;
        protected bool m_IsBus;
        protected bool m_IsLowVehicle;
        
        [SerializeField] private CarAcceleration m_CarAcceleration;

        public override void SetModel(int modelIndex)
        {
            base.SetModel(modelIndex);
        }

        public override bool SetHandlingData(string vehicleIdentifier)
        {
            var result = base.SetHandlingData(vehicleIdentifier);
            m_IsVan = m_HandlingData.Flags.HasFlag(EHandlingFlags.IsVan);
            m_IsBig = m_HandlingData.Flags.HasFlag(EHandlingFlags.IsBig);
            m_IsBus = m_HandlingData.Flags.HasFlag(EHandlingFlags.IsBus);
            m_IsLowVehicle = m_HandlingData.Flags.HasFlag(EHandlingFlags.IsLow);
            return result;
        }

        private void Awake()
        {
            m_LayerMaskVehicleBody = LayerMask.GetMask("VehicleBody", "VehicleWheel");
            m_VehicleBodyLayer = LayerMask.NameToLayer("VehicleBody");
            m_VehicleWheelLayer = LayerMask.NameToLayer("VehicleWheel");

            if (m_CarAcceleration == null)
            {
                m_CarAcceleration = GetComponent<CarAcceleration>();
                if (m_CarAcceleration == null)
                {
                    m_CarAcceleration = gameObject.AddComponent<CarAcceleration>();
                }
            }
        }

        protected override void Start()
        {
            base.Start();

            if (FileLoader.Instance == null)
            {
                DisableVehicle("FileLoader is not available.");
                return;
            }

            StartCoroutine(InitializeWhenReady());
        }

        public override void OnInput(StarterAssetsInputs input)
        {
            if (!m_IsInitialized || input == null)
            {
                return;
            }

            m_CarAcceleration?.OnInput(input);
            HandleSteeringInput(input);
        }

        private void LateUpdate()
        {
            if (!m_IsInitialized)
            {
                return;
            }

            for (int i = 0; i < WheelCount; i++)
            {
                UpdateWheelVisual(i);
            }
        }

        private void UpdateWheelVisual(int wheelIndex)
        {
            WheelCollider wheel = m_Wheels[wheelIndex];
            Transform wheelVisual = m_WheelVisuals[wheelIndex];
            if (wheel == null || wheelVisual == null)
            {
                return;
            }

            wheel.GetWorldPose(out Vector3 position, out Quaternion rotation);
            wheelVisual.SetPositionAndRotation(
                position,
                rotation * m_WheelVisualRotationOffsets[wheelIndex]);
        }

        private float m_TargetSteeringInput;
        private float m_SteeringInput;
        private float m_SteeringAngle;

        private void HandleSteeringInput(StarterAssetsInputs input)
        {
            m_TargetSteeringInput = Mathf.Clamp(
                input.move.x,
                -1.0f,
                1.0f);
        }

        private void FixedUpdate()
        {
            UpdateSteering();
        }

        private void UpdateSteering()
        {
            const float SteeringResponsePerFrame = 0.2f;
            const float OriginalFrameRate = 50.0f;

            float steeringResponse =
                SteeringResponsePerFrame *
                Time.fixedDeltaTime *
                OriginalFrameRate;

            steeringResponse = Mathf.Clamp01(steeringResponse);

            m_SteeringInput +=
                (m_TargetSteeringInput - m_SteeringInput) *
                steeringResponse;

            m_SteeringInput = Mathf.Clamp(
                m_SteeringInput,
                -1.0f,
                1.0f);

            float shapedSteeringInput =
                Mathf.Sign(m_SteeringInput) *
                m_SteeringInput *
                m_SteeringInput;

            // WheelCollider.steerAngle expects degrees.
            m_SteeringAngle =
                m_HandlingData.SteeringLock *
                shapedSteeringInput;

            for (int i = 0; i < FrontWheelCount; i++)
            {
                WheelCollider frontWheel = m_Wheels[i];

                if (frontWheel == null)
                {
                    continue;
                }

                frontWheel.steerAngle = m_SteeringAngle;
            }
        }

        private IEnumerator InitializeWhenReady()
        {
            while (FileLoader.Instance != null && !FileLoader.Instance.IsActuallyInit)
            {
                yield return null;
            }

            if (FileLoader.Instance == null)
            {
                DisableVehicle("FileLoader was destroyed before initialization completed.");
                yield break;
            }

            InitializeVehicle();
        }

        private void InitializeVehicle()
        {
            if (string.IsNullOrWhiteSpace(VehicleIdentifier))
            {
                DisableVehicle("VehicleIdentifier is empty; GTA handling data is required.");
                return;
            }

            if (!SetHandlingData(VehicleIdentifier) || HandlingData == null)
            {
                DisableVehicle($"No handling data was found for '{VehicleIdentifier}'.");
                return;
            }

            if (!FileLoader.Instance.TryGetCarDefinition(VehicleIdentifier, out IdeCar ideCar))
            {
                DisableVehicle($"No vehicle IDE definition was found for '{VehicleIdentifier}'.");
                return;
            }

            if (ideCar.WheelScale <= 0.0f)
            {
                DisableVehicle($"Vehicle '{VehicleIdentifier}' has an invalid wheel scale.");
                return;
            }

            SetModel(ideCar.Id);
            if (m_PedModel == null)
            {
                DisableVehicle($"Could not load the vehicle model for '{VehicleIdentifier}'.");
                return;
            }

            SetLayerRecursively(m_PedModel.transform, m_VehicleBodyLayer);

            List<Renderer> lod0Renderers = new();
            List<Renderer> lod1Renderers = new();
            Renderer[] modelRenderers = m_PedModel.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < modelRenderers.Length; i++)
            {
                Renderer renderer = modelRenderers[i];
                if (renderer.gameObject.name.EndsWith("_hi", StringComparison.OrdinalIgnoreCase))
                {
                    lod0Renderers.Add(renderer);
                }
                else if (renderer.gameObject.name.EndsWith("_vlo", StringComparison.OrdinalIgnoreCase))
                {
                    lod1Renderers.Add(renderer);
                }
            }

            if (lod0Renderers.Count > 0 && lod1Renderers.Count > 0)
            {
                LODGroup lodGroup = m_PedModel.GetComponent<LODGroup>();
                if (lodGroup == null)
                {
                    lodGroup = m_PedModel.AddComponent<LODGroup>();
                }
                lodGroup.fadeMode = LODFadeMode.CrossFade;
                lodGroup.animateCrossFading = true;

                lodGroup.SetLODs(new[]
                {
                    new LOD(0.8f, lod0Renderers.ToArray()),
                    new LOD(0.1f, lod1Renderers.ToArray())
                });
                lodGroup.RecalculateBounds();
            }

            ConfigureChassisCollision();

            Transform[] wheelFrames = new Transform[WheelCount];
            for (int i = 0; i < WheelCount; i++)
            {
                wheelFrames[i] = FindChildByName(m_PedModel.transform, s_WheelFrameNames[i]);
                if (wheelFrames[i] == null)
                {
                    DisableVehicle(
                        $"Vehicle '{VehicleIdentifier}' is missing the wheel frame '{s_WheelFrameNames[i]}'.");
                    return;
                }
            }

            ConfigureRigidbody();
            CreateWheels(ideCar, wheelFrames);
            m_CarAcceleration.Initialize(m_Wheels, m_WheelRadius, HandlingData);
            LoadVehicleDummy();
            m_IsInitialized = true;
        }

        private void ConfigureRigidbody()
        {
            float mass = Mathf.Max(1.0f, HandlingData.Mass);
            m_RigidBody.mass = mass;
            m_RigidBody.centerOfMass = ConvertGtaVectorToUnity(HandlingData.CentreOfMass);

            Vector3 dimensions = HandlingData.Dimensions;
            float aerodynamicArea = Mathf.Abs(dimensions.x * dimensions.z);
            m_RigidBody.linearDamping = aerodynamicArea / mass;
            m_RigidBody.angularDamping = 0.05f;
            m_RigidBody.interpolation = RigidbodyInterpolation.Interpolate;
            m_RigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void CreateWheels(
            IdeCar ideCar,
            Transform[] wheelFrames)
        {
            m_WheelRadius = ideCar.WheelScale * 0.5f;

            float suspensionTravel = Mathf.Max(
                MinimumSuspensionTravel,
                Mathf.Abs(HandlingData.SuspensionUpperLimit - HandlingData.SuspensionLowerLimit));
            float suspensionTarget = Mathf.InverseLerp(
                HandlingData.SuspensionUpperLimit,
                HandlingData.SuspensionLowerLimit,
                0.0f);
            float suspensionSpring = Mathf.Clamp(
                Mathf.Abs(HandlingData.SuspensionForceLevel) * 10_000.0f,
                MinSuspensionSpring,
                MaxSuspensionSpring);
            float suspensionDamper = Mathf.Clamp(
                Mathf.Abs(HandlingData.SuspensionDampingLevel) * 20_000.0f,
                MinSuspensionDamper,
                MaxSuspensionDamper);

            float frontSuspensionShare = Mathf.Clamp01(HandlingData.SuspensionBias);
            float rearSuspensionShare = 1.0f - frontSuspensionShare;
            float frontGripBias = Mathf.Max(0.01f, 2.0f * HandlingData.TractionBias);
            float rearGripBias = Mathf.Max(0.01f, 2.0f - frontGripBias);

            for (int i = 0; i < WheelCount; i++)
            {
                bool isFrontWheel = i < FrontWheelCount;
                GameObject wheelAnchor = new GameObject($"WheelCollider_{s_WheelFrameNames[i]}");
                wheelAnchor.transform.SetParent(transform, false);
                if (m_VehicleWheelLayer >= 0)
                {
                    wheelAnchor.layer = m_VehicleWheelLayer;
                }
                wheelAnchor.transform.SetPositionAndRotation(
                    wheelFrames[i].position,
                    wheelFrames[i].rotation * s_WheelColliderRotationCorrection);

                WheelCollider wheel = wheelAnchor.AddComponent<WheelCollider>();
                wheel.radius = m_WheelRadius;
                wheel.mass = WheelMass;
                wheel.suspensionDistance = suspensionTravel;
                wheel.wheelDampingRate = 1.0f;
                wheel.sprungMass = Mathf.Max(
                    1.0f,
                    m_RigidBody.mass * (isFrontWheel ? frontSuspensionShare : rearSuspensionShare) * 0.5f);
                wheel.excludeLayers = m_LayerMaskVehicleBody;

                JointSpring suspension = wheel.suspensionSpring;
                suspension.spring = suspensionSpring;
                suspension.damper = suspensionDamper;
                suspension.targetPosition = Mathf.Clamp01(suspensionTarget);
                wheel.suspensionSpring = suspension;

                float axleGripBias = isFrontWheel ? frontGripBias : rearGripBias;
                wheel.forwardFriction = CreateFrictionCurve(
                    Mathf.Max(0.01f, HandlingData.TractionLoss) * axleGripBias);
                wheel.sidewaysFriction = CreateFrictionCurve(
                    Mathf.Max(0.01f, HandlingData.TractionMultiplier) * axleGripBias);

                m_Wheels[i] = wheel;
                CreateWheelVisual(
                    i,
                    wheelAnchor.transform,
                    ideCar.WheelModelId,
                    ideCar.WheelScale);
            }
        }

        private void ConfigureChassisCollision()
        {
            MeshFilter chassisMesh = FindMeshFilterByName(
                m_PedModel.transform,
                "chassis_vlo");

            if (chassisMesh == null || chassisMesh.sharedMesh == null)
            {
                Debug.LogWarning(
                    $"Vehicle '{VehicleIdentifier}' has no chassis_vlo mesh for collision.");
                return;
            }

            MeshCollider chassisCollider =
                chassisMesh.GetComponent<MeshCollider>();
            if (chassisCollider == null)
            {
                chassisCollider = chassisMesh.gameObject.AddComponent<MeshCollider>();
            }

            chassisCollider.sharedMesh = chassisMesh.sharedMesh;
            chassisCollider.convex = true;
            chassisCollider.enabled = true;
        }

        private void LoadVehicleDummy()
        {
            LoadHeadlights();
            LoadTaillights();
            LoadReverseLights();
            LoadBrakeLights();
            LoadIndicators();
            LoadExhaust();
        }

        private void LoadHeadlights()
        {
            // TODO: Implement headlights.
        }

        private void LoadTaillights()
        {
            // TODO: Implement taillights.
        }

        private void LoadReverseLights()
        {
            // TODO: Implement reverse lights.
        }

        private void LoadBrakeLights()
        {
            // TODO: Implement brake lights.
        }

        private void LoadIndicators()
        {
            // TODO: Implement indicators.
        }

        private void LoadExhaust()
        {
            // TODO: Implement exhaust effects.
        }

        private void CreateWheelVisual(
            int wheelIndex,
            Transform wheelAnchor,
            int wheelModelId,
            float wheelScale)
        {
            if (wheelModelId <= 0)
            {
                Debug.LogWarning($"Vehicle '{VehicleIdentifier}' has no wheel model.");
                return;
            }

            GameObject wheelVisual = InstantiateModel(wheelModelId);
            if (wheelVisual == null)
            {
                Debug.LogWarning(
                    $"Could not load wheel model {wheelModelId} for vehicle '{VehicleIdentifier}'.");
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

        private void DisableVehicle(string reason)
        {
            Debug.LogError($"Vehicle '{name}' disabled: {reason}", this);
            m_IsInitialized = false;
            enabled = false;
        }

        private static Transform FindChildByName(Transform root, string targetName)
        {
            if (string.Equals(root.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindChildByName(root.GetChild(i), targetName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static MeshFilter FindMeshFilterByName(
            Transform root,
            string targetName)
        {
            MeshFilter meshFilter = root.GetComponent<MeshFilter>();
            if (meshFilter != null &&
                string.Equals(root.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return meshFilter;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                MeshFilter match = FindMeshFilterByName(root.GetChild(i), targetName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            if (layer < 0 || layer > 31)
            {
                return;
            }

            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }

        private static WheelFrictionCurve CreateFrictionCurve(float stiffness)
        {
            return new WheelFrictionCurve
            {
                extremumSlip = 0.4f,
                extremumValue = 1.0f,
                asymptoteSlip = 0.8f,
                asymptoteValue = 0.75f,
                stiffness = stiffness
            };
        }

        private static Vector3 ConvertGtaVectorToUnity(Vector3 value)
        {
            return new Vector3(value.x, value.z, -value.y);
        }

        private Vector3 GetVehicleForward()
        {
            return -transform.forward;
        }
    }
}
