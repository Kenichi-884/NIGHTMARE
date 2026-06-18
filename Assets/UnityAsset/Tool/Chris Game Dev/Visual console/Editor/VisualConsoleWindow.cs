#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine.UIElements.InputSystem;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.UIElements;

namespace chrisGameDev.VisualConsole
{
    internal class VisualConsoleWindow : EditorWindow
    {
        private Label label;
        private ScrollView stackTrace_scrollView;
        private ScrollView logRows_scrollView;
        private VisualElement parentContainer;

        private Button clearDebugButton;
        private Button clearButton;
        private Button testNormalLogButton;
        private Button testNestedLogButton;
        private Button testNestedLogEndButton;
        private IntegerField testIntInput;

        /// <summary>
        /// List that stores all active nesting logs.
        /// </summary>
        private List<logData> activeNestingLogsDataList;
        private List<VisualElement> logRowsList;
        

        /// <summary>
        /// Stores the last and farthest log button created.
        /// </summary>
        private Button farthestLogButton = null;

        private bool cancelVisualLogs = false;
        //private bool cancelQueuingLogs = false;

        /// <summary>
        /// Indicates that the next log to create is the first nested log, right after a nesting log was created.
        /// </summary>
        private bool setupFirstNestedLog = false;

        private List<logData> logQueue;
        private object queueLock = new object(); // For thread safety

        private ObjectField debugTextField;
        //[SerializeField] 
        //private TMPro.TextMeshProUGUI debugTextComponent;// For DEBUGGING.

        private int buttonClickCount = 0;
        private bool isEditorFocused = true;
        private bool editorInPlayState = true;
        //private bool editorUpdateAllowed = true;

        private Button selectedLogButton;

        [MenuItem("Tools/Chris Game Dev/Visual Console")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow<VisualConsoleWindow>("Visual Console");

        }

        private void Init()
        {
            //Debug.Log("VISUAL CONSOLE window Init()");
            cancelVisualLogs = false;

            Application.logMessageReceivedThreaded += HandleLog;
            EditorApplication.playModeStateChanged += playModeStateChanged;
            EditorApplication.focusChanged += HandleFocusChange;
            //EditorApplication.update += UpdateEditor;
            EditorApplication.pauseStateChanged += HandlePauseStateChange;

            activeNestingLogsDataList = new List<logData>();
            logRowsList = new List<VisualElement>();
            logQueue = new List<logData>();

            // Import UXML
            //VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Chris Game Dev/Visual console/uxml/consoleVisualTree.uxml");
            //VisualElement labelFromUXML = visualTree.Instantiate();
            //rootVisualElement.Add(labelFromUXML);
            //
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Chris Game Dev/Visual console/uxml/consoleVisualTree.uxml");
            visualTree.CloneTree(rootVisualElement);


            // Import USS:
            StyleSheet uss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Chris Game Dev/Visual console/uss/consoleStyles.uss");
            rootVisualElement.styleSheets.Add(uss);

            // Load buttons from UI Document (UXML):
            clearDebugButton = rootVisualElement.Q<Button>("ClearButtonDebug");
            clearDebugButton.RegisterCallback<ClickEvent>(clearButton_Click);
            clearButton = rootVisualElement.Q<Button>("ClearButton");
            clearButton.RegisterCallback<ClickEvent>(clearButton_Click);

            // Test Buttons:
            testNormalLogButton = rootVisualElement.Q<Button>("TestNormalLogButton");
            testNormalLogButton.RegisterCallback<ClickEvent>(testNormalButton_Click);
            // nested:
            testNestedLogButton = rootVisualElement.Q<Button>("TestNestedLogButton");
            testNestedLogButton.RegisterCallback<ClickEvent>(testNestingButton_Click);
            // End nested:
            testNestedLogEndButton = rootVisualElement.Q<Button>("TestNestedEndLogButton");
            testNestedLogEndButton.RegisterCallback<ClickEvent>(testNestedEndButton_Click);
            // input:
            testIntInput = rootVisualElement.Q<IntegerField>("TestIntField");
            testIntInput.RegisterCallback<ChangeEvent<int>>(testIntInputChanged);

            parentContainer = rootVisualElement.Q<VisualElement>("parentContainer");
            // store containers.
            logRows_scrollView = rootVisualElement.Q<ScrollView>("LogRowsScrollView");
            stackTrace_scrollView = rootVisualElement.Q<ScrollView>("StackTraceScrollview");
            //Debug.Log("Scroll view: "+ stackTrace_scrollView);

            debugTextField = rootVisualElement.Q<ObjectField>("DebugTextObject");
            debugTextField.RegisterValueChangedCallback( _event => { 
                //debugTextComponent = _event.newValue as TMPro.TextMeshProUGUI;
            });
                       
            clearButton_Click(null);
            config_stackTraceResizeHandle();

        }

        private void HandleFocusChange(bool focus)
        {
            isEditorFocused = focus;
        }

        private void HandlePauseStateChange(PauseState modeState)
        {
            switch(modeState)
            {
                case PauseState.Paused:
                    editorInPlayState = false;
                    break;

                case PauseState.Unpaused:
                    editorInPlayState = true;
                    break;
            }
        }

        private void clearButton_Click(ClickEvent _clickEvent)
        {
            
            //UnityEngine.Debug.Log("↕ Clear Button clicked.");
            logRows_scrollView.Clear();
            logRowsList.Clear();
            stackTrace_scrollView.Clear();
            activeNestingLogsDataList.Clear();

            //newNestedLogPosition = 0f;
            //nestingLogsCounter = 0;
            createRow();
            stackTrace_scrollView.Add(CreateStackTracerHeightChanger());
            
        }

        private void testNormalButton_Click(ClickEvent _clickEvent)
        {
            //Debug.Log("☻ TEST BUTTON clicked.");
            UnityEngine.Debug.Log("○ Normal Log.");

        }

        private void testNestingButton_Click(ClickEvent _clickEvent)
        {
            //Debug.Log("☻ TEST BUTTON clicked.");
            int nestingQuantity = activeNestingLogsDataList.Count;
            string newID = "test" + nestingQuantity;
            VisualDebug.NestingLog("↕ NESTING TEST LOG ► ID: "+ newID, newID);

        }

        private void testNestedEndButton_Click(ClickEvent _clickEvent)
        {
            VisualDebug.NestingLogEnd("test"+testIntInput.value);
        }

        private void testIntInputChanged(ChangeEvent<int> _inputEvent)
        {
            testIntInput.value = _inputEvent.newValue; ;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            //if (cancelQueuingLogs)
            //{
            //    cancelQueuingLogs = false;
            //    return;
            //}

            logData newLogData = null;
            if (VisualDebug.getNewestNormalLogData() != null)
            {
                newLogData = VisualDebug.getNewestNormalLogData();
                VisualDebug.resetNewestNormalLogData();
                
            }
            else if (VisualDebug.getNewNestingLogData() != null) 
            {
                newLogData = VisualDebug.getNewNestingLogData() as logData;
                VisualDebug.resetNewNestingLogData();
            }
            else newLogData = new logData();
            

            newLogData.logString = logString;
            newLogData.stackTrace = stackTrace;
            newLogData.type = type;
            logQueue.Add(newLogData);

            switch(newLogData.type)
            {
                case LogType.Exception:
                    internalDebugCanvas("EXCEPTION");
                    break;
            }


            internalDebugCanvas(".... HandleLog .... Enqueued log Last...(" + logQueue.Count + "): " + logQueue.LastOrDefault().logString);
            internalDebugCanvas("Is Visual Log: "+ logQueue.LastOrDefault().customVisualLog + "| Nesting id: " + logQueue.LastOrDefault().Nesting_id);
            
        }

        /// <summary>
        /// Stores the new log into each of all active nesting logs, so that each nesting log can hide/fold its nested logs.
        /// </summary>
        /// <param name="_targetLogData"></param>
        private void storeNestedLog(logData _targetLogData)
        {
            foreach(logData nestingLogdata in activeNestingLogsDataList)
            {
                if(nestingLogdata == _targetLogData) continue;
                // only store the log if its on the row just below the nesting log.
                if(logRows_scrollView.IndexOf(_targetLogData.button.parent) != (logRows_scrollView.IndexOf(nestingLogdata.button.parent) + 1)) continue;
                nestingLogdata.nestedLogs.Add(_targetLogData);
            }
        }

        private void setupQueuedLog(logData _LogData)
        {

            //internalDebugCanvas("VisualConsole setupNewLog: " + logString + " | " + type);//+ " | " + stackTrace);
            if(_LogData.nesting_ender)
            {
                UpdateNestingLogs_ActiveState(_LogData);
                return;
            }
            
            //Debug.Log("cancelVisualLogs: " + cancelVisualLogs);
            if (cancelVisualLogs) return;
             
            // Decide what type of log to create, normal or nesting group.
            if (_LogData.Nesting_id != "")
            {
                /////////////////
                // NESTING LOG //
                /////////////////
                #region Nesting Log
                // if the log id already exist, do not create a new log.
                if (activeNestingLogsDataList.Find(oldLog => oldLog.Nesting_id.Equals(_LogData.Nesting_id)) != null)
                {
                    UnityEngine.Debug.LogWarning("Visual Debug: Nesting log ID is already active, try a different ID.");
                    return;
                }

                // create new visual log button:
                Button nestingLogButton = CreateLogButton(_LogData);
                _LogData.button = nestingLogButton;
                _LogData.rowIndex = activeNestingLogsDataList.Count;
                positionLog(nestingLogButton);

                // store the new active nesting log as active AFTER it was positioned so it becomes active for setting nested positioning of the following logs including other nesting logs.
                activeNestingLogsDataList.Add(_LogData);
                // if there is no row below the latest active nesting log, create a new row.
                if (getRowBelowLatestActiveNestingLog() == null) createRow();

                //internatDebugText("Created nesting log: " + logString);
                farthestLogButton = nestingLogButton;
                setupFirstNestedLog = true;

                
                
                #endregion
            }
            else
            {
                ////////////////
                // NORMAL LOG //
                ////////////////
                #region Normal Log
                // create normal log button and place it in the right row.
                Button newNormalLogButton = CreateLogButton(_LogData);
                _LogData.button = newNormalLogButton;
                positionLog(newNormalLogButton);
                farthestLogButton = newNormalLogButton;
                farthestLogButton.MarkDirtyRepaint();
                //newNormalLogButton.RegisterCallback<GeometryChangedEvent>((GeometryChangedEvent evt) =>{
                //    updateNestingLogs_Width();
                //    //logRows_scrollView.horizontalScroller.value = logRows_scrollView.horizontalScroller.highValue;
                //});
                //updateNestingLogs_Width();
                //logRows_scrollView.MarkDirtyRepaint();
                //newNormalLogButton.schedule.Execute(() =>
                //{
                //    updateNestingLogs_Width();
                //    logRows_scrollView.horizontalScroller.value = logRows_scrollView.horizontalScroller.highValue;
                //}).ExecuteLater(0); // Force immediate execution

                //internatDebugText("Created normal log: "+ logString);
                #endregion
            }
            updateNestingLogs_Width();
            storeNestedLog(_LogData);
        }


        public void Update()
        {
            if (editorInPlayState) ProcessLogQueue();
            //ProcessLogQueue();
        }

        /// <summary>
        /// Used for updates in EditorApplication.update +=
        /// </summary>
        //private void UpdateEditor()
        //{

        //internalDebugCanvas("▀▀▀ EditorUpdate ▀▀▀ " + Time.frameCount);
        //internalDebugCanvas("editorInPlayState: " + editorInPlayState);
        //if (editorInPlayState) ProcessLogQueue();
        //if (editorUpdateAllowed)
        //{

        //    ProcessLogQueue();
        //    editorUpdateAllowed = false;
        //    updateAllowerSetter();
        //}     
        //Repaint();
        //UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        //}

        //private async void updateAllowerSetter()
        //{
        //    editorUpdateAllowed = false;
        //    await Awaitable.NextFrameAsync();
        //    editorUpdateAllowed = true;
        //}

        private void ProcessLogQueue()
        {
            //internalDebugCanvas("-1 ProcessLogQueue(): " + logQueue.Count);
            if (logQueue.Count == 0) return;
            //if (!isEditorFocused) return;
            logData logEntry;
            internalDebugCanvas("---------------2 ProcessLogQueue(): " + logQueue.Count);
            
            logEntry = logQueue.ElementAt(0);
            logQueue.Remove(logEntry);

            //for(int x = logQueue.Count-1; x >= 0 ; x--)
            //{
            //    logEntry = logQueue[x];
            //    setupQueuedLog(logEntry);
            //    logQueue.Remove(logEntry);
            //}

            //internalDebugCanvas("-Dequeue: " + logEntry.logString);
            // Process the log entry (e.g., add to UI)
            setupQueuedLog(logEntry);
            internalDebugCanvas("☻☻☻☻☻☻☻☻☻☻☻☻☻ END of processing queue - Frame: "+ Time.frameCount);
        }


        private void createRow()
        {
            if(cancelVisualLogs) return;
            VisualElement newRow = createLogRow();
            
        }

        /// <summary>
        /// Updates the width of the nesting logs right.
        /// Should be called right after a new log button is created.
        /// </summary>
        private void updateNestingLogs_Width()
        {
            //await Awaitable.NextFrameAsync();
            //await Awaitable.WaitForSecondsAsync(0.5f);
            farthestLogButton.RegisterCallbackOnce<GeometryChangedEvent>((GeometryChangedEvent evt) => {
                //internalDebugCanvas("♦♦♦♦♦♦ Updating NESTING LOGS width ♦♦♦♦♦♦ Frame: " + Time.frameCount);
                //farthestLogButton.SetEnabled(false);
                foreach (logData nestingLogdata in activeNestingLogsDataList)
                {
                    // Update the nested log button to the width of the latest normal logs:
                    // cycle all log buttons to get the farthest right position and set the nested log to that.
                    if (nestingLogdata.active)
                    {
                        //internalDebugCanvas("► Updating width: " + nestingLogdata.Nesting_id + " | text: " + nestingLogdata.logString);

                        // Calculate the new width for the top button
                        var bottomButtonBounds = farthestLogButton.worldBound;
                        var topButtonBounds = nestingLogdata.button.worldBound;
                        //internalDebugCanvas("bottomButtonBounds.xMax - " + bottomButtonBounds.xMax);
                        //internalDebugCanvas("topButtonBounds.xMin - " + topButtonBounds.xMin);
                        // Calculate the new width for the top button
                        float bottomButtonRight = bottomButtonBounds.xMax; // Right edge of the bottom button
                        float topButtonLeft = topButtonBounds.xMin; // Left edge of the top button
                        float newWidth = bottomButtonRight - topButtonLeft; // Width needed for alignment

                        //Debug.Log("Old width: " + nestingLogdata.button.style.width);
                        //Debug.Log("New width: " + newWidth);
                        nestingLogdata.button.style.width = newWidth;
                        nestingLogdata.finalWidth = newWidth;
                    }
                }
                logRows_scrollView.horizontalScroller.highValue = farthestLogButton.layout.position.x+ farthestLogButton.layout.width;//farthestLogButton.worldBound.xMax - logRows_scrollView.contentViewport.layout.width;
                logRows_scrollView.horizontalScroller.value = logRows_scrollView.horizontalScroller.highValue;
            });
            
        }

        /// <summary>
        /// Updates the active state of the nesting logs.
        /// Before removing the nesting log, it should wait until its right moment or turn in the queue.
        /// </summary>
        public void UpdateNestingLogs_ActiveState(logData _LogData)
        {
            
            if (_LogData.nesting_ender)
            {
                internalDebugCanvas("NESTING ENDER ACTIVATED - "+ _LogData.Nesting_id);
                foreach (logData nestingLogdata in activeNestingLogsDataList)
                {
                    if (nestingLogdata.Nesting_id == _LogData.Nesting_id)
                    {
                        nestingLogdata.active = false;
                    }
                }
                // remove non active nested logs from the list of active nesting logs.
                activeNestingLogsDataList.RemoveAll(nestingLogData => !nestingLogData.active);
            }

        }


        public void OnEnable()
        {
            //Debug.Log("VISUAL CONSOLE window enabled()");
            Init();
           
        }


        public void OnDisable()
        {
            //Debug.Log("VISUAL CONSOLE window ONDISABLED()");

            Application.logMessageReceivedThreaded -= HandleLog;
            EditorApplication.playModeStateChanged -= playModeStateChanged;
            EditorApplication.focusChanged -= HandleFocusChange;
            //EditorApplication.update -= UpdateEditor;
            EditorApplication.pauseStateChanged -= HandlePauseStateChange;


            VisualDebug.resetNewestNormalLogData();
            VisualDebug.resetNewNestingLogData();

            //Debug.Log("VISUAL CONSOLE window OnDestroy()");
        }

        private void setNestedLogsDisplay(INestingLogData nestingLogData, DisplayStyle display)
        {
            // the first nested log of the row.
            VisualElement firstNestedLogInRow = null;
            // the last nested log of the row.
            VisualElement lastNestedLogInRow = null;
            VisualElement newSpacer = null;
            VisualElement targetRow = null;
            // the current row beign evaluated.
            //int currentRowIndex = -1;
            // the target row of the spacer.
            int nestedLogsRowIndex = 1;
            // cycle all nested logs, they are not stored per row, they are stored in order of appearance in the scrollview.
            foreach (logData nestedLogdata in nestingLogData.nestedLogs)
            {
                if (nestedLogdata.button == null) continue;
                nestedLogdata.button.style.display = display;
                

                // create a spacer for all the hidden nested logs of the same row.
                // figure out if this is the last nested log of the row.
                // only create the spacer if the nested logs are getting hidden.
                if (display == DisplayStyle.None)
                {
                    internalDebugCanvas("HIDING NESTED LOGs.");
                    nestedLogsRowIndex = logRows_scrollView.IndexOf(nestedLogdata.button.parent);
                    // find the first and last nested logs, they should be in the same row. (nestedLogdata stores the nested logs of the same row).
                    if (nestingLogData.nestedLogs.Count == 1)
                    {
                        firstNestedLogInRow = nestedLogdata.button;
                        lastNestedLogInRow = nestedLogdata.button;
                    }
                    else if (nestedLogdata.Equals(nestingLogData.nestedLogs.ElementAt(0)))
                    {
                        firstNestedLogInRow = nestedLogdata.button;
                    }
                    else if (nestedLogdata.Equals(nestingLogData.nestedLogs.ElementAt(nestingLogData.nestedLogs.Count - 1)))
                    {
                        lastNestedLogInRow = nestedLogdata.button;
                    }

                    // create the spacer when reaching the last log of the current row.
                    if (nestingLogData.nestedLogs.IndexOf(nestedLogdata) == (nestingLogData.nestedLogs.Count - 1))
                    {
                        // Create a spacer to fill the space of the nested logs.
                        if (nestingLogData.folded_spacer_nestedLogs == null)
                        {
                            targetRow = logRows_scrollView.ElementAt(nestedLogsRowIndex);
                            newSpacer = createSpacerLog(lastNestedLogInRow.worldBound.xMax, firstNestedLogInRow.worldBound.xMin);
                            targetRow.Insert(firstNestedLogInRow.parent.IndexOf(firstNestedLogInRow), newSpacer);
                            nestingLogData.folded_spacer_nestedLogs = newSpacer;

                            internalDebugCanvas("Spacer created.");
                        }
                        else nestingLogData.folded_spacer_nestedLogs.style.display = DisplayStyle.Flex;
                        
                    }
                    
                }
                else
                {
                    if (nestingLogData.folded_spacer_nestedLogs != null) nestingLogData.folded_spacer_nestedLogs.style.display = DisplayStyle.None;
                }
                
                // continue with the next nested logs inside the nested log.
                // but only if the nested log is not folded.
                if (nestedLogdata.nestedLogs != null) if (!nestedLogdata.isFolded) setNestedLogsDisplay(nestedLogdata, display);

                // if the nested log is also folded or not.
                if (nestedLogdata.isFolded)
                {
                    setFoldingIcon(nestedLogdata, display);
                }
            }
        }

        private void setFoldingIcon(INestingLogData nestingLogData, DisplayStyle display)
        {
            if(nestingLogData.folded_icon == null)
            {
                nestingLogData.folded_icon = new VisualElement();
                nestingLogData.button.parent.Add(nestingLogData.folded_icon);
                nestingLogData.folded_icon.AddToClassList("FoldIcon");
                nestingLogData.folded_icon.style.backgroundImage = EditorGUIUtility.FindTexture("d_PrefabOverlayAdded Icon");//("PrefabOverlayAdded On Icon");
                nestingLogData.folded_icon.style.marginLeft =
                    (nestingLogData.button.worldBound.xMin - nestingLogData.button.parent.worldBound.xMin);// - (nestingLogData.folded_icon.style.backgroundSize.value.x.value);
            }
            else
            {
                nestingLogData.folded_icon.style.display = display;

            }
        }

        private void logButton_doubleClick(logData clickedLogButton)
        {
            buttonClickCount++;

            if(buttonClickCount == 2)
            {
                internalDebugCanvas("Button double click. " + clickedLogButton.button.text);
                buttonClickCount = 0;
                foldNestedLogs(clickedLogButton);
            }

            logButton_doubleClick_reset();
        }

        /// <summary>
        /// Hide all nested logs.
        /// </summary>
        /// <param name="clickedLogButton"></param>
        private void foldNestedLogs(logData clickedLogButton)
        {
            clickedLogButton.isFolded = !clickedLogButton.isFolded;
            // hide or show all nested logs.
            if (clickedLogButton.isFolded)
            {
                
                // shrink nesting log.
                clickedLogButton.button.style.opacity = 0.2f;
                setFoldingIcon(clickedLogButton, DisplayStyle.Flex);
                internalDebugCanvas("Nested count: " + clickedLogButton.nestedLogs.Count);
                
                // hide all nested logs.
                setNestedLogsDisplay(clickedLogButton, DisplayStyle.None);
            }
            else
            {
                clickedLogButton.button.style.opacity = 1f;
                // restore previous nesting log width.                
                setFoldingIcon(clickedLogButton, DisplayStyle.None);
                // hide the spacer created when hiding the nesting log.
                if (clickedLogButton.folded_spacer_width != null) clickedLogButton.folded_spacer_width.style.display = DisplayStyle.None;


                internalDebugCanvas("Nested count: " + clickedLogButton.nestedLogs.Count);
                // show all nested logs.
                setNestedLogsDisplay(clickedLogButton, DisplayStyle.Flex);
            }
        }

        private async void logButton_doubleClick_reset()
        {
            await Awaitable.WaitForSecondsAsync(0.2f);
            buttonClickCount = 0;
        }

        private Button CreateLogButton(logData _LogData)
        {

            // Create the new button.
            Button newLogButton = new Button(() =>
            {
                //UnityEngine.Debug.Log($"Button clicked for log: {log_message}");
                stackTrace_scrollView.Clear();
                stackTrace_scrollView.Add(CreateStackTracerHeightChanger());
                DisplayStackTrace(_LogData.logString + " \n " + _LogData.stackTrace);

                //Debug.Log("localIsCustomLog: " + localIsCustomLog);
                //Debug.Log("isCustomLog: " + isCustomLog);
                if (_LogData.customVisualLog)
                {
                    // remove the 2 lines of custom debug logger.
                    // keep in mind the height changer.
                    if (stackTrace_scrollView.childCount > 2)
                    {
                        //Debug.Log("Removing lines. ------");
                        stackTrace_scrollView.RemoveAt(2);
                        stackTrace_scrollView.RemoveAt(2);
                        stackTrace_scrollView.RemoveAt(2);
                    }

                    EditorGUIUtility.PingObject(_LogData.gameObjectContext);

                    if (_LogData.Nesting_id != "") logButton_doubleClick(_LogData);
                }

                // indicate that the log is selected visually.
                if (selectedLogButton != null) selectedLogButton.RemoveFromClassList("LogSelected");
                selectedLogButton = _LogData.button;
                if (!selectedLogButton.ClassListContains("LogSelected")) selectedLogButton.AddToClassList("LogSelected");
            })
            {

                enableRichText = true,
                text = _LogData.logString,                
                tooltip = _LogData.logString

            };

            newLogButton.AddToClassList("LogButton");
            //internalDebugCanvas("Creating button: " + _LogData.logString);
            Color newcolor;
            switch (_LogData.type)
            {
                case LogType.Assert:
                case LogType.Exception:
                case LogType.Error:
                    newLogButton.ClearClassList();
                    newLogButton.AddToClassList("LogIconed");

                    ColorUtility.TryParseHtmlString("#D00400", out newcolor);
                    newLogButton.style.backgroundColor = newcolor;
                    newLogButton.style.color = Color.black;
                    newLogButton.style.backgroundImage = EditorGUIUtility.FindTexture("console.erroricon");
                    
                    return newLogButton;

                case LogType.Warning:
                    newLogButton.ClearClassList();
                    newLogButton.AddToClassList("LogIconed");

                    newLogButton.style.backgroundColor = Color.yellow; 
                    newLogButton.style.color = Color.black;
                    newLogButton.style.backgroundImage = EditorGUIUtility.FindTexture("console.warnicon");
                    
                    return newLogButton;
            }

            // if the log was not configured by visual console debug.log, skip the following:
            if(_LogData.customVisualLog == false ) return newLogButton;
                        
            newLogButton.style.fontSize = _LogData.textSize;

            ColorUtility.TryParseHtmlString(_LogData.textColor, out newcolor);
            newLogButton.style.color = newcolor;
            ColorUtility.TryParseHtmlString(_LogData.backgroundColor, out newcolor);
            newLogButton.style.backgroundColor = newcolor;

            newLogButton.style.unityFontStyleAndWeight = _LogData.fontStyle;

            newLogButton.style.borderBottomLeftRadius = _LogData.borerRadius;
            newLogButton.style.borderBottomRightRadius = _LogData.borerRadius;
            newLogButton.style.borderTopLeftRadius = _LogData.borerRadius;
            newLogButton.style.borderTopRightRadius = _LogData.borerRadius;
            return newLogButton;
        }

        private void positionLog(Button newLogButton)
        {
            Button spacer = null;
            // place the new log button at the begining of the nesting log, or in the actual active row.
            VisualElement targetLogRow;            
            targetLogRow = getRowBelowLatestActiveNestingLog();

            float gapStart = -1; 
            float gapEnd = newLogButton.worldBound.xMin; // Left edge of the nesting log. 
            if (targetLogRow != null) // if there is a row below the active nesting log.
            {
                internalDebugCanvas("○ targetLogRow: " + targetLogRow);
                gapStart = targetLogRow.worldBound.xMin; // by default the gap starts at the beggining of the row below the nesting log.
                // set start of the spacer.
                if (setupFirstNestedLog)
                {
                    VisualElement lastNestedLogButton = null;
                    // if there are logs in the row below the nesting log.
                    if (targetLogRow.hierarchy.childCount > 0)
                    {
                        lastNestedLogButton = targetLogRow.hierarchy[targetLogRow.hierarchy.childCount - 1];
                        gapStart = lastNestedLogButton.worldBound.xMax; // Right edge of the last nested log.
                    }
                    else 
                    {
                        gapStart = targetLogRow.worldBound.xMin;
                    }
                    setupFirstNestedLog = false;
                }

                // Set spacer width if needed.
                Button nestingLog = activeNestingLogsDataList[activeNestingLogsDataList.Count - 1].button;
                float widthDifference = 0f;
                // Calculate the width for the spacer.
                // add a spacer log.
                if (targetLogRow.childCount == 0)
                {
                    gapEnd = nestingLog.worldBound.xMin;
                    //internalDebugCanvas("1 gapStart ► " + gapStart);
                    //internalDebugCanvas("1 gapEnd ► " + gapEnd);
                    if (gapStart < gapEnd)
                    {
                        spacer = createSpacerLog(gapEnd, gapStart);
                    }
                }
                else
                {
                    //internalDebugCanvas("farthestLogButton: " + farthestLogButton);
                    gapEnd = nestingLog.worldBound.xMin; // gap ends at the start of the farthest log.
                    gapStart = targetLogRow.contentContainer[targetLogRow.childCount-1].worldBound.xMax; // the last log of the row.
                    //internalDebugCanvas("2 gapStart ► " + gapStart);
                    //internalDebugCanvas("2 gapEnd ► " + gapEnd);
                    if (gapStart < gapEnd)
                    {
                        spacer = createSpacerLog(gapEnd, gapStart);
                    }
                }

                if (spacer != null) targetLogRow.contentContainer.Add(spacer);
                targetLogRow.contentContainer.Add(newLogButton);
                newLogButton.visible = false;
                //targetLogRow.schedule.Execute(() =>
                //{
                //    if (spacer != null)
                //    {
                //        // if the nesting log width is greater than the new log width: get the difference and add it to the spacer width.
                //        if (newLogButton.resolvedStyle.width < nestingLog.resolvedStyle.width)
                //        {
                //            widthDifference = nestingLog.resolvedStyle.width - newLogButton.resolvedStyle.width;
                //        }
                //        calculateNewWidth(spacer, gapEnd + widthDifference, gapStart);
                //    }
                //    newLogButton.visible = true;
                //}).ExecuteLater(0);//(20);
                setSpacerWidth (spacer, nestingLog, newLogButton, gapStart, gapEnd, widthDifference);
            }
            else // if there is no active nesting log.
            {
                
                //internalDebugCanvas("○○○ targetLogRow: " + targetLogRow);
                //internalDebugCanvas("logRows_scrollView.contentContainer: " + logRows_scrollView.childCount);
                // add it to the first row, at the position of the right of the farthest to the right log.
                targetLogRow = logRows_scrollView.contentContainer.hierarchy[0];
                gapStart = targetLogRow.worldBound.xMin; // start the gap at the start of the empty row.
                // add an empty spacer if necesary.
                // how to detect if there is an empty space?
                // Find gaps in the target row by comparing the xMax of the last button of the target row and the xMin of the farthest button in any row.
                if (targetLogRow.childCount > 0 && farthestLogButton != null)
                {
                    gapEnd = farthestLogButton.worldBound.xMax;
                    gapStart = targetLogRow.contentContainer.hierarchy[targetLogRow.childCount - 1].worldBound.xMax;
                    if (gapStart < gapEnd)
                    {
                        spacer = createSpacerLog(gapEnd, gapStart);
                    }
                }
                else // if the target row is empty. 
                {
                    if (gapStart < gapEnd) // only createa a gap if there is an empty space between the start of the empty row and the last log. (if its the first log, there is no empty space)
                    {
                        spacer = createSpacerLog(gapEnd, gapStart);
                    }
                        
                }
                if (spacer != null) targetLogRow.contentContainer.Add(spacer);
                targetLogRow.contentContainer.Add(newLogButton);
            }
        }

        private async void setSpacerWidth(VisualElement spacer, Button nestingLog, Button newLogButton, float gapStart, float gapEnd, float widthDifference)
        {
            await Awaitable.NextFrameAsync();
            internalDebugCanvas("▀▀▀ setSpacerWidth: " + newLogButton.text + " Frame: " + Time.frameCount);
            if (spacer != null)
            {
                // if the nesting log width is greater than the new log width: get the difference and add it to the spacer width.
                if (newLogButton.resolvedStyle.width < nestingLog.resolvedStyle.width)
                {
                    widthDifference = nestingLog.resolvedStyle.width - newLogButton.resolvedStyle.width;
                }
                calculateNewWidth(spacer, gapEnd + widthDifference, gapStart);
            }
            newLogButton.visible = true;

        }

        private Button createSpacerLog(float worldX_Big, float worldX_Small)
        {
            // Create the button
            Button spacerlogButton = new Button();
            spacerlogButton.AddToClassList("SpacerLog");

            // Calculate the new width for the top button
            float newWidth = worldX_Big - worldX_Small; // Width needed for alignment
            spacerlogButton.style.width = newWidth;

            return spacerlogButton;
        }

        private void calculateNewWidth(VisualElement targetElement, float big, float small)
        {
            // Calculate the new width for the top button
            float newWidth = big - small; // Width needed for alignment
            targetElement.style.width = newWidth;
        }

        private Button createSpacerLog(float width)
        {
            // Create the button
            Button spacerlogButton = new Button();
            spacerlogButton.AddToClassList("SpacerLog");

            spacerlogButton.style.width = width;

            return spacerlogButton;
        }

        /// <summary>
        /// Try to get the row below the latest active nesting log.
        /// </summary>
        /// <returns>Null if there are not active nesting logs or if there is no row below the latest active nesting log.</returns>
        private VisualElement getRowBelowLatestActiveNestingLog()
        {
            //internalDebugCanvas("activeNestingLogsDataList.Count: " + activeNestingLogsDataList.Count);
            if (activeNestingLogsDataList.Count == 0) return null;
            // get latest active nesting log:
            int nestingLogData_RowIndex = activeNestingLogsDataList[activeNestingLogsDataList.Count - 1].rowIndex;
            // get row below the nesting log.
            //internalDebugCanvas("nestingLogData_RowIndex: " + nestingLogData_RowIndex);
            //internalDebugCanvas("logRows_scrollView.contentContainer.childCount: " + logRows_scrollView.contentContainer.childCount);
            // if there is no row below the latest active nesting log's row, return null.
            if ((nestingLogData_RowIndex + 1) > logRows_scrollView.contentContainer.childCount-1) return null;
            // get row below the nesting log.
            VisualElement nestingLogRow_below = logRows_scrollView.contentContainer.hierarchy.ElementAt(nestingLogData_RowIndex + 1);
           
            return nestingLogRow_below;
        }


        private VisualElement createLogRow()
        {
            // Create the log row.
            VisualElement logRow = new VisualElement();
            logRow.name = "logRow";
            logRow.AddToClassList("LogRow");
            // Add the log row to the ScrollView
            logRows_scrollView.contentContainer.Add(logRow);
            logRowsList.Add(logRow);
                       
            return logRow;
        }

        private Color GetLogTypeColor(LogType type)
        {
            // Assign different colors based on the log type
            switch (type)
            {
                case LogType.Error:
                    return new Color(1f, 0.5f, 0.5f); // Light red for errors
                case LogType.Warning:
                    return new Color(1f, 1f, 0.5f); // Yellow for warnings
                case LogType.Log:
                    return new Color(0.8f, 0.8f, 0.8f); // Light gray for regular logs
                default:
                    return Color.white; // Default to white
            }
        }

        private void DisplayStackTrace(string stackTrace)
        {
            var regex = new Regex(@"\((at (.+):(\d+))\)");

            foreach (var line in stackTrace.Split('\n'))
            {
                // Parse the line for links
                var match = regex.Match(line);
                if (match.Success)
                {
                    // Create a container for the line
                    var lineContainer = new VisualElement();
                    lineContainer.style.flexDirection = FlexDirection.Row;
                    lineContainer.style.position = Position.Relative;
                    //lineContainer.style.flexGrow = 1;
                    //lineContainer.style.flexShrink = 1;
                    //lineContainer.style.backgroundColor = Color.black;

                    // Add static text before the link
                    int linkStart = line.IndexOf(match.Value);
                    if (linkStart > 0)
                    {
                        string beforeLink = line.Substring(0, linkStart);
                        Label staticText = new Label(beforeLink);
                        lineContainer.Add(staticText);
                    }

                    // Create the clickable link
                    string filePath = match.Groups[2].Value; // e.g., Assets/MyScript.cs
                    int lineNumber = int.Parse(match.Groups[3].Value); // e.g., 15
                    Label clickableLink = new Label(match.Value)
                    {
                        style =
                    {
                        color = new StyleColor(Color.yellow),
                        unityTextAlign = TextAnchor.MiddleLeft,
                        //flexDirection = FlexDirection.Row,
                        position = Position.Relative
                    }
                    };
                    helpers.SetCursor(clickableLink, MouseCursor.Link);

                    // Add click functionality
                    clickableLink.RegisterCallback<MouseDownEvent>(_ =>
                    {
                        UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(filePath, lineNumber);
                    });

                    lineContainer.Add(clickableLink);

                    // Add static text after the link (if any)
                    int linkEnd = linkStart + match.Value.Length;
                    if (linkEnd < line.Length)
                    {
                        string afterLink = line.Substring(linkEnd);
                        Label staticTextAfter = new Label(afterLink);
                        lineContainer.Add(staticTextAfter);
                    }

                    // Add the line container to the ScrollView
                    stackTrace_scrollView.Add(lineContainer);
                }
                else
                {
                    // Add the entire line as static text if no link is found
                    stackTrace_scrollView.Add(new Label(line));
                }
            }

            
        }

        private void config_stackTraceResizeHandle()
        {
            // Add dragging logic to the handle
            var isDragging = false;
            stackTrace_scrollView.RegisterCallback<MouseDownEvent>(evt =>
            {
                //stackTrace_scrollView.style.position = Position.Absolute;
                isDragging = true;
                evt.StopPropagation();
            });
            rootVisualElement.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (isDragging)
                {
                    // Adjust height based on mouse movement (delta.y)
                    float newHeight = stackTrace_scrollView.resolvedStyle.height -evt.mouseDelta.y;
                    float maxHeight = rootVisualElement.resolvedStyle.height * 0.7f; // Limit to % of total height
                    stackTrace_scrollView.style.height = Mathf.Clamp(newHeight, rootVisualElement.resolvedStyle.height * 0.1f, maxHeight); // Minimum height %

                    evt.StopPropagation();
                }
            });

            rootVisualElement.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (isDragging)
                {
                    //stackTrace_scrollView.style.position = Position.Relative;
                    isDragging = false;
                    evt.StopPropagation();
                }
            });


        }

        private VisualElement CreateStackTracerHeightChanger()
        {
            VisualElement newElement = new VisualElement();
            newElement.AddToClassList("HeightChanger");
            newElement.name = "heightChanger";
            helpers.SetCursor(newElement, MouseCursor.ResizeVertical);

            return newElement;
        }

        private void internalDebugCanvas(string logString)
        {
            //if (debugTextComponent != null)
            //{
            //    debugTextComponent.text += logString + "<br>";
            //}           
        }

        /// <summary>
        /// This does not work well with showing visual logs because they get interrupted when 2 or more logs are handled one after the other.
        /// </summary>
        /// <param name="logString"></param>
        //private void internalDebugDefault(string logString)
        //{
        //    cancelQueuingLogs = true;
        //    Debug.Log(logString);
        //}

        private void playModeStateChanged(PlayModeStateChange state)
        {
            if(state == PlayModeStateChange.ExitingEditMode)
            {
                clearButton_Click(null);
                logQueue.Clear();
            }
        }


    }

    internal static class helpers
    {

        public static void SetCursor(this VisualElement element, MouseCursor cursor)
        {

            object objCursor = new UnityEngine.UIElements.Cursor();
            PropertyInfo fields = typeof(UnityEngine.UIElements.Cursor).GetProperty("defaultCursorId", BindingFlags.NonPublic | BindingFlags.Instance);
            fields.SetValue(objCursor, (int)cursor);
            element.style.cursor = new StyleCursor((UnityEngine.UIElements.Cursor)objCursor);

        }
    }
    
}
#endif