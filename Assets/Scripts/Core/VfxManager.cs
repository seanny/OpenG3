using System;
using System.Collections;
using GTA3Unity;
using UnityEngine;
using UnityEngine.VFX;

namespace OpenG3.Core
{
    public enum EVfxType
    {
        Fire
    };

    public class VfxManager : MonoBehaviour
    {
        public static VfxManager Instance { get; private set; }

        [SerializeField] private VisualEffect m_FirePrefab;

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

        public VisualEffect SpawnVisualEffect(EVfxType vfxType, Vector3 position)
        {
            switch(vfxType)
            {
                case EVfxType.Fire:
                    return SpawnFireVfx(position);
            }
            return null;
        }

        private VisualEffect SpawnFireVfx(Vector3 position)
        {
            var vfxObject = GameObject.Instantiate(m_FirePrefab, position, Quaternion.identity);
            if(vfxObject == null)
            {
                Debug.LogError("Unable to spawn fire vfx");
                return null;
            }

            var texture = FileLoader.Instance.GetFrontendTexture("explo01", "particle");
            if(texture == null)
            {
                Debug.LogError($"Cannot set texture: particle.txd does not contain flame1");
                return vfxObject;
            }
            vfxObject.SetTexture("FireTexture", texture);
            return vfxObject;
        }
    }
}
