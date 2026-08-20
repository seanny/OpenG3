using System;
using System.Collections.Generic;
using GTA3Unity.Utility;
using RenderWareIo;
using RenderWareIo.Structs.Ide;
using UnityEngine;
using ImgFile = GTA3Unity.Img.ImgFile;

namespace GTA3Unity
{
    public static class MeshSpawn
    {
        private static readonly Dictionary<int, GameObject> s_Templates = new();

        private static int s_TemplateHits;
        private static int s_TemplateMisses;
        private static int s_SpawnFailures;

        public static GameObject SpawnMesh(
            RenderWareIo.Structs.Ide.Obj meshObj,
            Vector3 position,
            Quaternion rotation,
            ImgFile imgFileToReadFrom,
            Material fallbackMaterial,
            TxdMaterialCache materialCache)
        {
            string dffName = $"{meshObj.ModelName}.dff";

            if (!imgFileToReadFrom.Contains(dffName) &&
                !FileLoader.Instance.TryGetLooseDff(dffName, out _))
            {
                Debug.LogWarning($"Could not find DFF '{meshObj.ModelName}'.");
                s_SpawnFailures++;
                return null;
            }

            try
            {
                GameObject template = GetOrCreateTemplate<Obj>(
                    meshObj.Id,
                    meshObj,
                    imgFileToReadFrom,
                    fallbackMaterial,
                    materialCache);

                if (template == null)
                {
                    s_SpawnFailures++;
                    return null;
                }

                GameObject instance = UnityEngine.Object.Instantiate(template);
                instance.name = meshObj.ModelName;
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.transform.localScale = Vector3.one;
                instance.SetActive(true);
                return instance;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to spawn DFF '{meshObj.ModelName}': {exception.Message}");
                s_SpawnFailures++;
                return null;
            }
        }

        public static void ClearCache()
        {
            foreach (GameObject template in s_Templates.Values)
            {
                if (template == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(template);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(template);
                }
            }

            s_Templates.Clear();
            s_TemplateHits = 0;
            s_TemplateMisses = 0;
            s_SpawnFailures = 0;
        }

        public static void LogStats()
        {
            Debug.Log(
                $"DFF template cache: templates={s_Templates.Count}, " +
                $"hits={s_TemplateHits}, misses={s_TemplateMisses}, failures={s_SpawnFailures}.");
        }

        public static GameObject GetOrCreateTemplate<T>(
            int index,
            IModelTxd meshObj,
            ImgFile imgFileToReadFrom,
            Material fallbackMaterial,
            TxdMaterialCache materialCache) where T : IModelTxd
        {
            if (s_Templates.TryGetValue(index, out GameObject template))
            {
                s_TemplateHits++;
                return template;
            }

            s_TemplateMisses++;

            string dffName = $"{meshObj.ModelName}.dff";

            DffFile dffFile = null;

            if (!FileLoader.Instance.TryGetLooseDff(dffName, out dffFile))
            {
                if (!imgFileToReadFrom.Contains(dffName))
                {
                    Debug.LogWarning($"Could not find DFF '{dffName}'.");
                    s_SpawnFailures++;
                    return null;
                }

                GTA3Unity.Img.FileEntry entry = imgFileToReadFrom[dffName];
                dffFile = new DffFile(entry.GetData());
            }

            bool isPedDefinition =
                meshObj is RenderWareIo.Structs.Ide.Ped;

            template = isPedDefinition
                ? DffMeshConverter.CreatePedDffTemplate(
                    dffFile,
                    meshObj.ModelName,
                    meshObj.TxdName,
                    fallbackMaterial,
                    materialCache)
                : DffMeshConverter.CreateDffTemplate(
                    dffFile,
                    meshObj.ModelName,
                    meshObj.TxdName,
                    fallbackMaterial,
                    materialCache);

            if (template != null)
            {
                s_Templates[index] = template;
            }

            return template;
        }
    }
}
