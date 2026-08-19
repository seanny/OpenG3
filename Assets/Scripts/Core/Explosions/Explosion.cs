using System;
using GTA3Unity.Core;
using GTA3Unity.Peds;
using OpenG3.Vehicles;
using StarterAssets;
using UnityEngine;

namespace OpenG3.Core
{
    /// <summary>
    /// Short lived class that handles an explosion
    /// </summary>
    public sealed class Explosion: MonoBehaviour
    {
        public int RuntimeId => m_RuntimeId;
        public EExplosionType Type => m_Type;
        public float Radius => m_Radius;
        public float Power => m_Power;
        public float Delay => m_Delay;
        
        private int m_RuntimeId = -1;

        [SerializeField] private EExplosionType m_Type;
        [SerializeField][Min(1f)] private float m_Radius = 1f;
        [SerializeField] private float m_Delay;
        [SerializeField] private float m_StopTime = 0.75f;
        [SerializeField] private float m_Power;
        [SerializeField] private Light m_Light;

        public void SetRuntimeId(int runtimeId)
        {
            if(m_RuntimeId > -1) return;

            m_RuntimeId = runtimeId;
        }

        public void SetDelay(float lifetime)
        {
            m_Delay = lifetime;
        }

        public void SetStopTime(float stopTime)
        {
            m_StopTime = stopTime;
        }

        public void SetPower(float power)
        {
            m_Power = power;
        }

        public void SetRadius(float radius)
        {
            m_Radius = radius;
        }

        void Start()
        {
            if(m_Light == null)
            {
                m_Light = GetComponentInChildren<Light>();
            }
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            if(m_Delay > 0f)
            {
                m_Delay -= deltaTime;
                if(m_Delay < 0f)
                {
                    TriggerBlast();
                }
                return;
            }

            m_Radius += 0.5f * deltaTime;
            m_Light.range += m_Radius;
            m_Light.intensity += m_Radius * 1.5f;

            switch(m_Type)
            {
                case EExplosionType.CarDestroyed:
                case EExplosionType.CarBomb:
                    SpawnCarExplosionEffects();
                    break;
            }

            m_StopTime -= deltaTime;
            if(m_StopTime <= 0f)
            {
                ExplosionManager.DestroyExplosion(m_RuntimeId);
            }
        }

        private void SpawnCarExplosionEffects()
        {
            if(m_Radius <= 0f)
            {
                return;
            }

            var colliders = Physics.OverlapSphere(transform.position, m_Radius);
            foreach(var collider in colliders)
            {
                var gtaObject = collider.GetComponent<GtaObject>();
                if(gtaObject == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, gtaObject.transform.position);
                if(distance > m_Radius)
                {
                    continue;
                }

                float falloff = (m_Radius - gtaObject.transform.position.magnitude) * 2f / m_Radius;
                float damageMult = Mathf.Min(falloff, 1.0f);
                float baseDamage = 100f;
                if(gtaObject is Vehicle)
                {
                    baseDamage = 1000f;
                }
                if(gtaObject is PedObject)
                {
                    PedObject pedObject = (PedObject)gtaObject;
                    pedObject.StartExplosionImpact();
                }
                gtaObject.DamageHealth(baseDamage * damageMult);
            }
        }

        private void TriggerBlast()
        {
            // Play BOOM sound FX
        }
    }
}