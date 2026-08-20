using System;
using System.Collections;
using System.Collections.Generic;
using GTA3Unity;
using GTA3Unity.Core;
using UnityEngine;
using UnityEngine.VFX;

namespace OpenG3.Core
{
    public enum EVfxType
    {
        Fire,
        Smoke
    };

    public class VfxManager : MonoBehaviour
    {
        public static VfxManager Instance { get; private set; }

        [SerializeField] private VisualEffect m_FirePrefab;
        [SerializeField] private VisualEffect m_SmokePrefab;
        
        private List<VisualEffect> m_SpawnedVisualEffects = new();

        void Awake()
        {
            if(Instance == null)
            {
                Instance = this;
            }
        }

        private IEnumerator Start()
        {
            while (!FileLoader.Instance.IsDone)
            {
                yield return null;
            }
        }

        void Update()
        {
            if(PlayerController.Instance == null)
            {
                return;
            }

            RemoveDistanceVisualEffects();
        }

        private void RemoveDistanceVisualEffects()
        {
            List<VisualEffect> visualEffectsToRemove = new();
            foreach(var visualEffect in m_SpawnedVisualEffects)
            {
                if(visualEffect == null)
                {
                    // Temp fix for: "MissingReferenceException: The object of type 'UnityEngine.VFX.VisualEffect' has been destroyed but you are still trying to access it."
                    // In reality we should remove it from the list
                    continue;
                }

                float distance = Vector3.Distance(PlayerController.Instance.transform.position, visualEffect.transform.position);
                if(distance > 50.0f)
                {
                    visualEffectsToRemove.Add(visualEffect);
                }
            }
            foreach(var vfx in visualEffectsToRemove)
            {
                Destroy(vfx.gameObject);
                m_SpawnedVisualEffects.Remove(vfx);
            }
        }

        public VisualEffect SpawnFire(Vector3 position)
        {
            return SpawnVisualEffect(m_FirePrefab, position, "flame1", "FireTexture");
        }

        public VisualEffect SpawnSmoke(Vector3 position)
        {
            return SpawnVisualEffect(m_SmokePrefab, position, "cloudmasked");
        }

        private VisualEffect SpawnVisualEffect(VisualEffect prefab, Vector3 position, string textureName, string texturePropertyName = "MainTexture")
        {
            var vfxObject = GameObject.Instantiate(prefab, position, Quaternion.identity);
            if(vfxObject == null)
            {
                Debug.LogError($"Unable to spawn {textureName} vfx");
                return null;
            }
            m_SpawnedVisualEffects.Add(vfxObject);

            var texture = FileLoader.Instance.GetFrontendTexture(textureName, "particle");
            if(texture == null)
            {
                Debug.LogError($"Cannot set texture: particle.txd does not contain {textureName}");
                return vfxObject;
            }
            vfxObject.SetTexture(texturePropertyName, texture);
            return vfxObject;
        }
    }
}
