using System.Collections.Generic;
using System.Linq;
using GTA3Unity.Core;
using UnityEngine;

namespace OpenG3.Core
{
    public static class ExplosionManager
    {
        public static IReadOnlyDictionary<int, Explosion> Explosions => m_Explosions;

        public static List<Explosion> m_ExplosionPrefabs = new();
        private static Dictionary<int, Explosion> m_Explosions = new();
        private static int m_NextExplosionId = 0;

        private static int AllocateExplosionId()
        {
            return m_NextExplosionId++;
        }

        public static void Init()
        {
            m_ExplosionPrefabs = Resources.LoadAll<Explosion>("Explosions").ToList();
        }

        public static Explosion CreateExplosion(EExplosionType explosionType, Vector3 position)
        {
            int explosionId = AllocateExplosionId();
            Explosion explosionPrefab = m_ExplosionPrefabs.FirstOrDefault(explosion => explosion.Type == explosionType);
            if(explosionPrefab == null)
            {
                Debug.LogError($"[ExplosionManager] Cannot spawn {explosionType}");
                return null;
            }

            var explosion = Explosion.Instantiate<Explosion>(explosionPrefab);
            explosion.SetRuntimeId(explosionId);
            explosion.transform.position = position;
            m_Explosions.Add(explosionId, explosion);
            Debug.Log($"[ExplosionManager] Spawned {explosionType} explosion at {position}");
            return explosion;
        }

        public static void DestroyExplosion(int explosionId)
        {
            if(!Explosions.ContainsKey(explosionId))
            {
                return;
            }

            GameObject.Destroy(Explosions[explosionId].gameObject);
        }
    }
}