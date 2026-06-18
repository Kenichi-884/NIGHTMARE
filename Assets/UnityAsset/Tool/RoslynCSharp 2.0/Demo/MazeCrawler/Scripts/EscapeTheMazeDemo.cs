using RoslynCSharp.CodeEditor;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace RoslynCSharp.Demo
{
    /// <summary>
    /// Main type for the included example maze crawler game.
    /// Manages the scripting and UI elements of the game.
    /// </summary>
    internal sealed class EscapeTheMazeDemo : MonoBehaviour
    {
        // Private
        private ScriptDomain domain = new ScriptDomain();
        private ScriptProxy activeCrawlerInstance = null;
        private string activeCrawlerSource = null;

        // Public
        /// <summary>
        /// The main code editor input field.
        /// </summary>
        [Header("UI")]
        public CSharpCodeEditor runCrawlerInput;
        /// <summary>
        /// The run code button.
        /// </summary>
        public Button runCrawlerButton;
        /// <summary>
        /// The stop code button.
        /// </summary>
        public Button stopCrawlerButton;
        /// <summary>
        /// The edit code button.
        /// </summary>
        public Button editCodeButton;

        /// <summary>
        /// The code editor window root game object.
        /// </summary>
        [Header("Code Editor")]
        public GameObject codeEditorWindow;
        /// <summary>
        /// The code editor window close button.
        /// </summary>
        public Button codeEditorCloseButton;
        /// <summary>
        /// The code editor button load template button.
        /// </summary>
        public Button codeEditorLoadTemplateButton;
        /// <summary>
        /// The code editor button load solution button.
        /// </summary>
        public Button codeEditorLoadSolutionButton;

        /// <summary>
        /// The maze mouse crawler game object.
        /// </summary>
        [Header("Maze Game")]
        public GameObject mazeMouse;
        /// <summary>
        /// The breadcrumb object that is dropped after every move.
        /// </summary>
        public GameObject breadcrumbPrefab;
        /// <summary>
        /// The speed that the crawler moves around the maze.
        /// </summary>
        public float mouseSpeed = 5f;
        /// <summary>
        /// The code template for an empty script.
        /// </summary>
        [Header("Code Templates")]
        public TextAsset mazeCodeTemplate;
        /// <summary>
        /// The completed code for the maze crawler.
        /// </summary>
        public TextAsset mazeCodeSolution;        
        /// <summary>
        /// When true the maze solution will be loaded instead of the blank code template.
        /// </summary>
        public bool showCompletedCodeOnStartup = false;
        /// <summary>
        /// Used to allow the compiled code to reference <see cref="MazeCrawler"/> type.
        /// </summary>
        [Header("References")]
        public AssemblyReferenceAsset demoReferenceAsset;

        // Methods
        private void Awake()
        {
            runCrawlerButton.onClick.AddListener(RunCrawler);
            stopCrawlerButton.onClick.AddListener(StopCrawler);
            editCodeButton.onClick.AddListener(() => codeEditorWindow.SetActive(true));
            codeEditorCloseButton.onClick.AddListener(() => codeEditorWindow.SetActive(false));
            codeEditorLoadTemplateButton.onClick.AddListener(() => runCrawlerInput.Text = mazeCodeTemplate.text);
            codeEditorLoadSolutionButton.onClick.AddListener(() => runCrawlerInput.Text = mazeCodeSolution.text);
        }

        private void Start()
        {
            // Load startup code
            if (showCompletedCodeOnStartup == true)
            {
                // Load the solution code
                runCrawlerInput.Text = mazeCodeSolution.text;
            }
            else
            {
                // Load the template code
                runCrawlerInput.Text = mazeCodeTemplate.text;
            }
        }

        private void OnValidate()
        {
            // Allow changing the mouse speed in editor while the crawler is running
            if (Application.isPlaying == true && activeCrawlerInstance != null)
                activeCrawlerInstance.Fields["moveSpeed"] = mouseSpeed;
        }

        /// <summary>
        /// Main run method.
        /// This causes any modified code to be recompiled and executed on the mouse crawler.
        /// </summary>
        public void RunCrawler()
        {
            // Kill current attempt
            if(activeCrawlerInstance != null)
            {
                activeCrawlerInstance.Dispose();
                activeCrawlerInstance = null;
            }

            // Get the C# code from the input field
            string cSharpSource = runCrawlerInput.Text;

            // Don't recompile the same code
            if (activeCrawlerSource != cSharpSource || activeCrawlerInstance == null)
            {
                // Remove any other scripts
                StopCrawler();

                // Create options from settings, but add a reference to RoslynCSharp.Demo so that the compiled code can inherit from MazeCrawler
                CompileOptions options = CompileOptions.FromSettings(extraReferences: new[] { demoReferenceAsset });

#if DOTNOW && ENABLE_IL2CPP
                Debug.Log("Using dotnow for execution");

                // Compile the code
                ScriptAssembly asm = domain.CompileAndLoadSourceInterpreted(cSharpSource, out CompileResult compileResult, out _, options);
#else
                Debug.Log("Using Mono/Jit for execution");

                // Check for IL2CPP
                if (ScriptDomain.IsIL2CPPBackend == true)
                    throw new InvalidOperationException("Attempting to run maze crawler demo on IL2CPP backend using Mono-Jit API. You will need to install dotnow and define `DOTNOW` in player settings to support IL2CPP platforms!");

                // Compile the code
                ScriptAssembly asm = domain.CompileAndLoadSource(cSharpSource, out CompileResult compileResult, out _, options);
#endif

                // Check for null
                if (asm == null)
                {
                    // Check for failed to compile
                    if (compileResult.Success == false)
                        throw new Exception("Maze crawler code contains compiler errors. Check console to fix and try again!");

                    // Must 
                    throw new Exception("Maze crawler code contains code security errors. Check console to fix and try again!");
                }

                // Get type
                ScriptType type = asm.FindSubTypeOf<MazeCrawler>();

                // Check for base class
                if (type.IsSubTypeOf<MazeCrawler>() == false)
                    throw new Exception("Maze crawler code must define a single type that inherits from 'RoslynCSharp.Example.MazeCrawler'");


                // Create an instance
                activeCrawlerInstance = type.CreateInstance(mazeMouse);
                activeCrawlerSource = cSharpSource;

                // Set speed value
                activeCrawlerInstance.Fields["breadcrumbPrefab"] = breadcrumbPrefab;
                activeCrawlerInstance.Fields["moveSpeed"] = mouseSpeed;
            }
            else
            {
                // Dispose of the crawler
                activeCrawlerInstance.Dispose();
                activeCrawlerInstance = null;
            }
        }

        /// <summary>
        /// Causes the mouse crawler to stop moving and reset to its initial position.
        /// </summary>
        public void StopCrawler()
        {
            if (activeCrawlerInstance != null)
            {
                // Get the maze crawler instance
                MazeCrawler mazeCrawler = activeCrawlerInstance.GetInstanceAs<MazeCrawler>(false);

                // Call the restart method
                mazeCrawler.Stop();

                // Destroy script
                activeCrawlerInstance.Dispose();
                activeCrawlerInstance = null;
            }
        }
    }
}
