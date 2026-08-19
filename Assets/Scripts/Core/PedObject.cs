using System;
using UnityEngine;

namespace GTA3Unity.Core
{
    public enum EPedState
    {
        OnFoot,
        Driving,
        Passenger,
        Dead
    };

    public class PedObject : GtaObject
    {
        public EPedState PedState => m_PedState;
        public bool IsMissionPed => m_IsMissionPed;
        public float Health => m_Health;
        protected float m_ExplosionImpactTime;

        [Header("Animations")]
        [SerializeField] protected string m_ExplosionFallAnimation = "KO_skid_front";
        [SerializeField] protected string m_GetupAnimation = "Getup";

        [Header("Explosion")]
        [SerializeField] protected float m_ExplosionKnockbackTime = 1f;
        [SerializeField] protected float m_GetUpTime = 1f;
        protected float m_GettingUpTime = 0f;

        [SerializeField] protected EPedState m_PedState = EPedState.OnFoot;
        [SerializeField] protected bool m_IsMissionPed = false;

        public override void DamageHealth(float damage)
        {
            base.DamageHealth(damage);
            if(m_Health <= 0f)
            {
                SetPedState(EPedState.Dead);
            }
        }

        public void ExplosionImpact()
        {
            m_ExplosionImpactTime = UnityEngine.Random.Range(4.0f, 6.0f);
        }
        
        public void SetPedState(EPedState pedState)
        {
            m_PedState = pedState;
        }

        public void SetMissionPed(bool isMissionPed)
        {
            m_IsMissionPed = isMissionPed;
        }

        public bool PlayAnimation(
            string animName,
            float fadeLength = 0.15f,
            WrapMode wrapMode = WrapMode.Loop,
            bool makeInPlace = false)
        {
            if(m_PedModel == null)
            {
                Debug.LogError($"Ped {name} does not have a PedModel attached.");
                return false;
            }

            return FileLoader.Instance.PlayPedAnimation(
                m_PedModel,
                animName,
                fadeLength,
                wrapMode,
                makeInPlace);
        }

        public void StartExplosionImpact()
        {
            Debug.Log($"[PedObject] Start explosion impact {m_ExplosionKnockbackTime}");
            m_ExplosionImpactTime = m_ExplosionKnockbackTime;
        }

        protected virtual void UpdateExplosionImpact()
        {
            if (m_ExplosionImpactTime <= 0f)
            {
                return;
            }
            PlayAnimation(m_ExplosionFallAnimation);
            m_ExplosionImpactTime -= Time.deltaTime;
            if (m_ExplosionImpactTime <= 0f)
            {
                GetUp();
            }
            return;
        }

        protected void GetUp()
        {
            m_GettingUpTime = m_GetUpTime;
            PlayAnimation(m_GetupAnimation);
        }

        protected virtual bool UpdateGetup()
        {
            if (m_GettingUpTime <= 0f)
            {
                return false;
            }
            m_GettingUpTime -= Time.deltaTime;
            return true;
        }
    }
}
