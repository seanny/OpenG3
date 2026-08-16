using UnityEngine;

namespace OpenG3.Vehicles
{
    /// <summary>
    /// Applies smoothed steering input to the front wheel colliders of a car.
    /// </summary>
    internal sealed class CarSteeringController
    {
        private const int FrontWheelCount = 2;
        private const float SteeringResponsePerFrame = 0.2f;
        private const float OriginalFrameRate = 50.0f;

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

            float steeringResponse = Mathf.Clamp01(
                SteeringResponsePerFrame * fixedDeltaTime * OriginalFrameRate);

            m_SmoothedInput += (m_TargetInput - m_SmoothedInput) * steeringResponse;
            m_SmoothedInput = Mathf.Clamp(m_SmoothedInput, -1.0f, 1.0f);

            float shapedInput = Mathf.Sign(m_SmoothedInput) * m_SmoothedInput * m_SmoothedInput;
            float steeringAngle = m_SteeringLock * shapedInput;

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
