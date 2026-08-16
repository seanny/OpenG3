using System;
using GTA3Unity.Core;
using Unity.VisualScripting;
using UnityEngine;

namespace GTA3Unity.Vehicles
{
    public enum EDoorState
    {
        Closed,
        Open,
        Swinging
    };

    public enum EDoorSwingingState
    {
        None,
        Opening,
        Closing
    }

    public enum EDoorType
    {
        Bonnet,
        Boot,
        LeftDoor,
        RightDoor
    };

    public sealed class VehicleDoor: MonoBehaviour
    {
        #region Properties
        public EDoorState DoorState => m_DoorState;
        public float Speed => m_Speed;
        public bool IsDamaged => m_IsDamaged;
        public EDoorSwingingState SwingingState => m_SwingingState;
        public PedObject OpenedBy => m_OpenedBy;
        public EVehicleDoorIndex VehicleDoorIndex => m_VehicleDoorIndex;
        #endregion

        #region Events
        public event Action OnDoorOpen;
        public event Action<PedObject> OnPedDoorOpen;
        #endregion

        #region Fields
        [SerializeField] private EDoorState m_DoorState = EDoorState.Closed;
        [SerializeField] private Quaternion m_OpenRotation = Quaternion.identity;
        [SerializeField] private float m_Speed = 90;
        [SerializeField] private bool m_IsDamaged = false;
        [SerializeField] private EDoorSwingingState m_SwingingState = EDoorSwingingState.None;
        [SerializeField] private PedObject m_OpenedBy;
        [SerializeField] private EVehicleDoorIndex m_VehicleDoorIndex;
        [SerializeField] private MeshCollider m_MeshCollider;
        #endregion

        void Start()
        {
            var meshFilter = GetComponentInChildren<MeshFilter>();
            if(meshFilter == null)
            {
                return;
            }

            m_MeshCollider = GetComponent<MeshCollider>();
            if(m_MeshCollider == null)
            {
                m_MeshCollider = gameObject.AddComponent<MeshCollider>();
            }
            m_MeshCollider.convex = true;
            m_MeshCollider.isTrigger = true;
            m_MeshCollider.sharedMesh = meshFilter.mesh;


            var rigidBody = GetComponent<Rigidbody>();
            if(rigidBody == null)
            {
                rigidBody = gameObject.AddComponent<Rigidbody>();
            }
            rigidBody.isKinematic = true;
        }

        public void OnStart(EVehicleDoorIndex doorIndex)
        {
            // Get m_Speed value from vehicle_settings.dat
            m_Speed = VehicleManager.VehicleData.DoorOpenSpeed;
            m_VehicleDoorIndex = doorIndex;

            // Assign correct open rotations based on door type
            switch(doorIndex)
            {
                case EVehicleDoorIndex.Bonnet:
                    m_OpenRotation = Quaternion.Euler(-45f, 0f, 0f);
                    break;
                case EVehicleDoorIndex.Boot:
                    m_OpenRotation = Quaternion.Euler(45f, 0f, 0f);
                    break;
                case EVehicleDoorIndex.FrontLeft:
                case EVehicleDoorIndex.RearLeft:
                    m_OpenRotation = Quaternion.Euler(0f, 45f, 0f);
                    break;
                case EVehicleDoorIndex.FrontRight:
                case EVehicleDoorIndex.RearRight:
                    m_OpenRotation = Quaternion.Euler(0f, -45f, 0f);
                    break;
            }
        }

        /// <summary>
        /// Set open or closed state
        /// </summary>
        /// <param name="opened"></param>
        /// <param name="openedBy">Who opened/closed this door?</param>
        public void SetOpened(bool opened, PedObject openedBy = null)
        {
            if(opened)
            {
                m_SwingingState = EDoorSwingingState.Opening;
            }
            else
            {
                m_SwingingState = EDoorSwingingState.Closing;
            }
            m_OpenedBy = openedBy;
            m_DoorState = EDoorState.Swinging;
        }

        void Update()
        {
#if UNITY_EDITOR
            // So I can test damage state in editor
            EnsureDamaged(IsDamaged);
#endif
            if(m_SwingingState == EDoorSwingingState.Opening)
            {
                var targetRotation = m_OpenRotation;

                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, targetRotation, Speed * Time.deltaTime);
                if(Quaternion.Angle(transform.localRotation, targetRotation) <= 0.01f)
                {
                    transform.localRotation = targetRotation;
                    m_SwingingState = EDoorSwingingState.None;
                    m_DoorState = EDoorState.Open;
                    if(m_OpenedBy != null)
                    {
                        OnPedDoorOpen?.Invoke(m_OpenedBy);
                    }
                    else
                    {
                        OnDoorOpen?.Invoke();
                    }
                }
            }
            if(m_SwingingState == EDoorSwingingState.Closing)
            {
                var targetRotation = Quaternion.Euler(0, 0, 0);

                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, targetRotation, Speed * Time.deltaTime);
                if(Quaternion.Angle(transform.localRotation, targetRotation) <= 0.01f)
                {
                    transform.localRotation = targetRotation;
                    m_SwingingState = EDoorSwingingState.None;
                    m_DoorState = EDoorState.Closed;
                }
            }
        }

        public void SetDamaged(bool damaged)
        {
            m_IsDamaged = damaged;
            EnsureDamaged(damaged);
        }

        private void EnsureDamaged(bool damaged)
        {
            for(int i = 0; i < transform.childCount; i++)
            {
                var childObject = transform.GetChild(i);
                if(childObject.name.EndsWith("_ok"))
                {
                    childObject.gameObject.SetActive(!damaged);
                }
                if(childObject.name.EndsWith("_dam"))
                {
                    childObject.gameObject.SetActive(damaged);
                }
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if(m_IsDamaged == true)
            {
                return;
            }

            Vehicle parentVehicle = GetComponentInParent<Vehicle>();
            Vehicle vehicle = other.GetComponentInParent<Vehicle>();
            if(vehicle != null)
            {
                if(vehicle == parentVehicle)
                {
                    return;
                }

                SetDamaged(true);
                return;
            }

            // Static, solid colliders represent world geometry without making the door
            // a physical obstacle. Ignore trigger volumes and pedestrian colliders.
            if(other.attachedRigidbody == null && !other.isTrigger &&
               other.GetComponentInParent<PedObject>() == null)
            {
                SetDamaged(true);
            }
        }
    }
}
