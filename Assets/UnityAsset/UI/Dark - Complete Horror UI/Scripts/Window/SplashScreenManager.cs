using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Michsky.UI.Dark
{
    [DisallowMultipleComponent]
    public class SplashScreenManager : MonoBehaviour
    {
        // Content
        public List<SplashScreenTitle> splashScreenTitles = new List<SplashScreenTitle>();

        // Resources
        public GameObject splashScreen;
        public GameObject modalWindowParent;
        public GameObject mainPanelParent;
        public UIDissolveEffect transitionHelper;
        public MainPanelManager mainPanelManager;

        // Settings
        public bool disableSplashScreen;
        public bool showOnlyOnce = true;
        public bool skipOnAnyKeyPress = false;
        public float disableTimer = 0;
        [Range(0, 5)] public float startDelay = 0.5f;
        public UnityEvent onSplashScreenEnd;

        GameObject currentTitleObj;
        int currentTitleIndex;
        float currentTitleDuration;

        void OnEnable()
        {
            if (showOnlyOnce && GameObject.Find("[Dark UI - Splash Screen Helper]") != null) 
            { 
                disableSplashScreen = true; 
            }

            if (disableSplashScreen)
            {
                splashScreen.SetActive(false);
                modalWindowParent.SetActive(true);

                mainPanelParent.gameObject.SetActive(true);
                transitionHelper.gameObject.SetActive(true);

                mainPanelManager.EnableFirstPanel();

                transitionHelper.location = 0;
                transitionHelper.DissolveOut();

                onSplashScreenEnd.Invoke();
            }

            else
            {
                splashScreen.SetActive(true);
                modalWindowParent.SetActive(false);

                mainPanelParent.gameObject.SetActive(false);
                transitionHelper.gameObject.SetActive(false);

                InitializeTitles();         
            }

            if (showOnlyOnce)
            {
                GameObject tempHelper = new GameObject();
                tempHelper.name = "[Dark UI - Splash Screen Helper]";
                DontDestroyOnLoad(tempHelper);
            }
        }

        void Update()
        {
            if (!skipOnAnyKeyPress)
                return;

            if ((Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.press.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                || (Touchscreen.current != null && Touchscreen.current.press.wasPressedThisFrame))
            {
                skipOnAnyKeyPress = false;
                SkipSplashScreen();
            }
        }

        public void SkipSplashScreen()
        {
            if (!splashScreen.activeInHierarchy)
                return;

            StopCoroutine("DisableSplashScreen");
            StopCoroutine("InitializeTitleDuration");
            StopCoroutine("ProcessStartDelay");

            disableTimer = 0;

            StartCoroutine("DisableSplashScreen");
        }

        public void InitializeTitles()
        {
            if (splashScreenTitles.Count != 0)
            {
                for (int i = 0; i < splashScreenTitles.Count; ++i)
                    disableTimer = disableTimer + splashScreenTitles[i].screenTime;

                foreach (Transform child in splashScreenTitles[0].gameObject.transform.parent)
                    child.gameObject.SetActive(false);

                currentTitleIndex = 0;
                currentTitleDuration = splashScreenTitles[currentTitleIndex].screenTime;
                currentTitleObj = splashScreenTitles[currentTitleIndex].gameObject;

                // startDelay の値に関わらず常にコルーチン経由で開始し、
                // 初回の重いフレーム（シェーダーコンパイル等）を1フレーム吸収する
                StartCoroutine("ProcessStartDelay");
            }
        }

        public void EnableTransition()
        {
            StartCoroutine("DisableSplashScreen");
            StartCoroutine("InitializeTitleDuration");
        }

        IEnumerator ProcessStartDelay()
        {
            // 初回フレームが重い場合でも正確な時間を確保するため1フレーム待機
            yield return null;
            if (startDelay > 0)
                yield return new WaitForSecondsRealtime(startDelay);

            currentTitleObj.SetActive(true);

            StopCoroutine("ProcessStartDelay");
            EnableTransition();
        }

        IEnumerator InitializeTitleDuration()
        {
            yield return new WaitForSecondsRealtime(currentTitleDuration);
           
            currentTitleObj.SetActive(false);
            currentTitleIndex++;
            
            try
            {
                currentTitleDuration = splashScreenTitles[currentTitleIndex].screenTime;
                currentTitleObj = splashScreenTitles[currentTitleIndex].gameObject;
                currentTitleObj.SetActive(true);
                StartCoroutine("InitializeTitleDuration");
            }

            catch 
            {
                StopCoroutine("InitializeTitleDuration");
            }
        }

        IEnumerator DisableSplashScreen()
        {
            yield return new WaitForSecondsRealtime(disableTimer);

            splashScreen.SetActive(false);
            modalWindowParent.SetActive(true);

            mainPanelParent.gameObject.SetActive(true);
            transitionHelper.gameObject.SetActive(true);

            mainPanelManager.EnableFirstPanel();

            transitionHelper.location = 0;
            transitionHelper.DissolveOut();

            onSplashScreenEnd.Invoke();

            StopCoroutine("StartTransition");
        }
    }
}