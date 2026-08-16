using System;
using System.Collections;
using GTA3Unity;
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

        public VisualEffect SpawnFire(Vector3 position)
        {
            return SpawnVisualEffect(m_SmokePrefab, position, "flame1", "FireTexture");
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
