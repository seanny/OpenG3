using UnityEngine;

namespace GTA3Unity.Vehicles
{
    public enum EDoorState
    {
        Closed,
        Open,
        Swinging
    };

    public sealed class VehicleDoor: MonoBehaviour
    {
        private static readonly Vector3 s_SpeedOffset = new Vector3(1.0f, 0.0f, 0.0f);

        public float MaxAngle;
        public float MinAngle;
        public int Direction;
        public int Axis;
        public EDoorState DoorState;
        public float Angle;
        public float PreviousAngle;
        public float AngularVelocity;
        public Vector3 Speed;

        public float ClosedAngle
        {
            get
            {
                if(Mathf.Abs(MaxAngle) < Mathf.Abs(MinAngle))
                {
                    return MaxAngle;
                }
                else
                {
                    return MinAngle;
                }
            }
        }

        public float OpenedAngle
        {
            get
            {
                if(Mathf.Abs(MaxAngle) < Mathf.Abs(MinAngle))
                {
                    return MinAngle;
                }
                else
                {
                    return MaxAngle;
                }
            }
        }

        public float OpenRatioAngle
        {
            get
            {
                if(OpenedAngle == 0.0f)
                {
                    return 0.0f;
                }
                return Angle / OpenedAngle;
            }
        }

        public bool IsOpen
        {
            get
            {
                if(Mathf.Abs(Angle) < Mathf.Abs(OpenedAngle) - 0.5f)
                {
                    return false;
                }
                return true;
            }
        }

        public bool IsClosed
        {
            get
            {
                return Angle == ClosedAngle;
            }
        }

        public void OnStart(float minAngle, float maxAngle, int direction, int axis)
        {
            MinAngle = minAngle;
            MaxAngle = maxAngle;
            Direction = direction;
            Axis = axis;
        }

        public void Open(float ratio)
        {
            float open;
            PreviousAngle = Angle;
            open = OpenedAngle;
            if(ratio < 1.0f)
            {
                Angle = open*ratio;
                if(Angle == 0.0f)
                {
                    AngularVelocity = 0.0f;
                }
            }
            else
            {
                DoorState = EDoorState.Open;
                Angle = open;
            }
        }

        public void OnUpdate(Car car)
        {
            Vector3 speed = car.GetSpeed(s_SpeedOffset);
            Vector3 speedDifference = speed - Speed;

            speedDifference = car.transform.InverseTransformDirection(speedDifference);

            float speedDifferenceAlongDoor = 0.0f;
            switch(Axis)
            {
                case 0:
                    speedDifferenceAlongDoor = Direction != 0 ?
                        speedDifference.y + speedDifference.z :
                        -(speedDifference.y + speedDifference.z);
                    break;
                case 2:
                    speedDifferenceAlongDoor = Direction != 0 ?
                        -(speedDifference.y + speedDifference.x) :
                        speedDifference.y - speedDifference.x;
                    break;
            }

            speedDifferenceAlongDoor = Mathf.Clamp(speedDifferenceAlongDoor, -0.2f, 0.2f);
            if(Mathf.Abs(speedDifferenceAlongDoor) > 0.002f)
            {
                AngularVelocity += speedDifferenceAlongDoor;
            }
            AngularVelocity *= 0.945f;
            AngularVelocity = Mathf.Clamp(AngularVelocity, -0.3f, 0.3f);

            Angle += AngularVelocity;
            DoorState = EDoorState.Swinging;
            if(Angle > MaxAngle)
            {
                Angle = MaxAngle;
                AngularVelocity *= -0.8f;
                DoorState = EDoorState.Open;
            }
            if(Angle < MinAngle)
            {
                Angle = MinAngle;
                AngularVelocity *= -0.8f;
                DoorState = EDoorState.Closed;
            }

            Speed = speed;
        }
    }
}
