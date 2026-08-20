using UnityEngine;
using UnityEngine.UI;
using GTA3Unity.Core;
using System.Collections;
using UnityEngine.SceneManagement;
using System;

namespace GTA3Unity.UI
{
    public enum EMainMenuState
    {
        Startup,
        StartMenu,
        StartGame,
        LoadGame,
        DeleteGame,
        OptionsMenu,
        ControllerSetup,
        AudioSetup,
        DisplaySetup,
        GraphicsSetup,
        LanguageSetup,
        PlayerSetup,
        Back,
        QuitGame,
        LoadingScreen
    }

    public sealed class MainMenu: MonoBehaviour
    {
        public static MainMenu Instance { get; private set; }

        public EMainMenuState MenuState
        {
            get
            {
                return m_MenuState;
            }
        }

        [Header("Menu Objects")]
        [SerializeField] private GameObject m_StartMenu;
        [SerializeField] private GameObject m_StartGame;

        [Header("Prefabs")]
        [SerializeField] private Button m_MenuButtonPrefab;

        private EMainMenuState m_MenuState;

        private void Awake()
        {
            if(Instance == null)
            {
                Instance = this;
            }
        }

        public void SetMenuState(EMainMenuState mainMenuState)
        {
            Debug.Assert(m_StartGame != null);
            Debug.Assert(m_StartMenu != null);

            Debug.Log($"Set menu state to {mainMenuState}");
            m_MenuState = mainMenuState;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            switch(mainMenuState)
            {
                case EMainMenuState.Startup:
                    LoadingScreen.Instance.ShowSplashScreen("mainsc1");
                    LoadingScreen.Instance.SetSplashText("FED_LDW");
                    break;
                case EMainMenuState.StartMenu:
                    LoadingScreen.Instance.ShowSplashScreen("mainmenu24", "menu");
                    LoadingScreen.Instance.SetSplashText("FEM_MM");
                    m_StartMenu.gameObject.SetActive(true);
                    break;
                case EMainMenuState.StartGame:
                    LoadingScreen.Instance.ShowSplashScreen("singleplayer24", "menu");
                    LoadingScreen.Instance.SetSplashText("FET_SGA");
                    m_StartMenu.gameObject.SetActive(false);
                    m_StartGame.gameObject.SetActive(true);
                    break;
            }
        }

        public void OnClickStartButton()
        {
            SetMenuState(EMainMenuState.StartGame);
        }

        public void OnStartDebugGame()
        {
            m_StartGame.gameObject.SetActive(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            FileLoader.Instance.StopWorldLoading();
            var asyncOp = SceneManager.LoadSceneAsync("TestScene", LoadSceneMode.Additive);
            asyncOp.completed += OnDebugMapLoaded;
        }

        private void OnDebugMapLoaded(AsyncOperation operation)
        {
            OnMapLoaded();
            PlayerController.Instance.TeleportPlayer(new Vector3(910.777771f, 101, -409.139709f));
        }

        public void OnClickStartNewGame()
        {
            Debug.Assert(FileLoader.Instance != null);
            m_StartGame.gameObject.SetActive(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            LoadingScreen.Instance.HideSplashText();
            if(GameManager.Instance.IsInit)
            {
                OnMapLoaded();
            }
            else
            {
                LoadingScreen.Instance.ShowSplashScreen(LoadingScreen.Instance.GetRandomSplashScreen());
                LoadingScreen.Instance.ShowProgressBar(FileLoader.Instance.SpawnedCount, FileLoader.Instance.CountToLoad);
                SetMenuState(EMainMenuState.LoadingScreen);
                FileLoader.Instance.OnMapLoaded += OnMapLoaded;
            }
        }

        private void OnMapLoaded()
        {
            GameManager.Instance.SetInGame(true);
            LoadingScreen.Instance.HideProgressBar();
            LoadingScreen.Instance.HideSplashScreen();
        }

        private void LateUpdate()
        {
            if(m_MenuState != EMainMenuState.LoadingScreen)
            {
                return;
            }

            if(FileLoader.Instance.SpawnedCount % 1000 == 0)
            {
                // Occasionally change the loading screen
                LoadingScreen.Instance.ShowSplashScreen(LoadingScreen.Instance.GetRandomSplashScreen());
            }
            LoadingScreen.Instance.ShowProgressBar(FileLoader.Instance.SpawnedCount, FileLoader.Instance.CountToLoad);
        }

        public void OnClickOptionsButton()
        {
            SetMenuState(EMainMenuState.OptionsMenu);
        }

        public void OnClickQuitButton()
        {
            SetMenuState(EMainMenuState.QuitGame);
        }

        // User flow
        // Startup(mainsc1): Load some content

        // Start Menu (mainmenu24): "Start Game", "Options", "Quit Game"

        // Start Game (singleplayer24): "Start New Game", "Load Game", "Delete Game", "Back"
        // Start New Game & Load Save: (mainsc1 -> loadscX)

        // Load Game (singleplayer24): "Cancel", "Slot 1-8 is free" or 'MISSION NAME\tDD Month YYYY HH:MM:SS'

        // Delete Game (singleplayer24 with a red tint): "Cancel", "Slot 1-8 is free" or 'MISSION NAME\tDD Month YYYY HH:MM:SS'

        // Options Menu (playersetup24): "Controller Setup", "Audio Setup", "Display Setup", "Graphics Setup", "Language Setup", "Player Setup", "Back"
        // All Options submenus except Player Setup: (findgame24), player setup: (playersetup24)

        // Quit Game (singleplayer24): "No", "Yes"

    }
}