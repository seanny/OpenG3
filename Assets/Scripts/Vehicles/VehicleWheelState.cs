using UnityEngine;

namespace OpenG3.Vehicles
{
    public enum EWheelState
    {
        Normal,
        Spinning,
        Skidding,
        Locked
    }

    public enum EWheelDamageState
    {
        Intact,
        Burst,
        Missing
    }

    /// <summary>
    /// Read-only per-wheel physics data exposed by an automobile.
    /// </summary>
    public readonly struct WheelSnapshot
    {
        public WheelSnapshot(
            int wheelIndex,
            bool isFrontWheel,
            bool isGrounded,
            bool hasContact,
            Vector3 contactPoint,
            Vector3 contactNormal,
            float contactForwardSpeed,
            float contactLateralSpeed,
            float forwardSlip,
            float sidewaysSlip,
            float rpm,
            float motorTorque,
            float brakeTorque,
            EWheelState state,
            EWheelDamageState damageState)
        {
            WheelIndex = wheelIndex;
            IsFrontWheel = isFrontWheel;
            IsGrounded = isGrounded;
            HasContact = hasContact;
            ContactPoint = contactPoint;
            ContactNormal = contactNormal;
            ContactForwardSpeed = contactForwardSpeed;
            ContactLateralSpeed = contactLateralSpeed;
            ForwardSlip = forwardSlip;
            SidewaysSlip = sidewaysSlip;
            Rpm = rpm;
            MotorTorque = motorTorque;
            BrakeTorque = brakeTorque;
            State = state;
            DamageState = damageState;
        }

        public int WheelIndex { get; }
        public bool IsFrontWheel { get; }
        public bool IsGrounded { get; }
        public bool HasContact { get; }
        public Vector3 ContactPoint { get; }
        public Vector3 ContactNormal { get; }
        public float ContactForwardSpeed { get; }
        public float ContactLateralSpeed { get; }
        public float ForwardSlip { get; }
        public float SidewaysSlip { get; }
        public float Rpm { get; }
        public float MotorTorque { get; }
        public float BrakeTorque { get; }
        public EWheelState State { get; }
        public EWheelDamageState DamageState { get; }
    }

    internal static class WheelStateClassifier
    {
        internal static EWheelState Classify(
            EWheelState previousState,
            bool hasContact,
            bool driven,
            float contactForwardSpeed,
            float forwardSlip,
            float sidewaysSlip,
            float rpm,
            float motorTorque,
            float brakeTorque,
            float slipEnterThreshold,
            float slipExitThreshold,
            float spinContactSpeedThreshold,
            float lockContactSpeedThreshold,
            float lockRpmThreshold)
        {
            if (!hasContact)
            {
                return EWheelState.Normal;
            }

            float slipEnter = Mathf.Max(0.01f, slipEnterThreshold);
            float slipExit = Mathf.Clamp(slipExitThreshold, 0.0f, slipEnter);
            float combinedSlip = Mathf.Sqrt(
                forwardSlip * forwardSlip + sidewaysSlip * sidewaysSlip);
            bool isBraking = brakeTorque > 0.001f;
            bool locked = isBraking &&
                Mathf.Abs(rpm) <= Mathf.Max(0.0f, lockRpmThreshold) &&
                Mathf.Abs(contactForwardSpeed) > Mathf.Max(0.0f, lockContactSpeedThreshold);
            if (locked)
            {
                return EWheelState.Locked;
            }

            bool spinning = driven &&
                Mathf.Abs(motorTorque) > 0.001f &&
                Mathf.Abs(contactForwardSpeed) <= Mathf.Max(0.0f, spinContactSpeedThreshold) &&
                Mathf.Abs(forwardSlip) >= slipEnter;
            bool skidding = combinedSlip >= slipEnter;

            if (spinning ||
                (previousState == EWheelState.Spinning &&
                 driven &&
                 Mathf.Abs(motorTorque) > 0.001f &&
                 Mathf.Abs(forwardSlip) >= slipExit))
            {
                return EWheelState.Spinning;
            }

            if (skidding ||
                (previousState == EWheelState.Skidding && combinedSlip >= slipExit))
            {
                return EWheelState.Skidding;
            }

            return EWheelState.Normal;
        }
    }
}
