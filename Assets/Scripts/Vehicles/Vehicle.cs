using GTA3Unity.Core;
using GTA3Unity;
using OpenG3.Core;
using StarterAssets;
using UnityEngine;
using UnityEngine.VFX;

namespace OpenG3.Vehicles
{
    public enum EVehicleControlState
    {
        None,
        PlayerControlled,
        AiControlled,
        MissionControlled
    }

    public enum EVehicleLifecycleState
    {
        Active,
        Parked,
        Abandoned,
        Wrecked
    }

    public enum EVehicleType
    {
        Normal,
        Mission
    }

    [RequireComponent(typeof(Rigidbody))]
    public abstract class Vehicle : GtaObject
    {
        public int VehicleRuntimeId => m_VehicleRuntimeId;
        public string VehicleIdentifier => m_VehicleIdentifier;
        public PedObject Driver => m_Driver;
        public HandlingData HandlingData => m_HandlingData;
        public EVehicleControlState ControlState => m_ControlState;
        public EVehicleLifecycleState LifecycleState => m_LifecycleState;
        public bool IsFlippedOver => m_IsFlippedOver;
        public EVehicleType VehicleType => m_VehicleType;
        public float DeathTime => m_DeathTime;

        [SerializeField] private string m_VehicleIdentifier;
        [SerializeField] protected HandlingData m_HandlingData;
        [SerializeField] private PedObject m_Driver;
        [SerializeField] private EVehicleControlState m_ControlState = EVehicleControlState.None;
        [SerializeField] private EVehicleLifecycleState m_LifecycleState = EVehicleLifecycleState.Parked;
        [SerializeField] protected float m_VehicleHealth;
        [SerializeField] protected float DamageWhenFlipped = 40;
        [SerializeField] protected EVehicleType m_VehicleType = EVehicleType.Normal;
        [SerializeField] protected float m_DeathTime = 0f;

        private int m_VehicleRuntimeId = -1;
        protected VisualEffect m_FireVisualEffect;
        protected VisualEffect m_SmokeVisualEffect;
        protected Rigidbody m_RigidBody;
        private bool m_IsFlippedOver;
        private CharacterController m_DriverController;
        private bool m_DriverControllerWasEnabled;

        internal bool SetRuntimeId(int runtimeId)
        {
            if(m_VehicleRuntimeId > -1)
            {
                return false;
            }

            m_VehicleRuntimeId = runtimeId;
            return true;
        }

        protected virtual void Start()
        {
            m_RigidBody = GetComponent<Rigidbody>();
            Debug.Assert(m_RigidBody != null);
            m_VehicleHealth = 1000f;
        }

        private void OnDestroy()
        {
            VehicleSpawning.UnregisterSpawnedVehicle(this);
        }

        protected virtual void Update()
        {
            if (m_LifecycleState == EVehicleLifecycleState.Wrecked)
            {
                m_DeathTime += Time.deltaTime;
                return;
            }

            m_IsFlippedOver = transform.up.y < 0f;

            if (m_IsFlippedOver && m_VehicleHealth > 250)
            {
                float damage = VehicleManager.VehicleData.DamageWhenFlipped * Time.deltaTime;
                DamageVehicle(damage);
            }

            if (m_VehicleHealth <= 0f)
            {
                BlowUp();
                return;
            }

            // GTA 3 sets the flame and smoke at the "headlights" position
            if (m_VehicleHealth < 600)
            {
                // Spawn smoke VFX on car.
                if (m_SmokeVisualEffect == null)
                {
                    Vector3 enginePosition = new Vector3(0f, 1.25f, 1.5f); // Default for landstalker, used as fallback.
                    var headlightsTransform = transform.Find("headlights");
                    if (headlightsTransform != null)
                    {
                        enginePosition = headlightsTransform.position;
                    }
                    m_SmokeVisualEffect = VfxManager.Instance.SpawnSmoke(transform.position);
                    m_SmokeVisualEffect.transform.SetParent(transform);
                    m_SmokeVisualEffect.transform.localPosition = enginePosition;
                }
            }

            if (m_VehicleHealth < 250)
            {
                // Spawn fire VFX on car.
                if (m_FireVisualEffect == null)
                {
                    Vector3 enginePosition = new Vector3(0f, 1.25f, 1.5f); // Default for landstalker, used as fallback.
                    var headlightsTransform = transform.Find("headlights");
                    if (headlightsTransform != null)
                    {
                        enginePosition = headlightsTransform.position;
                    }
                    m_FireVisualEffect = VfxManager.Instance.SpawnFire(transform.position);
                    m_FireVisualEffect.transform.SetParent(transform);
                    m_FireVisualEffect.transform.localPosition = enginePosition;
                }

                // GTA3 seems to blow up vehicles by decreasing health. Once health is <= 0f, vehicle go BOOM!
                // This takes about 5-6 seconds after flame starts based on good old iOS Clock stopwatch.
                float damage = VehicleManager.VehicleData.DamageOnFire * Time.deltaTime;
                DamageVehicle(damage);
            }
        }

        public void SetVehicleType(EVehicleType vehicleType)
        {
            m_VehicleType = vehicleType;
        }

        public void SetVehicleIdentifier(string vehicleIdentifier)
        {
            m_VehicleIdentifier = vehicleIdentifier;
        }

        public virtual bool SetHandlingData(string vehicleIdentifier)
        {
            if (!HandlingManager.Data.TryGetValue(vehicleIdentifier, out HandlingData handlingData))
            {
                return false;
            }
            m_VehicleIdentifier = vehicleIdentifier;
            m_HandlingData = handlingData;
            return true;
        }

        public override void SetModel(int modelIndex)
        {
            base.SetModel(modelIndex);

            if (m_PedModel == null)
            {
                return;
            }

            // Vehicle bodies use WheelColliders for physics. Concave MeshColliders
            // cannot be attached to their dynamic Rigidbody.
            MeshCollider[] meshColliders = m_PedModel.GetComponentsInChildren<MeshCollider>(true);
            for (int i = 0; i < meshColliders.Length; i++)
            {
                meshColliders[i].enabled = false;
            }
        }

        public bool TrySetControlState(EVehicleControlState controlState)
        {
            if (m_LifecycleState == EVehicleLifecycleState.Wrecked)
            {
                return false;
            }

            switch (controlState)
            {
                case EVehicleControlState.None:
                    m_ControlState = controlState;
                    return true;
                case EVehicleControlState.PlayerControlled:
                case EVehicleControlState.AiControlled:
                case EVehicleControlState.MissionControlled:
                    m_ControlState = controlState;
                    m_LifecycleState = EVehicleLifecycleState.Active;
                    return true;
                default:
                    return false;
            }
        }

        public bool TrySetLifecycleState(EVehicleLifecycleState lifecycleState)
        {
            if (m_LifecycleState == EVehicleLifecycleState.Wrecked &&
                lifecycleState != EVehicleLifecycleState.Wrecked)
            {
                return false;
            }

            switch (lifecycleState)
            {
                case EVehicleLifecycleState.Active:
                    m_LifecycleState = lifecycleState;
                    return true;
                case EVehicleLifecycleState.Parked:
                case EVehicleLifecycleState.Abandoned:
                    if (m_ControlState != EVehicleControlState.None)
                    {
                        return false;
                    }

                    m_LifecycleState = lifecycleState;
                    return true;
                case EVehicleLifecycleState.Wrecked:
                    m_ControlState = EVehicleControlState.None;
                    m_LifecycleState = lifecycleState;
                    return true;
                default:
                    return false;
            }
        }

        public void SetDriver(PedObject ped)
        {
            if (ped == null)
            {
                return;
            }

            if (m_Driver == ped)
            {
                return;
            }

            if (m_Driver != null)
            {
                ClearDriver();
            }

            m_Driver = ped;
            m_DriverController = ped.GetComponent<CharacterController>();
            if (m_DriverController != null)
            {
                // A CharacterController parented to a dynamic vehicle can
                // create an impulse during entry and fight the vehicle body.
                m_DriverControllerWasEnabled = m_DriverController.enabled;
                m_DriverController.enabled = false;
            }

            ped.transform.SetParent(transform);
            ped.transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y, transform.eulerAngles.z);
        }

        public void ClearDriver()
        {
            if (m_Driver == null)
            {
                return;
            }

            PedObject driver = m_Driver;
            CharacterController driverController = m_DriverController;
            bool driverControllerWasEnabled = m_DriverControllerWasEnabled;
            m_Driver = null;
            m_DriverController = null;
            m_DriverControllerWasEnabled = false;

            driver.transform.SetParent(null, worldPositionStays: true);
            if (driverController != null)
            {
                driverController.enabled = driverControllerWasEnabled;
            }
        }

        public abstract void OnInput(StarterAssetsInputs input);

        protected override GameObject InstantiateModel(int modelIndex)
        {
            GameObject template = FileLoader.Instance.GetModel(modelIndex);

            if (template == null)
            {
                Debug.LogWarning($"Could not load model {modelIndex}.");
                return null;
            }

            var spawnedModel = GameObject.Instantiate<GameObject>(template);
            spawnedModel.name = template.name.Replace("_Template", string.Empty);
            spawnedModel.transform.SetParent(transform, worldPositionStays: false);
            spawnedModel.transform.localPosition = s_ModelBasisPosition;
            spawnedModel.transform.localRotation = Quaternion.identity;
            spawnedModel.transform.localScale = Vector3.one;
            spawnedModel.SetActive(true);
            return spawnedModel;
        }

        public abstract void BlowUp();

        public virtual void DamageVehicle(float damage)
        {
            if (m_VehicleHealth <= 0f)
            {
                return;
            }

            m_VehicleHealth -= damage;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (m_VehicleHealth <= 0f)
            {
                return;
            }

            float impulse = collision.impulse.magnitude / 50f; // gta3 units appears to be closest to: unity collision impulse magnitude / 50f

            if (impulse <= 25f)
            {
                // If impulse is less than 25, no damage is applied
                return;
            }


            float damage = (impulse - 25f) * HandlingData.CollisionDamageMultiplier * 0.6f;
            if (m_Driver != null && m_Driver is PlayerController)
            {
                damage /= 2f;
            }
            else
            {
                damage /= 4f;
            }
            Debug.Log($"Vehicle: {name}\n" +
                        $"Damage: {damage}\n" +
                        $"Magnitude: {collision.impulse.magnitude}\n" +
                        $"SqrMagnitude: {collision.impulse.sqrMagnitude}\n" +
                        $"Impulse: {impulse}\n" +
                        $"HandlingData.CollisionDamageMultiplier: {HandlingData.CollisionDamageMultiplier}");

            DamageVehicle(damage);
        }
    }
}
