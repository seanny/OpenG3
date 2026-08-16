using System;
using System.Collections;
using System.Collections.Generic;
using GTA3Unity.Core;
using GTA3Unity;
using StarterAssets;
using UnityEngine;
using IdeCar = RenderWareIo.Structs.Ide.Car;

namespace OpenG3.Vehicles
{
    public enum EVehicleDoorIndex
    {
        Bonnet = 0,
        Boot,
        FrontLeft,
        FrontRight,
        RearLeft,
        RearRight,

        DoorCount
    }

    /// <summary>
    /// Coordinates the Unity lifecycle and data-driven setup of a car.
    /// </summary>
    public class Car : Vehicle
    {
        public Dictionary<EVehicleDoorIndex, VehicleDoor> VehicleDoors => m_VehicleDoors;

        private int m_VehicleBodyLayer = -1;
        private bool m_IsInitialized;
        private bool m_IsVan;
        private bool m_IsBus;
        private CarWheelSystem m_WheelSystem;
        private CarSteeringController m_SteeringController;
        private List<MeshRenderer> m_Renderers = new();
        private Dictionary<EVehicleDoorIndex, VehicleDoor> m_VehicleDoors = new();

        [SerializeField]
        private CarAcceleration m_CarAcceleration;

        public override bool SetHandlingData(string vehicleIdentifier)
        {
            bool result = base.SetHandlingData(vehicleIdentifier);
            if (!result || m_HandlingData == null)
            {
                m_IsVan = false;
                m_IsBus = false;
                return false;
            }

            m_IsVan = m_HandlingData.Flags.HasFlag(EHandlingFlags.IsVan);
            m_IsBus = m_HandlingData.Flags.HasFlag(EHandlingFlags.IsBus);
            return true;
        }

        public override void SetModel(int modelIndex)
        {
            base.SetModel(modelIndex);

            if(m_PedModel == null)
            {
                return;
            }

            // Vehicle bodies use WheelColliders for physics. Concave MeshColliders
            // cannot be attached to their dynamic Rigidbody.
            var renderers = m_PedModel.GetComponentsInChildren<MeshRenderer>();
            foreach(var renderer in renderers)
            {
                m_Renderers.Add(renderer);
            }
        }

        private void Awake()
        {
            m_VehicleBodyLayer = LayerMask.NameToLayer("VehicleBody");

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
            m_SteeringController?.SetInput(input.move.x);
        }

        private void FixedUpdate()
        {
            if (!m_IsInitialized)
            {
                return;
            }

            m_SteeringController?.Update(Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            if (!m_IsInitialized)
            {
                return;
            }

            m_WheelSystem?.UpdateVisuals();
        }

        private IEnumerator InitializeWhenReady()
        {
            while (FileLoader.Instance != null && !FileLoader.Instance.IsActuallyInit)
            {
                yield return null;
            }

            while(string.IsNullOrEmpty(VehicleIdentifier))
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
            ConfigureModelLod();
            ConfigureChassisCollision();

            if (!CarWheelSystem.TryFindWheelFrames(
                    m_PedModel.transform,
                    out Transform[] wheelFrames,
                    out string missingFrameName))
            {
                DisableVehicle(
                    $"Vehicle '{VehicleIdentifier}' is missing the wheel frame '{missingFrameName}'.");
                return;
            }

            CarDoorAssembler doorAssembler = new();
            doorAssembler.CreateDoors(m_PedModel, HandlingData, m_IsVan, m_IsBus, out var intactDoors, out var damagedDoors);

            ConfigureRigidbody();

            m_WheelSystem = new CarWheelSystem(
                transform,
                m_RigidBody,
                LayerMask.GetMask("VehicleBody", "VehicleWheel"),
                LayerMask.NameToLayer("VehicleWheel"),
                InstantiateModel);
            m_WheelSystem.CreateWheels(ideCar, HandlingData, wheelFrames);

            m_CarAcceleration.Initialize(
                m_WheelSystem.Wheels,
                m_WheelSystem.WheelRadius,
                HandlingData);
            m_SteeringController = new CarSteeringController(
                m_WheelSystem.Wheels,
                HandlingData.SteeringLock);
            var vehicleDoors = GetComponentsInChildren<VehicleDoor>();
            foreach(var vehicleDoor in vehicleDoors)
            {
                if(m_VehicleDoors.ContainsKey(vehicleDoor.VehicleDoorIndex))
                {
                    continue;
                }

                m_VehicleDoors.Add(vehicleDoor.VehicleDoorIndex, vehicleDoor);
            }
            m_IsInitialized = true;
        }

        private void ConfigureModelLod()
        {
            List<Renderer> lod0Renderers = new();
            List<Renderer> lod1Renderers = new();
            Renderer[] modelRenderers = m_PedModel.GetComponentsInChildren<Renderer>(true);

            for (int rendererIndex = 0; rendererIndex < modelRenderers.Length; rendererIndex++)
            {
                Renderer renderer = modelRenderers[rendererIndex];
                if (renderer.gameObject.name.EndsWith("_hi", StringComparison.OrdinalIgnoreCase))
                {
                    lod0Renderers.Add(renderer);
                }
                else if (renderer.gameObject.name.EndsWith("_vlo", StringComparison.OrdinalIgnoreCase))
                {
                    lod1Renderers.Add(renderer);
                }
            }

            if (lod0Renderers.Count == 0 || lod1Renderers.Count == 0)
            {
                return;
            }

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

        private void ConfigureChassisCollision()
        {
            MeshFilter chassisMesh = FindMeshFilterByName(m_PedModel.transform, "chassis_hi");
            if (chassisMesh == null || chassisMesh.sharedMesh == null)
            {
                Debug.LogWarning(
                    $"Vehicle '{VehicleIdentifier}' has no chassis_hi mesh for collision.");
                return;
            }

            MeshCollider chassisCollider = chassisMesh.GetComponent<MeshCollider>();
            if (chassisCollider == null)
            {
                chassisCollider = chassisMesh.gameObject.AddComponent<MeshCollider>();
            }

            chassisCollider.sharedMesh = chassisMesh.sharedMesh;
            chassisCollider.convex = true;
            chassisCollider.enabled = true;
        }

        private void DisableVehicle(string reason)
        {
            Debug.LogError($"Vehicle '{name}' disabled: {reason}", this);
            m_IsInitialized = false;
            enabled = false;
        }

        private static MeshFilter FindMeshFilterByName(Transform root, string targetName)
        {
            MeshFilter meshFilter = root.GetComponent<MeshFilter>();
            if (meshFilter != null &&
                string.Equals(root.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return meshFilter;
            }

            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                MeshFilter match = FindMeshFilterByName(root.GetChild(childIndex), targetName);
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
            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                SetLayerRecursively(root.GetChild(childIndex), layer);
            }
        }

        private static Vector3 ConvertGtaVectorToUnity(Vector3 value)
        {
            return new Vector3(value.x, value.z, -value.y);
        }

        public Vector3 GetSpeed(Vector3 offset)
        {
            return m_RigidBody.linearVelocity + Vector3.Cross(m_RigidBody.angularVelocity, offset);
        }

        public override void BlowUp()
        {
            m_RigidBody.AddForce(0, VehicleManager.VehicleData.VehicleBlowUpUpwardForce, 0, ForceMode.Impulse);
            SetState(EVehicleState.Wrecked);
            m_VehicleHealth = 0f;
            foreach(var renderer in m_Renderers)
            {
                foreach(var material in renderer.materials)
                {
                    if(material.shader.name.Contains("SimpleBurnableLit"))
                    {
                        material.SetFloat("_Burnt", VehicleManager.VehicleData.VehicleShaderBurntMax);
                    }
                }
            }
            UnparentWheels();
            UnparentDoors();
        }

        private void UnparentDoors()
        {
            if (m_PedModel == null)
            {
                return;
            }

            var doors = m_PedModel.GetComponentsInChildren<GameObject>();
            for (int i = 0; i < doors.Length; i++)
            {
                if(doors[i].name.StartsWith("door_"))
                {
                    doors[i].transform.SetParent(null);
                }
            }
        }

        private void UnparentWheels()
        {
            WheelCollider[] wheels = m_WheelSystem?.Wheels;
            if (wheels == null)
            {
                return;
            }

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelCollider wheel = wheels[i];
                if (wheel != null)
                {
                    wheel.transform.SetParent(null);
                }
            }
        }
    }
}
