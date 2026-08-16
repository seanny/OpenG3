using UnityEngine;

namespace OpenG3.Vehicles
{
    /// <summary>
    /// Applies smoothed steering input to the front wheel colliders of a car.
    /// </summary>
    internal sealed class CarSteeringController
    {
        private const int FrontWheelCount = 2;
        private const float SteeringResponsePerSecond = 14.0f;
        private const float SteeringReturnPerSecond = 18.0f;

        private readonly WheelCollider[] m_Wheels;
        private readonly float m_SteeringLock;
        private float m_TargetInput;
        private float m_SmoothedInput;

        internal CarSteeringController(WheelCollider[] wheels, float steeringLock)
        {
            m_Wheels = wheels;
            m_SteeringLock = steeringLock;
        }

        internal void SetInput(float input)
        {
            m_TargetInput = Mathf.Clamp(input, -1.0f, 1.0f);
        }

        internal void Update(float fixedDeltaTime)
        {
            if (m_Wheels == null || m_Wheels.Length < FrontWheelCount)
            {
                return;
            }

            // GTA3 reaches the requested steering angle quickly and keeps a
            // mostly linear relationship between stick input and wheel angle.
            // Squaring the input makes small corrections feel unresponsive and
            // gives the car a heavier, more simulation-like turn-in.
            float responsePerSecond = Mathf.Abs(m_TargetInput) > Mathf.Abs(m_SmoothedInput)
                ? SteeringResponsePerSecond
                : SteeringReturnPerSecond;
            m_SmoothedInput = Mathf.MoveTowards(
                m_SmoothedInput,
                m_TargetInput,
                responsePerSecond * Mathf.Max(0.0f, fixedDeltaTime));

            float steeringAngle = m_SteeringLock * m_SmoothedInput;

            for (int wheelIndex = 0; wheelIndex < FrontWheelCount; wheelIndex++)
            {
                WheelCollider wheel = m_Wheels[wheelIndex];
                if (wheel != null)
                {
                    // WheelCollider.steerAngle expects degrees.
                    wheel.steerAngle = steeringAngle;
                }
            }
        }
    }
}
