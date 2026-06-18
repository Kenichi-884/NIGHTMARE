using RoslynCSharp.CodeEditor;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoslynCSharp.Demo
{
    public sealed class Playground : MonoBehaviour
    {
        // Private
        private ScriptDomain domain = new ScriptDomain();
        private Material skyboxMaterial = null;
        private Scene playgroundScene = default;

        [SerializeField]
        private CodeEditorPanel codeEditorPanel;
        [SerializeField]
        private ConsolePanel consolePanel;
        [SerializeField]
        private CSharpCodeEditor cSharpEditor;

        [SerializeField]
        private Button showCodeEditorButton;
        [SerializeField]
        private Button showConsoleButton;

        [SerializeField]
        private Button newScriptButton;

        // Public
        public TextAsset newScriptTemplate;

        // Methods
        private void Awake()
        {
            // Add listeners
            codeEditorPanel.OnVisibilityChanged.AddListener(OnCodeEditorVisibilityChanged);
            codeEditorPanel.OnRunCode.AddListener(RunCode);
            consolePanel.OnVisibilityChanged.AddListener(OnConsoleVisibilityChanged);

            showCodeEditorButton.onClick.AddListener(codeEditorPanel.Show);
            showConsoleButton.onClick.AddListener(consolePanel.Show);

            // Trigger visibility update
            OnCodeEditorVisibilityChanged();
            OnConsoleVisibilityChanged();


            newScriptButton?.onClick.AddListener(() => LoadScript(newScriptTemplate));


            // Handle incoming log messages
            Application.logMessageReceived += (string condition, string stackTrace, LogType type) =>
            {
                switch(type)
                {
                    case LogType.Assert:
                    case LogType.Exception:
                    case LogType.Error: consolePanel.LogError(condition); break;
                    case LogType.Warning: consolePanel.LogWarning(condition); break;
                    case LogType.Log: consolePanel.LogMessage(condition); break;
                }
            };
        }

        private void Start()
        {
            // Get skybox
            skyboxMaterial = RenderSettings.skybox;

            // Check for loaded
            string loadedCode = PlayerPrefs.GetString("RoslynCSharp.Demo.UserCode", null);

            if (string.IsNullOrEmpty(loadedCode) == false)
            {
                cSharpEditor.Text = loadedCode;
            }
            else
            {
                // Load the default script
                LoadScript(newScriptTemplate);
            }
        }

        public void RunCode()
        {
            // Clear console before load
            consolePanel.Clear();

            // Get the text
            string cSharpSource = cSharpEditor.Text;

            // Save the code
            PlayerPrefs.SetString("RoslynCSharp.Demo.UserCode", cSharpSource);

            // ### Scene Reset - So we can create objects from code and start fresh for each run by unloading the temp scene
            {
                // Unload playground scene if valid
                if (playgroundScene.IsValid() == true)
                    SceneManager.UnloadSceneAsync(playgroundScene);

                // Load the new scene
                playgroundScene = SceneManager.CreateScene("Playground-" + Guid.NewGuid().ToString());

                // Make active
                SceneManager.SetActiveScene(playgroundScene);

                // Update skybox
                RenderSettings.skybox = skyboxMaterial;
            }


            // Create options
            CompileOptions options = CompileOptions.FromSettings();

            // Don't report to console by default - we wil report them manually below
            options.CompilerLogLevel = LogLevel.None;

            // Compile and load
            CompileResult compileResult;

#if DOTNOW && ENABLE_IL2CPP
            ScriptType mainComponentType = domain.CompileAndLoadMainSourceInterpreted(cSharpSource, out compileResult, out _, options, ScriptSecurityMode.EnsureLoad);
#else
            // Check for IL2CPP
            if (ScriptDomain.IsIL2CPPBackend == true)
                throw new InvalidOperationException("Attempting to run playground demo on IL2CPP backend using Mono-Jit API. You will need to install dotnow and define `DOTNOW` in player settings to support IL2CPP platforms!");

            ScriptType mainComponentType = domain.CompileAndLoadMainSource(cSharpSource, out compileResult, out _, options, ScriptSecurityMode.EnsureLoad);
#endif

            // Check compile result
            if(compileResult.Success == false)
            {
                // Report all errors
                foreach(CompileError error in compileResult.Errors)
                {
                    // Only log warnings and errors
                    if(error.IsError == true)
                    {
                        consolePanel.LogError(error.ToString());
                    }
                    else if(error.IsWarning == true)
                    {
                        consolePanel.LogWarning(error.ToString());
                    }
                }
            }
            else
            {
                consolePanel.LogMessage("Code compiled successfully! Running code...");

                // Check for main type
                if(mainComponentType == null || mainComponentType.IsMonoBehaviour == false)
                {
                    consolePanel.LogWarning("Code compiled successfully but no MonoBehaviour type was found! Create a behaviour script to run code or nothing will happen in the scene");
                }
                else
                {
                    // Hide the editor editor - so we can see the results in the scene
                    codeEditorPanel.Hide();

                    // Create a new game object to hold
                    GameObject container = new GameObject("Container");

                    // Create a new component instance
                    mainComponentType.CreateInstance(container);
                }
            }
        }

        public void LoadScript(TextAsset script)
        {
            if(script != null)
                cSharpEditor.Text = script.text;
        }

        private void OnCodeEditorVisibilityChanged()
        {
            showCodeEditorButton.gameObject.SetActive(codeEditorPanel.IsShown == false);
        }

        private void OnConsoleVisibilityChanged()
        {
            showConsoleButton.gameObject.SetActive(consolePanel.IsShown == false);
        }
    }
}
