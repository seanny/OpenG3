using OpenG3.Vehicles;
using UnityEngine;

namespace GTA3Unity.Core
{
    public class GtaObject : MonoBehaviour
    {
        protected static readonly Vector3 s_ModelBasisPosition = new Vector3(0, 1.0f, 0.0f);
        private static readonly Quaternion s_ModelBasisRotation = Quaternion.Euler(-90.0f, 180.0f, 0.0f);
        public int ModelIndex;
        protected GameObject m_PedModel;
        [SerializeField] protected float m_Health;

        public virtual void DamageHealth(float damage)
        {
            m_Health -= damage;
        }

        public virtual void SetModel(int modelIndex)
        {
            if(FileLoader.Instance == null || !FileLoader.Instance.IsDone)
            {
                return;
            }

            if(m_PedModel != null)
            {
                Destroy(m_PedModel);
            }

            m_PedModel = InstantiateModel(modelIndex);
            ModelIndex = modelIndex;
        }

        protected virtual GameObject InstantiateModel(int modelIndex)
        {
            GameObject template = FileLoader.Instance.GetModel(modelIndex);

            if (template == null)
            {
                Debug.LogWarning($"Could not load model {modelIndex}.");
                return null;
            }

            var spawnedModel = GameObject.Instantiate<GameObject>(template);
            spawnedModel.name = template.name.Replace("_Template", string.Empty);
            spawnedModel.transform.SetParent(transform, worldPositionStays: false);
            spawnedModel.transform.localPosition = s_ModelBasisPosition;
            spawnedModel.transform.localRotation = s_ModelBasisRotation;
            spawnedModel.transform.localScale = Vector3.one;
            spawnedModel.SetActive(true);
            return spawnedModel;
        }
    }
}
