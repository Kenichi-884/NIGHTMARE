using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Text;
using UnityEngine;

namespace RoslynCSharp.CodeEditor
{
    /// <summary>
    /// A theme that can be assigned to a code editor to change the syntax highlighting rules.
    /// </summary>
    [Serializable]
    [CreateAssetMenu(fileName = "Code Language Theme", menuName = "Roslyn C# 2.0/Code Editor/Language Theme")]
    public class CSharpCodeSyntaxTheme : ScriptableObject
    {
        // Internal
        internal static readonly StringBuilder sharedBuilder = new StringBuilder();

        // Private
        private string keywordColorString = null;
        private string symbolColorString = null;
        private string numberColorString = null;
        private string literalColorString = null;
        private string commentColorString = null;

        // Public        
        /// <summary>
        /// The name of the language that this theme provides syntax highlighting for. For example: 'C#'.
        /// </summary>
        public string languageName;

        public Color keywordColor = Color.blue;
        public Color symbolColor = Color.yellow;
        public Color numberColor = Color.red;
        public Color literalColor = Color.magenta;
        public Color commentColor = Color.green;

        // Methods
        private void OnEnable()
        {
            Invalidate();
        }

        private void OnValidate()
        {
            Invalidate();
        }

        public void Invalidate()
        {
            keywordColorString = string.Format("<#{0}>", ColorUtility.ToHtmlStringRGB(keywordColor));
            symbolColorString = string.Format("<#{0}>", ColorUtility.ToHtmlStringRGB(symbolColor));
            numberColorString = string.Format("<#{0}>", ColorUtility.ToHtmlStringRGB(numberColor));
            literalColorString = string.Format("<#{0}>", ColorUtility.ToHtmlStringRGB(literalColor));
            commentColorString = string.Format("<#{0}>", ColorUtility.ToHtmlStringRGB(commentColor));
        }

        public string GetHTMLColorString(SyntaxToken token)
        {
            // Get the token kind
            SyntaxKind kind = token.Kind();
            
            // Check for keyword
            if (SyntaxFacts.IsKeywordKind(kind) == true)
            {
                return keywordColorString;
            }
            // Check for number
            else if(kind == SyntaxKind.NumericLiteralExpression || kind == SyntaxKind.NumericLiteralToken)
            {
                return numberColorString;
            }
            // Check for literal
            else if(SyntaxFacts.IsLiteralExpression(kind) == true)
            {
                return literalColorString;
            }
            // Check for punctuation
            else if (SyntaxFacts.IsLanguagePunctuation(kind) == true)
            {
                return symbolColorString;
            }
            // Check for comment
            else if(kind == SyntaxKind.SingleLineCommentTrivia || kind == SyntaxKind.MultiLineCommentTrivia || SyntaxFacts.IsDocumentationCommentTrivia(kind) == true)
            {
                return commentColorString;
            }            
            return string.Empty;
        }
    }
}
