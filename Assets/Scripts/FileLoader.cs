using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GTA3Unity.Utility;
using RenderWareIo;
using UnityEngine;
using Material = UnityEngine.Material;
using IdeObj = RenderWareIo.Structs.Ide.Obj;
using ImgFile = GTA3Unity.Img.ImgFile;
using GTA3Unity.Core;
using RenderWareIo.Structs.Ide;
using RenderWareIo.Structs.Ifp;
using Unity.AI.Navigation;
using GTA3Unity.UI;
using OpenG3.Vehicles;
using OpenG3.Core;

namespace GTA3Unity
{
    public sealed class FileLoader : MonoBehaviour
    {
        public static FileLoader Instance { get; private set; }

        public bool IsDone => m_IsDone;
        public bool IsActuallyInit => m_IsActuallyInit;
        public ImgFile MainImg => m_MainImg;
        public bool MapLoaded => m_MapLoaded;
        public int SpawnedCount => m_SpawnedCount;
        public int CountToLoad => m_CountToLoad;
        public IReadOnlyDictionary<string, DffFile> LooseDffFiles => m_LooseDff;

        public bool TryGetLooseDff(string dffName, out DffFile dffFile)
        {
            if (m_LooseDff.TryGetValue(dffName, out dffFile))
            {
                return true;
            }

            HashSet<DffFile> checkedFiles = new();
            foreach (DffFile looseDff in m_LooseDff.Values)
            {
                if (looseDff == null || !checkedFiles.Add(looseDff) ||
                    !looseDff.TryGetEmbeddedDff(dffName, out dffFile))
                {
                    continue;
                }

                m_LooseDff[dffName] = dffFile;
                return true;
            }

            dffFile = null;
            return false;
        }

        public Action OnMapLoaded;

        [SerializeField] private NavMeshSurface m_Surface;

        [SerializeField]
        private List<RenderWareIo.DatFile> m_DatFiles = new();

        [SerializeField]
        private List<IdeObj> m_Objects = new();

        [SerializeField]
        private List<RenderWareIo.Structs.Ide.Car> m_Cars = new();
        [SerializeField]
        private List<RenderWareIo.Structs.Ide.Ped> m_Peds = new();
        [SerializeField] private List<IplFile> m_IplFiles = new();
        [SerializeField] private List<DffFile> m_DffFiles = new();

        private Dictionary<int, GameObject> m_LoadedModels = new();
        private List<GameObject> m_IplRootObjects = new();

        private ImgFile m_MainImg;
        private Dictionary<string, DffFile> m_LooseDff =
            new(StringComparer.OrdinalIgnoreCase);
        private Material m_FallbackMaterial;
        private TxdMaterialCache m_TxdMaterialCache;
        private IfpFile m_PedIfpFile;
        private bool m_PreInitIsDone;
        private bool m_IsDone, m_MapLoaded;
        private bool m_IsActuallyInit;
        [SerializeField] private bool m_PrintAnims = false;
        private int m_SpawnedCount;
        private int m_CountToLoad;

        void Awake()
        {
            if(Instance == null)
            {
                Instance = this;
            }
        }

        public GameObject GetModel(int modelIndex)
        {
            if(m_LoadedModels.ContainsKey(modelIndex))
            {
                return m_LoadedModels[modelIndex];
            }

            // Load model into memory for use
            foreach(var car in m_Cars)
            {
                if(car.Id == modelIndex)
                {
                    return MeshSpawn.GetOrCreateTemplate<RenderWareIo.Structs.Ide.Car>(
                        modelIndex,
                        car,
                        m_MainImg,
                        m_FallbackMaterial,
                        m_TxdMaterialCache);
                }
            }
            foreach(var ideObject in m_Peds)
            {
                if(ideObject.Id == modelIndex)
                {
                    return MeshSpawn.GetOrCreateTemplate<Ped>(modelIndex, ideObject, m_MainImg, m_FallbackMaterial, m_TxdMaterialCache);
                }
            }
            foreach(var ideObject in m_Objects)
            {
                if(ideObject.Id == modelIndex)
                {
                    return MeshSpawn.GetOrCreateTemplate<Obj>(modelIndex, ideObject, m_MainImg, m_FallbackMaterial, m_TxdMaterialCache);
                }
            }
            return null;
        }

        public bool TryGetCarDefinition(
            string handlingIdentifier,
            out RenderWareIo.Structs.Ide.Car car)
        {
            for(int i = 0; i < m_Cars.Count; i++)
            {
                RenderWareIo.Structs.Ide.Car candidate = m_Cars[i];
                if(string.Equals(
                    candidate.HandlingId,
                    handlingIdentifier,
                    StringComparison.OrdinalIgnoreCase))
                {
                    car = candidate;
                    return true;
                }
            }

            car = default;
            return false;
        }

        public bool TryGetRandomPedModelIndex(out int modelIndex)
        {
            List<int> candidates = new();

            foreach (RenderWareIo.Structs.Ide.Ped ped in m_Peds)
            {
                if (ped.Id == 0 ||
                    ped.Id >= 26 && ped.Id <= 29 ||
                    string.IsNullOrEmpty(ped.ModelName))
                {
                    continue;
                }

                if (m_MainImg != null && m_MainImg.Contains($"{ped.ModelName}.dff"))
                {
                    candidates.Add(ped.Id);
                }
            }

            if (candidates.Count == 0)
            {
                modelIndex = -1;
                return false;
            }

            modelIndex = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return true;
        }

        public bool PlayPedAnimation(
            GameObject pedModel,
            string preferredAnimationName = "idle_stance",
            float fadeLength = 0.15f,
            WrapMode wrapMode = WrapMode.Loop,
            bool makeInPlace = false)
        {
            if (pedModel == null || m_PedIfpFile?.Ifp?.Animations == null)
            {
                return false;
            }

            IfpAnimation animation = FindPedAnimation(preferredAnimationName);

            if (animation == null)
            {
                Debug.LogWarning($"Could not find a usable ped animation in ped.ifp.");
                return false;
            }

            Animation animationComponent = pedModel.GetComponent<Animation>();

            if (animationComponent == null)
            {
                animationComponent = pedModel.AddComponent<Animation>();
            }

            string clipName = makeInPlace ? $"{animation.Name}_InPlace" : animation.Name;
            AnimationClip clip = animationComponent.GetClip(clipName);

            if (clip == null)
            {
                try
                {
                    clip = IfpAnimationConverter.CreateLegacyClip(
                        animation,
                        pedModel.transform,
                        makeInPlace);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Failed to build ped animation '{animation.Name}': {exception.Message}");
                    return false;
                }

                clip.name = clipName;
                animationComponent.AddClip(clip, clip.name);
            }

            clip.wrapMode = wrapMode;
            animationComponent.clip = clip;
            animationComponent.wrapMode = wrapMode;

            AnimationState state = animationComponent[clip.name];
            state.wrapMode = wrapMode;

            if (animationComponent.IsPlaying(clip.name))
            {
                return true;
            }

            if (fadeLength > 0.0f && animationComponent.isPlaying)
            {
                animationComponent.CrossFade(clip.name, fadeLength);
            }
            else
            {
                animationComponent.Play(clip.name);
            }

            return true;
        }

        public Texture2D GetFrontendTexture(string textureName, string txdFile)
        {
            Debug.Assert(m_TxdMaterialCache != null);

            if(m_TxdMaterialCache.Textures.ContainsKey($"{txdFile}/{textureName}"))
            {
                return m_TxdMaterialCache.Textures[$"{txdFile}/{textureName}"];;
            }

            // Frontend textures
            return m_TxdMaterialCache.LoadTexture(txdFile, textureName, out string _);
        }

        public void PreInit()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            LoadMaterials();
            InitTxdCache();
            RegisterEarlyTxds();
            m_PreInitIsDone = true;
            timer.Stop();
            Debug.Log($"PreInit took {timer.Elapsed.Milliseconds}ms");
        }

        public void Init()
        {
            StartCoroutine(OnInit());
        }

        public IEnumerator OnInit()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while(m_PreInitIsDone == false)
            {
                yield return null;
            }

            if (m_IsDone == true)
            {
                yield break;
            }

            m_IsDone = true; // To prevent multiple loading of this class
            Debug.Log("FileLoader.Init begin");
            LoadImages();
            m_TxdMaterialCache.SetImageFile(m_MainImg, m_FallbackMaterial);
            // TODO: Load GXT
            // TODO: Load Audio
            // TODO: Load Audio
            // TODO: Load data/surface.dat
            // TODO: Load Audio
            // TODO: Load data/pedstats.dat
            // TODO: Load data/timecyc.dat
            HandlingManager.LoadHandlingData(Path.Combine(GameManager.Instance.GtaDirectory, "data", "handling.cfg"));
            LoadPedAnimations();
            MeshSpawn.ClearCache();
            LoadDataFiles();
            ExplosionManager.Init();
            m_IsActuallyInit = true;
            Debug.Log($"OnInit took {timer.Elapsed.Milliseconds}ms");
        }

        private void LoadMaterials()
        {
            m_FallbackMaterial = Resources.Load<Material>("TestMaterial");
        }

        private void LoadImages()
        {
            // This is hardcoded for now as there only ever is the 1 gta3.img file
            m_MainImg = new ImgFile(Path.Combine(GameManager.Instance.GtaDirectory, "models", "gta3.img"));
            //
        }

        private void RegisterEarlyTxds()
        {
            m_TxdMaterialCache.RegisterLooseTxdDirectory(Path.Combine(GameManager.Instance.GtaDirectory, "models"));
            m_TxdMaterialCache.RegisterTxdFile("generic", Path.Combine(GameManager.Instance.GtaDirectory, "models", "generic.txd"));
            m_TxdMaterialCache.RegisterTxdFile("menu", Path.Combine(GameManager.Instance.GtaDirectory, "models", "menu.txd"));
            m_TxdMaterialCache.RegisterTxdFile("particle", Path.Combine(GameManager.Instance.GtaDirectory, "models", "particle.txd"));
        }

        private void InitTxdCache()
        {
            m_TxdMaterialCache = new TxdMaterialCache();
            m_TxdMaterialCache.RegisterLooseTxdDirectory(Path.Combine(GameManager.Instance.GtaDirectory, "txd"));
        }

        private void LoadDataFiles()
        {
            // Load only gta3 and default dat files.
            // The other dat files are different and need their own processing.
            m_DatFiles.Add(new(Path.Combine(GameManager.Instance.GtaDirectory, "data", "default.dat")));
            m_DatFiles.Add(new(Path.Combine(GameManager.Instance.GtaDirectory, "data", "gta3.dat")));
            foreach (RenderWareIo.DatFile dat in m_DatFiles)
            {
                foreach (string ide in dat.Dat.Ides)
                {
                    string path = Path.Combine(GameManager.Instance.GtaDirectory, StringExt.ReplaceInvalidSlash(ide));
                    IdeFile ideFile = new(path);
                    m_Objects.AddRange(ideFile.Ide.Objs);
                    m_Cars.AddRange(ideFile.Ide.Cars);
                    m_TxdMaterialCache.RegisterTxdParents(ideFile.Ide.Txdps);
                    m_Peds.AddRange(ideFile.Ide.Peds);
                }
                foreach (string ipl in dat.Dat.Ipls)
                {
                    string path = Path.Combine(GameManager.Instance.GtaDirectory, StringExt.ReplaceInvalidSlash(ipl));
                    IplFile iplFile = new(path);
                    m_IplFiles.Add(iplFile);
                    m_IplRootObjects.Add(new GameObject(iplFile.IplName));
                    m_CountToLoad += iplFile.Ipl.Insts.Count;
                }
                foreach (string modelFile in dat.Dat.ModelFiles)
                {
                    string path = Path.Combine(GameManager.Instance.GtaDirectory, StringExt.ReplaceInvalidSlash(modelFile));
                    DffFile dffFile = new(path);
                    m_LooseDff.Add(Path.GetFileName(path),dffFile);
                }
            }
            foreach(var car in m_Cars)
            {
                VehicleSpawning.AddVehicle(car);
            }
        }

        public void LoadWorldMap()
        {
            StartCoroutine(OnLoadWorldMap());
        }

        public void StopWorldLoading()
        {
            StopAllCoroutines();
        }

        public IEnumerator OnLoadWorldMap()
        {
            Debug.Log("Loading world map...");
            if(m_IsDone == false)
            {
                Debug.LogError($"Failed to load world map: m_IsDone was false");
                yield break;
            }

            LoadingScreen.Instance.ShowSplashScreen(LoadingScreen.Instance.GetRandomSplashScreen());

            Dictionary<int, IdeObj> objectsById = new();
            Dictionary<string, IdeObj> objectsByModelName = new(StringComparer.OrdinalIgnoreCase);

            foreach (IdeObj obj in m_Objects)
            {
                objectsById[obj.Id] = obj;
                objectsByModelName[obj.ModelName] = obj;
            }

            int instanceCount = 0;
            int missingDefinitionCount = 0;

            //foreach (RenderWareIo.DatFile dat in m_DatFiles)
            {
                foreach (var iplFile in m_IplFiles)
                {
                    GameObject iplRoot = null;
                    foreach(var iplObject in m_IplRootObjects)
                    {
                        if(iplObject.name == iplFile.IplName)
                        {
                            iplRoot = iplObject;
                            break;
                        }
                    }

                    foreach (RenderWareIo.Structs.Ipl.Inst inst in iplFile.Ipl.Insts)
                    {
                        if(inst.ModelName.Contains("LOD", StringComparison.InvariantCultureIgnoreCase))
                        {
                            // Ignore LOD's
                            continue;
                        }

                        instanceCount++;

                        if (!objectsById.TryGetValue(inst.Id, out IdeObj meshObj) &&
                            !objectsByModelName.TryGetValue(inst.ModelName, out meshObj))
                        {
                            missingDefinitionCount++;
                            continue;
                        }

                        Vector3 position = new Vector3(inst.Position.X, inst.Position.Y, inst.Position.Z);
                        Quaternion rotation = new Quaternion(
                            inst.Rotation.X,
                            inst.Rotation.Y,
                            inst.Rotation.Z,
                            inst.Rotation.W);

                        var gameObject = MeshSpawn.SpawnMesh(meshObj, position, rotation, m_MainImg, m_FallbackMaterial, m_TxdMaterialCache);
                        if (gameObject == null)
                        {
                            Debug.LogWarning($"Could not spawn {meshObj.ModelName}");
                            continue;
                        }
                        gameObject.transform.SetParent(iplRoot.transform);
                        m_SpawnedCount++;
                        if(m_SpawnedCount % 32 == 0)
                        {
                            // yield return to prevent the editor/player from freezing
                            yield return null;
                        }
                    }
                }
            }
            OnMapLoaded?.Invoke();

            MeshSpawn.LogStats();
            m_TxdMaterialCache.LogStats();
            m_MapLoaded = true;
        }

        private void LoadPedAnimations()
        {
            string pedIfpPath = Path.Combine(
                GameManager.Instance.GtaDirectory,
                "anim",
                "ped.ifp");

            if (!File.Exists(pedIfpPath))
            {
                Debug.LogWarning($"Could not find ped animation file '{pedIfpPath}'.");
                return;
            }

            try
            {
                m_PedIfpFile = new IfpFile(pedIfpPath);
                Debug.Log($"Loaded {m_PedIfpFile.Ifp.Animations.Count} ped animations from '{pedIfpPath}'.");
                if(m_PrintAnims)
                {
                    string animList = "==== ANIM LIST ====\n";
                    for(int i = 0; i < m_PedIfpFile.Ifp.Animations.Count; i++)
                    {
                        animList += m_PedIfpFile.Ifp.Animations[i].Name + "\n";
                    }
                    Debug.Log(animList);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to load ped animations from '{pedIfpPath}': {exception.Message}");
                m_PedIfpFile = null;
            }
        }

        private IfpAnimation FindPedAnimation(string preferredAnimationName)
        {
            IfpAnimation fallback = null;

            foreach (IfpAnimation animation in m_PedIfpFile.Ifp.Animations)
            {
                if (animation.Objects == null || animation.Objects.Count == 0)
                {
                    continue;
                }

                fallback ??= animation;

                if (!string.IsNullOrEmpty(preferredAnimationName) &&
                    string.Equals(
                        animation.Name,
                        preferredAnimationName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return animation;
                }
            }

            return fallback;
        }

        public void PrintPedAnimations()
        {
            string anims = string.Empty;
            foreach (IfpAnimation animation in m_PedIfpFile.Ifp.Animations)
            {
                if (animation.Objects == null || animation.Objects.Count == 0)
                {
                    continue;
                }

                anims += animation.Name + "\n";
            }
        }
    }
}
