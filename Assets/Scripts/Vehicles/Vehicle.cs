using GTA3Unity.Core;
using RenderWareIo.Structs.Col;
using StarterAssets;
using UnityEngine;

namespace GTA3Unity.Vehicles
{
    public enum EVehicleState
    {
        Player, // Controlled by player using WheelCollider
        AiSimple, // Controlled by AI not using WheelCollider
        AiPhysics, // Controlled by AI using WheelCollider,
        Abandoned, // Self-explanitory
        Wrecked, // Vehicle is using wrecked model
    }

    [RequireComponent(typeof(Rigidbody))]
    public abstract class Vehicle: GtaObject
    {
        public string VehicleIdentifier => m_VehicleIdentifier;
        public PedObject Driver => m_Driver;
        public HandlingData HandlingData => m_HandlingData;
        public EVehicleState VehicleState => m_VehicleState;

        [SerializeField] private string m_VehicleIdentifier;
        [SerializeField] protected HandlingData m_HandlingData;
        [SerializeField] private PedObject m_Driver;
        [SerializeField] private EVehicleState m_VehicleState;
        [SerializeField] protected float m_VehicleHealth;
        [SerializeField] protected float DamageWhenOnFire = 50;

        protected Rigidbody m_RigidBody;
        private CharacterController m_DriverController;
        private bool m_DriverControllerWasEnabled;

        protected virtual void Start()
        {
            m_RigidBody = GetComponent<Rigidbody>();
            Debug.Assert(m_RigidBody != null);
            m_VehicleHealth = 1000f;
        }

        protected virtual void Update()
        {
            if(m_VehicleHealth <= 250)
            {
                // Spawn fire VFX on car.

                float damage = VehicleManager.VehicleData.DamageOnFire * Time.deltaTime; // Not sure how gta3 did vehicle damage, gonna do this for now
                DamageVehicle(damage);
            }
        }

        public void SetVehicleIdentifier(string vehicleIdentifier)
        {
            m_VehicleIdentifier = vehicleIdentifier;
        }

        public virtual bool SetHandlingData(string vehicleIdentifier)
        {
            if(!HandlingManager.Data.TryGetValue(vehicleIdentifier, out HandlingData handlingData))
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

            if(m_PedModel == null)
            {
                return;
            }

            // Vehicle bodies use WheelColliders for physics. Concave MeshColliders
            // cannot be attached to their dynamic Rigidbody.
            MeshCollider[] meshColliders = m_PedModel.GetComponentsInChildren<MeshCollider>(true);
            for(int i = 0; i < meshColliders.Length; i++)
            {
                meshColliders[i].enabled = false;
            }
        }

        public void SetState(EVehicleState vehicleState)
        {
            m_VehicleState = vehicleState;
        }

        public void SetDriver(PedObject ped)
        {
            if(ped == null)
            {
                return;
            }

            if(m_Driver == ped)
            {
                return;
            }

            if(m_Driver != null)
            {
                ClearDriver();
            }

            m_Driver = ped;
            m_DriverController = ped.GetComponent<CharacterController>();
            if(m_DriverController != null)
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
            if(m_Driver == null)
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
            if(driverController != null)
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
            if(m_VehicleHealth <= 0f)
            {
                return;
            }

            m_VehicleHealth -= damage;
        }

        void OnCollisionEnter(Collision collision)
        {
            if(m_VehicleHealth <= 0f)
            {
                return;
            }

            float impulse = collision.impulse.magnitude / 50f; // gta3 units appears to be closest to: unity collision impulse magnitude / 50f

            if(impulse <= 25f)
            {
                // If impulse is less than 25, no damage is applied
                return;
            }


            float damage = (impulse - 25f) * HandlingData.CollisionDamageMultiplier * 0.6f;            
            if(m_Driver != null && m_Driver is PlayerController)
            {
                damage /= 2f;
            }
            else
            {
                damage /= 4f;
            }
            Debug.Log(  $"Vehicle: {name}\n" +
                        $"Damage: {damage}\n" +
                        $"Magnitude: {collision.impulse.magnitude}\n" +
                        $"SqrMagnitude: {collision.impulse.sqrMagnitude}\n" +
                        $"Impulse: {impulse}\n" +
                        $"HandlingData.CollisionDamageMultiplier: {HandlingData.CollisionDamageMultiplier}");

            DamageVehicle(damage);
        }
    }
}
