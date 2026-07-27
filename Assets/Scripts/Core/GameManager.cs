using System;
using System.IO;
using GTA3Unity.UI;
using GTA3Unity.Vehicles;
using UnityEngine;
using UnityEngine.Video;

namespace GTA3Unity.Core
{
    public enum EGameState
    {
        Startup,
        Logo,
        Intro,
        Init,
        Frontend
    }

    public sealed class GameManager: MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public string GtaDirectory
        {
            get
            {
                return m_GtaDirectory;
            }
        }

        public bool InGame
        {
            get
            {
                return m_InGame;
            }
        }

        public bool IsInit
        {
            get
            {
                return m_IsInit;
            }
        }

        public bool IsInGame => m_InGame;

        [SerializeField]
        private string m_GtaDirectory;

        [SerializeField] private VideoPlayer m_VideoPlayer;
        [SerializeField] private string m_Logo;
        [SerializeField] private string m_Intro;

        [SerializeField] private EGameState m_GameState;
        [SerializeField] private bool m_IsInit;
        [SerializeField] private bool m_InGame;

        private void Awake()
        {
            if(Instance == null)
            {
                Instance = this;
            }
        }

        public void StartGta(string gtaRoot)
        {
            const string vehicleJson = "vehicles_settings.dat";
            if(VehicleManager.Init(Path.Combine(Application.streamingAssetsPath, vehicleJson)) == false)
            {
                Debug.LogError($"{vehicleJson} does not exist");
                return;
            }

            m_GtaDirectory = gtaRoot;
            if(!Directory.Exists(m_GtaDirectory))
            {
                Debug.LogError($"Cannot access \"{m_GtaDirectory}\": Does not exist");
            }

            Debug.Assert(m_VideoPlayer != null);

            Init(m_GtaDirectory);
        }

        private void OnVideoEnded(VideoPlayer source)
        {
            m_GameState++;
            if(m_GameState > EGameState.Intro)
            {
                return;
            }

            try
            {
                m_VideoPlayer.url = @"file:///" + m_Intro;
                m_VideoPlayer.frame = 0; // Is this neccesary?
                m_VideoPlayer.Play();
            }
            catch(Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void Update()
        {
            switch(m_GameState)
            {
                case EGameState.Startup:
                    break;
                case EGameState.Logo:
                case EGameState.Intro:
                    UpdateVideoInput();
                    break;
                case EGameState.Init:
                    InitUpdate();
                    break;
                case EGameState.Frontend:
                    UpdateFrontend();
                    break;
            }
        }

        private void UpdateFrontend()
        {
            if (MainMenu.Instance == null)
            {
                Debug.LogError($"{nameof(MainMenu)} is no longer available.");
                return;
            }

            if(MainMenu.Instance.MenuState >= EMainMenuState.StartMenu)
            {
                return;
            }
            
            MainMenu.Instance.SetMenuState(EMainMenuState.StartGame);
        }

        private void InitUpdate()
        {
            if (FileLoader.Instance == null)
            {
                Debug.LogError($"{nameof(FileLoader)} is not available.");
                return;
            }

            if (MainMenu.Instance == null)
            {
                Debug.LogError($"{nameof(MainMenu)} is not available.");
                return;
            }

            try
            {
                FileLoader.Instance.PreInit();
                MainMenu.Instance.SetMenuState(EMainMenuState.Startup);
                FileLoader.Instance.Init();
                FileLoader.Instance.LoadWorldMap();
                FileLoader.Instance.OnMapLoaded += OnMapLoaded;
                
                m_GameState = EGameState.Frontend;
            }
            catch(Exception exception)
            {
                Debug.LogException(exception, this);
            }

        }

        private void OnMapLoaded()
        {
            m_IsInit = true;
        }

        private void UpdateVideoInput()
        {
            bool skipRequested =
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.Space);

            if (!skipRequested)
            {
                return;
            }

            m_VideoPlayer.Stop();
            AdvanceVideoSequence();
        }

        private void Init(string gtaRoot)
        {
            if (string.IsNullOrWhiteSpace(gtaRoot))
            {
                Debug.LogError("The GTA III installation directory is empty.");
                return;
            }

            if (!Directory.Exists(gtaRoot))
            {
                Debug.LogError($"Cannot access \"{gtaRoot}\": directory does not exist.");
                return;
            }

            if (m_VideoPlayer == null)
            {
                Debug.LogError("Cannot start because the VideoPlayer is missing.");
                return;
            }

            m_GameState = EGameState.Intro;
            m_Logo = Path.Combine(gtaRoot, "movies", "Logo.mpg").Replace(" ", "%20").Replace("\\", "/");
            m_Intro = Path.Combine(gtaRoot, "movies", "GTAtitles.mpg").Replace(" ", "%20").Replace("\\", "/");

            if(m_VideoPlayer == null)
            {
                m_VideoPlayer = GameObject.FindAnyObjectByType<VideoPlayer>();
                Debug.Assert(m_VideoPlayer != null);
            }

            LoadingScreen.Instance.HideProgressBar();

            m_VideoPlayer.url = @"file:///" + m_Logo;
            m_VideoPlayer.loopPointReached += OnVideoEnded;
            m_VideoPlayer.errorReceived += OnPlaybackError;
            m_VideoPlayer.Play();
        }

        private void OnPlaybackError(VideoPlayer source, string message)
        {
            Debug.LogWarning($"Video playback failed while in state '{m_GameState}': {message}", this);

            AdvanceVideoSequence();
        }

        private void AdvanceVideoSequence()
        {
            switch (m_GameState)
            {
                case EGameState.Logo:
                    m_GameState = EGameState.Intro;
                    PlayVideoOrAdvance(m_Intro);
                    break;

                case EGameState.Intro:
                    m_GameState = EGameState.Init;
                    break;
            }
        }

        private void PlayVideoOrAdvance(string videoPath)
        {
            if (!File.Exists(videoPath))
            {
                Debug.LogWarning($"Startup video does not exist: \"{videoPath}\".", this);

                AdvanceVideoSequence();
                return;
            }

            try
            {
                m_VideoPlayer.Stop();
                m_VideoPlayer.url = new Uri(videoPath).AbsoluteUri;
                m_VideoPlayer.Play();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                AdvanceVideoSequence();
            }
        }

        public void SetInGame(bool inGame)
        {
            m_InGame = inGame;
            PlayerController.Instance.TogglePlayerControllable(true);
            PlayerController.Instance.TeleportPlayer(new Vector3(910.777771f, 14.95467f, -409.139709f));
        }
    }
}