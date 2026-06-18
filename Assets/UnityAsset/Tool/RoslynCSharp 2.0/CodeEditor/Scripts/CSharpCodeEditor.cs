using UnityEngine;
using UnityEngine.UI;
using System.Text;
using System.Reflection;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Events;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RoslynCSharp.CodeEditor
{
    /// <summary>
    /// The main InGame Code Editor component for displaying a syntax highlighting code editor UI element.
    /// </summary>
    public class CSharpCodeEditor : MonoBehaviour
    {
        // Events
        public UnityEvent<string> OnTextChanged = new UnityEvent<string>();

        // Private 
        private static readonly KeyCode[] focusKeys = { KeyCode.Return, KeyCode.Backspace, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow };
        private static StringBuilder highlightedBuilder = new StringBuilder(4096);
        private static StringBuilder lineBuilder = new StringBuilder();
        private static MethodInfo scrollBarUpdateFix = null;

        private RectTransform inputTextTransform = null;
        private RectTransform lineHighlightTransform = null;
        private int lineCount = 0;
        private int currentLine = 0;
        private int currentColumn = 0;
        private int currentIndent = 0;
        private string lastText = null;
        private bool delayedRefresh = false;
        private float lastScrollValue = 0f;
        private bool lineHighlightLocked = false;

        // Complains about references never assigned but they are inspector values
#pragma warning disable 0649
        [Header("Elements")]
        [SerializeField]
        private TMP_InputField inputField;
        [SerializeField]
        private TextMeshProUGUI inputText;
        [SerializeField]
        private TextMeshProUGUI inputHighlightText;
        [SerializeField]
        private TextMeshProUGUI lineText;
        [SerializeField]
        private Image background;
        [SerializeField]
        private Image lineHighlight;
        [SerializeField]
        private Image lineNumberBackground;
        [SerializeField]
        private Image scrollbar;

        [Header("Themes")]
        [SerializeField]
        private CSharpCodeEditorTheme editorTheme = null;
        [SerializeField]
        private CSharpCodeSyntaxTheme languageTheme = null;

        [Header("Options")]
        [SerializeField]
        private bool lineNumbers = true;
        [SerializeField]
        private int lineNumbersSize = 20;

#if UNITY_2018_2_OR_NEWER
        [Header("TMP Compatibility")]
        [SerializeField]
        private bool applyLineOffsetFix = false;
#endif
#pragma warning restore 0649


        // Properties
        /// <summary>
        /// The current editor theme that is being used by the code editor.
        /// This value will be null if no theme is assigned but the code editor will revert to built in default colors.
        /// </summary>
        public CSharpCodeEditorTheme EditorTheme
        {
            get { return editorTheme; }
            set
            {
                editorTheme = value;
                ApplyTheme();
            }
        }

        /// <summary>
        /// The current language theme that is being used by the code editor.
        /// The language theme controls which aspects of the text are syntax highlighted.
        /// You can set this value to null to disable syntax highlighting.
        /// </summary>
        public CSharpCodeSyntaxTheme LanguageTheme
        {
            get { return languageTheme; }
            set
            {
                languageTheme = value;
            }
        }

        /// <summary>
        /// Get the TextMesh Pro input field that this code editor is managing.
        /// </summary>
        public TMP_InputField InputField
        {
            get { return inputField; }
        }

        /// <summary>
        /// Get the total number of lines that the text occupies.
        /// </summary>
        public int LineCount
        {
            get { return lineCount; }
        }

        /// <summary>
        /// Get the current line number for the caret position.
        /// </summary>
        public int CurrentLine
        {
            get { return currentLine; }
        }

        /// <summary>
        /// Get the current column number for the caret position.
        /// </summary>
        public int CurrentColumn
        {
            get { return currentColumn; }
        }

        /// <summary>
        /// Get the current indent level for the caret position.
        /// </summary>
        public int CurrentIndent
        {
            get { return currentIndent; }
        }

        /// <summary>
        /// The text of the code editor input field.
        /// Assigning text will automatically cause a refresh so you do not need to call it manually.
        /// </summary>
        public string Text
        {
            get { return inputField.text; }
            set
            {
                bool empty = string.IsNullOrEmpty(value);

                if (empty == false)
                {
                    inputField.text = value;
                    inputHighlightText.text = value;

                    // Nasty hack to force TMP to update the scroll bar because in some cases it will fail to do so.
                    try
                    {
                        if (scrollBarUpdateFix == null)
                        {
                            scrollBarUpdateFix = typeof(TMP_InputField).GetMethod("UpdateScrollbar", BindingFlags.Instance | BindingFlags.NonPublic);
                        }

                        // Invoke the method
                        scrollBarUpdateFix.Invoke(inputField, null);
                    }
                    catch { }

                    //inputField.ForceLabelUpdate();
                    //inputText.SetText(value, true);
                    delayedRefresh = true;

                    inputText.ForceMeshUpdate(false);
                }
                else
                {
                    inputField.text = string.Empty;
                    inputHighlightText.text = string.Empty;
                    inputText.ForceMeshUpdate(false);
                }
            }
        }

        /// <summary>
        /// Get the current text including xml color tags generated by the syntax highlighter.
        /// </summary>
        public string HighlightedText
        {
            get { return inputHighlightText.text; }
        }

        /// <summary>
        /// Is the line numbers column enabled.
        /// Setting this value to false will cause the column to be hidden.
        /// </summary>
        public bool LineNumbers
        {
            get { return lineNumbers; }
            set
            {
                lineNumbers = value;

                RectTransform inputFieldTransform = inputField.transform as RectTransform;
                RectTransform lineNumberBackgroudTransform = lineNumberBackground.transform as RectTransform;

                // Check for line numbers
                if (lineNumbers == true)
                {
                    // Enable line numbers
                    lineNumberBackground.gameObject.SetActive(true);
                    lineText.gameObject.SetActive(true);

                    // Set left value
                    inputFieldTransform.offsetMin = new Vector2(lineNumbersSize, inputFieldTransform.offsetMin.y);
                    lineNumberBackgroudTransform.sizeDelta = new Vector2(lineNumbersSize + 75, lineNumberBackgroudTransform.sizeDelta.y);
                }
                else
                {
                    // Disable line numbers
                    lineNumberBackground.gameObject.SetActive(false);
                    lineText.gameObject.SetActive(false);

                    // Set left value
                    inputFieldTransform.offsetMin = new Vector2(0, inputFieldTransform.offsetMin.y);
                }
            }
        }

        /// <summary>
        /// The current size of the line number column.
        /// Default size is 20.
        /// </summary>
        public int LineNumbersSize
        {
            get { return lineNumbersSize; }
            set
            {
                lineNumbersSize = value;

                // Update the line numbers
                LineNumbers = lineNumbers;
            }
        }

        // Methods
#if UNITY_EDITOR
        /// <summary>
        /// Called by Unity.
        /// </summary>
        public void OnValidate()
        {
            // Update line numbers
            LineNumbersSize = lineNumbersSize;

            // Appy the theme
            if (AllReferencesAssigned() == true)
                if (editorTheme != null)
                    ApplyTheme();

            if (languageTheme != null)
                languageTheme.Invalidate();
        }
#endif

        /// <summary>
        /// Called by Unity.
        /// </summary>
        public void Awake()
        {
            // Check for invalid references
            if (AllReferencesAssigned() == false)
            {
                enabled = false;
                throw new MissingReferenceException("One or more required references are missing. Make sure all references under the 'Elements' header are assigned");
            }

            // Cache transform
            this.inputTextTransform = inputText.GetComponent<RectTransform>();
            this.lineHighlightTransform = lineHighlight.GetComponent<RectTransform>();


            //highlightNode = Node.ArrayInitialize(Token.Symbol("{", 6), new ExpressionNode[] { Node.Literal(Token.EndToken) }, Token.Symbol("}", 27));
        }

        /// <summary>
        /// Called by Unity.
        /// </summary>
        public void Start()
        {
            // Load default theme
            if (editorTheme == null)
                editorTheme = CSharpCodeEditorTheme.DefaultTheme;

            // Apply the theme
            ApplyTheme();
        }

        /// <summary>
        /// Called by Unity.
        /// </summary>
        public void LateUpdate()
        {
            if (Input.mouseScrollDelta != Vector2.zero || inputField.verticalScrollbar.value != lastScrollValue)
            {
                UpdateCurrentLineHighlight();
                lastScrollValue = inputField.verticalScrollbar.value;
            }

            // Make sure the input field is focused
            if (inputField.isFocused == true || delayedRefresh == true)
            {
                // Check for delayed refresh caused by pasting text
                if (delayedRefresh == true)
                {
                    delayedRefresh = false;
                    Refresh(true, false);
                }

                // Check for paste text
                if (Input.GetKey(KeyCode.LeftControl) == true && Input.GetKeyDown(KeyCode.V) == true)
                {
                    // Refresh full text on the next frame after tmp has updated its text infos
                    delayedRefresh = true;
                }

                // Check if we are typing
                if (Input.anyKey == true)
                    Refresh();

                bool focusKeyPressed = false;

                // Check for any focus key pressed
                foreach (KeyCode key in focusKeys)
                {
                    if (Input.GetKey(key) == true)
                    {
                        focusKeyPressed = true;
                        break;
                    }
                }

                // Update line highlight
                if (focusKeyPressed == true || Input.GetMouseButton(0))
                    UpdateCurrentLineHighlight();
            }
        }

        internal int GetEndOfLineColumn(string text, int line)
        {
            int index = 0;
            int currentLine = 1;
            int currentColumn = 0;

            while (currentLine < line)
            {
                for (int i = index; i < text.Length; i++)
                {
                    if (text[i] == '\n')
                    {
                        index = i + 1;
                        currentLine++;
                        break;
                    }
                }
            }

            // Get column
            for (int i = index; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    break;
                }
                else if (text[i] == '\t')
                {
                    currentColumn += 4;
                }
                else
                    currentColumn++;
            }

            return currentColumn;
        }

        /// <summary>
        /// Causes the displayed text content to be refreshed and rehighlighted if it has changed.
        /// </summary>
        /// <param name="forceUpdate">Forcing an update will cause the text to be refreshed even if it has not changed</param>
        /// <param name="updateLineOnly">Should only the current line be refreshed or the whole text</param>
        public void Refresh(bool forceUpdate = false, bool updateLineOnly = true)
        {
            // Trigger a content change event
            DisplayedContentChanged(inputField.text, forceUpdate, updateLineOnly);
        }

        /// <summary>
        /// Set the line where the line highlight bar will be positioned. Valid line numbers start at 1 and count up until <see cref="LineCount"/>.
        /// You may also like to lock the line highlight bar in position to prevent it being moved by the user which can be achieved by passing 'true' as second argument.
        /// </summary>
        /// <param name="lineNumber">The absolute line number to move the line highlight bar to</param>
        /// <param name="lockLineHighlight">True if the line highlight bar should be locked after moving to the specified line or false if the line bar should be unlocked.</param>
        public void SetLineHighlight(int lineNumber, bool lockLineHighlight)
        {
            // Check if code editor is not active
            if (isActiveAndEnabled == false || lineNumber < 1 || lineNumber > LineCount)
                return;

            int lineOffset = 0;
            int lineIndex = lineNumber - 1;// inputText.textInfo.lineCount - lineNumber - 1;

#if UNITY_2018_2_OR_NEWER
            if (applyLineOffsetFix == true)
                lineOffset++;
#endif

            // Highlight the current line
            lineHighlightTransform.anchoredPosition = new Vector2(5,
                (inputText.textInfo.lineInfo[inputText.textInfo.characterInfo[0].lineNumber].lineHeight *
                -lineIndex) + lineOffset - 4f +
                inputTextTransform.anchoredPosition.y);

            // Lock the line highlight so it cannot be moved
            if (lockLineHighlight == true)
                LockLineHighlight();
            else
                UnlockLineHighlight();
        }

        /// <summary>
        /// Lock the line highlight bar at the current line. Mouse or keyboard events will not affect the position of the line highlight bar until <see cref="UnlockLineHighlight"/> is called.
        /// </summary>
        public void LockLineHighlight()
        {
            lineHighlightLocked = true;
        }

        /// <summary>
        /// Unlock the line highlight bar. Mouse or keyboard events will cause the line highlight bar to be updated as the user moves to different lines.
        /// </summary>
        public void UnlockLineHighlight()
        {
            lineHighlightLocked = false;
        }

        private void DisplayedContentChanged(string newText, bool forceUpdate, bool updateLineOnly)
        {
            // Check for change
            if ((forceUpdate == false && lastText == newText) || string.IsNullOrEmpty(newText) == true)
            {
                if (string.IsNullOrEmpty(newText) == true)
                {
                    inputHighlightText.text = string.Empty;
                }

                // Its possible the text was cleared so we need to sync numbers and highlighter
                UpdateCurrentLineNumbers();
                UpdateCurrentLineHighlight();
                return;
            }

            // Run parser to highlight keywords
            inputHighlightText.text = SyntaxHighlightContent(newText);

            // Autohide scrollbar
            bool showScrollbar = inputField.verticalScrollbar.size < 1f;

            // Show the scrollbar
            inputField.verticalScrollbar.gameObject.SetActive(showScrollbar);


            // Sync line numbers and update the line highlight
            UpdateCurrentLineNumbers();
            UpdateCurrentLineHighlight();

            this.lastText = newText;

            // Trigger event
            OnTextChanged.Invoke(newText);
        }

        private void UpdateCurrentLineNumbers()
        {
            // Get the line count
            int currentLineCount = inputText.textInfo.lineCount;

            int currentLineNumber = 1;

            // Check for a change in line
            if (currentLineCount != lineCount)
            {
                // Update line numbers
                lineBuilder.Length = 0;

                // Build line numbers string
                for (int i = 1; i < currentLineCount + 2; i++)
                {
                    if (i - 1 > 0 && i - 1 < currentLineCount - 1)
                    {
                        int characterStart = inputText.textInfo.lineInfo[i - 1].firstCharacterIndex;
                        int characterCount = inputText.textInfo.lineInfo[i - 1].characterCount;

                        if (characterCount != 0 && inputText.text.Substring(characterStart, characterCount).Contains("\n") == false)
                        {
                            lineBuilder.Append("\n");
                            continue;
                        }
                    }

                    lineBuilder.Append(currentLineNumber);
                    lineBuilder.Append('\n');

                    currentLineNumber++;

                    if (i - 1 == 0 && i - 1 < currentLineCount - 1)
                    {
                        int characterStart = inputText.textInfo.lineInfo[i - 1].firstCharacterIndex;
                        int characterCount = inputText.textInfo.lineInfo[i - 1].characterCount;

                        if (characterCount != 0 && inputText.text.Substring(characterStart, characterCount).Contains("\n") == false)
                        {
                            lineBuilder.Append("\n");
                            continue;
                        }
                    }
                }

                // Update displayed line numbers
                lineText.text = lineBuilder.ToString();
                lineCount = currentLineCount;
            }
        }

        private void UpdateCurrentLineHighlight()
        {
            // Check if code editor is not active
            if (isActiveAndEnabled == false || lineHighlightLocked == true)
                return;

            int lineOffset = 0;

#if UNITY_2018_2_OR_NEWER
            if (applyLineOffsetFix == true)
                lineOffset++;
#endif

            int caretIndex = inputField.caretPosition < inputText.textInfo.characterInfo.Length
                ? inputField.caretPosition
                : inputField.text.Length - 1;

            // Highlight the current line
            lineHighlightTransform.anchoredPosition = new Vector2(5, inputText.textInfo.lineInfo[inputText.textInfo.characterInfo[0].lineNumber].lineHeight *
                (-inputText.textInfo.characterInfo[caretIndex].lineNumber + lineOffset) - 4 +
                inputTextTransform.anchoredPosition.y);
        }

        private string SyntaxHighlightContent(string inputText)
        {
            // Check if parsing should not run
            if (languageTheme == null)
                return inputText;

            // Check if the theme supports highlighting
            if (editorTheme != null && editorTheme.allowSyntaxHighlighting == false)
                return inputText;

            const string closingTag = "</color>";
            int offset = 0;

            highlightedBuilder.Length = 0;

            // Parse all tokens
            IEnumerable <SyntaxToken> tokens = CSharpSyntaxTree.ParseText(inputText).GetRoot().DescendantTokens(descendIntoTrivia: true);

            //foreach (Token token in tokens)
            foreach(SyntaxToken token in tokens)
            {
                int startIndex = token.Span.Start;
                int endIndex = token.Span.End;

                // Get the colour
                string color = languageTheme.GetHTMLColorString(token);

                // Copy text before the match
                for (int i = offset; i < startIndex; i++)
                    highlightedBuilder.Append(inputText[i]);

                // Add the opening color tag
                highlightedBuilder.Append(color);

                // Copy text in between the match boundaries
                for (int i = startIndex; i < endIndex; i++)
                    highlightedBuilder.Append(inputText[i]);

                // Add the closing color tag
                highlightedBuilder.Append(closingTag);

                // Update offset
                offset = endIndex;
            }

            // Copy remaining text
            for (int i = offset; i < inputText.Length; i++)
                highlightedBuilder.Append(inputText[i]);

            // Convert to string
            inputText = highlightedBuilder.ToString();

            return inputText;
        }

        private void ApplyTheme()
        {
            // Check for missing references
            if (AllReferencesAssigned() == false)
                throw new MissingReferenceException("Cannot apply theme because one or more required component references are missing. Make sure all references under the 'Elements' header are assigned");

            bool nullTheme = false;

            // Check for no theme
            if (editorTheme == null)
            {
                // Get the default theme
                editorTheme = CSharpCodeEditorTheme.DefaultTheme;
                nullTheme = true;
            }

            // Apply theme colors
            inputField.caretColor = editorTheme.caretColor;
            //inputText.color = editorTheme.textColor;
            inputText.color = Color.clear;
            inputHighlightText.color = editorTheme.textColor;
            background.color = editorTheme.backgroundColor;
            lineHighlight.color = editorTheme.lineHighlightColor;
            lineNumberBackground.color = editorTheme.lineNumberBackgroundColor;
            lineText.color = editorTheme.lineNumberTextColor;
            scrollbar.color = editorTheme.scrollbarColor;

            // Set active to null
            if (nullTheme == true)
                editorTheme = null;
        }

        private bool AllReferencesAssigned()
        {
            if (inputField == null ||
                inputText == null ||
                inputHighlightText == null ||
                lineText == null ||
                background == null ||
                lineHighlight == null ||
                lineNumberBackground == null ||
                scrollbar == null)
            {
                // One or more references are not assigned
                return false;
            }
            return true;
        }
    }
}
