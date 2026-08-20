using System.Collections;
using System.IO;
using GTA3Unity.Core;
using UnityEngine;
using UnityEngine.AI;

namespace GTA3Unity.Peds
{
    public class Ped: PedObject
    {
        public NavMeshAgent NavMeshAgent => m_NavMeshAgent;

        private NavMeshAgent m_NavMeshAgent;
        private PedStateMachine m_StateMachine;
        #region Unity Lifecycle
        private IEnumerator Start()
        {
            while(FileLoader.Instance == null || !FileLoader.Instance.IsDone)
            {
                yield return null;
            }

            m_NavMeshAgent = GetComponent<NavMeshAgent>();

            if (m_NavMeshAgent == null)
            {
                m_NavMeshAgent = gameObject.AddComponent<NavMeshAgent>();
            }

            if (FileLoader.Instance.TryGetRandomPedModelIndex(out int modelIndex))
            {
                SetModel(modelIndex);
            }
            else
            {
                Debug.LogWarning("Could not find any loadable ped models.");
            }

            m_StateMachine = new PedStateMachine(this);
        }

        private void Update()
        {
            if(m_StateMachine == null)
            {
                return;
            }

            m_StateMachine.OnUpdate(Time.deltaTime);
        }
        #endregion

        public override void SetModel(int modelIndex)
        {
            base.SetModel(modelIndex);
            FileLoader.Instance.PlayPedAnimation(m_PedModel);
        }

    }
}
