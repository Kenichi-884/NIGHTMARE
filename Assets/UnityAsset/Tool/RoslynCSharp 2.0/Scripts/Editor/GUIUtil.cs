using UnityEngine;

namespace RoslynCSharp
{
    internal static class GUIUtil
    {
        // Private
        private static GUIStyle separatorStyle;

        // Public
        public const float SeparatorThickness = 2f;
        public static readonly Color SeparatorColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);

        // Properties
        public static GUIStyle SeparatorStyle
        {
            get
            {
                // Build the style
                if (separatorStyle == null)
                {
                    separatorStyle = new GUIStyle();

                    separatorStyle.fixedHeight = SeparatorThickness;
                    separatorStyle.stretchWidth = true;
                    separatorStyle.margin = new RectOffset(4, 4, -2, -2);

                    // Background colour
                    separatorStyle.normal.background = new Texture2D(1, 1);
                    separatorStyle.normal.background.SetPixel(0, 0, SeparatorColor);
                    separatorStyle.normal.background.Apply();
                }
                return separatorStyle;
            }
        }

        // Methods
        public static void DrawSeparator(float uniformSpacing = 0f)
        {
            // Before spacing
            if (uniformSpacing != 0f)
                GUILayout.Space(uniformSpacing);

            // Draw the label
            GUILayout.Button(GUIContent.none, SeparatorStyle);

            // After spacing
            if (uniformSpacing != 0f)
                GUILayout.Space(uniformSpacing);
        }
    }
}
