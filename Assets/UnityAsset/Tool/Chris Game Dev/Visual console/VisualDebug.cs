using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace chrisGameDev.VisualConsole
{
    public static class VisualDebug
    {
        /// <summary>
        /// When a new nesting log gets created, it is stored here.
        /// </summary>
        private static logData newestLogData = null;
        /// <summary>
        /// When a new nesting log gets created, it is stored here.
        /// </summary>
        private static INestingLogData activatedNestingLog = null;

        [HideInCallstack]
        public static void LogPreset(string message, string presetName, UnityEngine.Object context = null)
        {
#if UNITY_EDITOR
            logPreset selectedPreset = VisualConsole_SO.instance.presetList.Find(x => x.name == presetName);
            if (selectedPreset == null) selectedPreset = new logPreset() { name = "default", textColor = "white", backgroundColor = "yellowgreen", borderRadius = 3, textSize = 12, fontStyle = FontStyle.Normal };

            string logString = getRichTextString(message, selectedPreset.textColor, selectedPreset.textSize, selectedPreset.fontStyle);
            createNormalLogData(selectedPreset.textColor, selectedPreset.backgroundColor, selectedPreset.borderRadius, selectedPreset.textSize, selectedPreset.fontStyle, (GameObject)context);
#endif
            defaultLog(message, context);
        }

        [HideInCallstack]
        public static void NestingLogPreset(string id, string message, string presetName, UnityEngine.Object context = null)
        {
#if UNITY_EDITOR
            logPreset selectedPreset = VisualConsole_SO.instance.presetList.Find(x => x.name == presetName);
            if (selectedPreset == null) selectedPreset = new logPreset() { name = "default", textColor = "white", backgroundColor = "yellowgreen", borderRadius = 3, textSize = 12, fontStyle = FontStyle.Normal };

            string logString = getRichTextString(message, selectedPreset.textColor, selectedPreset.textSize, selectedPreset.fontStyle);
            createNestingLogData(id, selectedPreset.textColor, selectedPreset.backgroundColor, selectedPreset.borderRadius, selectedPreset.textSize, selectedPreset.fontStyle, (GameObject)context);
#endif
            defaultLog(message, context);
        }

        [HideInCallstack]
        public static void Log(
            string message, string textColor = "white",int textSize = 14,
            FontStyle fontStyle = FontStyle.Normal, 
            string backgroundColor = "yellowgreen", 
            int borderRadius = 3,
            UnityEngine.Object context = null)
        {
#if UNITY_EDITOR
            string logString = getRichTextString(message, textColor,textSize, fontStyle);

            // Colors for UI button:
            color_data selectedColor = VisualConsole_SO.instance.colorList.Find(x => x.name == textColor);
            if (selectedColor == null) selectedColor = VisualConsole_SO.instance.colorList.Find(x => x.name == "yellowgreen");
            textColor = selectedColor.getColorHex();
            selectedColor = VisualConsole_SO.instance.colorList.Find(x => x.name == backgroundColor);
            if (selectedColor == null) selectedColor = VisualConsole_SO.instance.colorList.Find(x => x.name == "forestgreen");
            backgroundColor = selectedColor.getColorHex();

            createNormalLogData(textColor, backgroundColor, borderRadius, textSize, fontStyle, (GameObject)context);

            
            defaultLog(message, context);
#endif
        }

        [HideInCallstack]
        private static void defaultLog(string message, UnityEngine.Object context = null)
        {
            UnityEngine.Debug.Log(
                message,
                context
            );
        }

        private static string getRichTextString(
            string message, string textColor = "white", int textSize = 14,
            FontStyle fontStyle = FontStyle.Normal)
        {
            // set text style:
            string styleTagStart = "";
            string styleTagEnd = "";
            switch (fontStyle)
            {
                case FontStyle.Bold:
                    styleTagStart = "<b>";
                    styleTagEnd = "</b>";
                    break;
                case FontStyle.Italic:
                    styleTagStart = "<i>";
                    styleTagEnd = "</i>";
                    break;
                case FontStyle.Normal:
                    styleTagStart = "";
                    styleTagEnd = "";
                    break;
                case FontStyle.BoldAndItalic:
                    styleTagStart = "<i><b>";
                    styleTagEnd = "</i></b>";
                    break;
                    //case TMPro.FontStyles.Strikethrough:
                    //    styleTagStart = "<s>";
                    //    styleTagEnd = "</s>";
                    //    break;
                    //case TMPro.FontStyles.Underline:
                    //    styleTagStart = "<u>";
                    //    styleTagEnd = "</u>";
                    //    break;
                    //case TMPro.FontStyles.UpperCase:
                    //    styleTagStart = "<uppercase>";
                    //    styleTagEnd = "</uppercase>";
                    //    break;
            }

            // set text color:
            color_data selectedColor = VisualConsole_SO.instance.colorList.Find(x => x.name == textColor);
            if (selectedColor == null) selectedColor = VisualConsole_SO.instance.colorList.Find(x => x.name == "white");
            textColor = selectedColor.getColorHex();
            

            string logString =
                "<size=" + textSize + ">" +
                "<color=" + textColor + ">" +
                    styleTagStart +
                    message +
                    styleTagEnd +
                    "</color>" +
                    "</size>";

            return logString;
        }

        /// <summary>
        /// Starts a nesting group log.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="context"></param>
        /// <param name="id"></param>
        public static void NestingLog(string id, string message, UnityEngine.Object context)
        {
#if UNITY_EDITOR
            createNestingLogData(id, "forestgreen", "yellowgreen", 2, 12, FontStyle.Normal, (GameObject)context);

#endif
            defaultLog(message, context);
        }

        /// <summary>
        /// Starts a nesting group log.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="id"></param>
        public static void NestingLog(string id, string message)
        {
#if UNITY_EDITOR
            createNestingLogData(id);

#endif
            defaultLog(message);
        }

        public static void NestingLog(string id, string message, 
            string textColor = "white", int textSize = 14,
            FontStyle fontStyle = FontStyle.Normal,
            string backgroundColor = "yellowgreen",
            int borderRadius = 3)
        {
#if UNITY_EDITOR
            string logString = getRichTextString(message, textColor, textSize, fontStyle);

            createNestingLogData(id, textColor, backgroundColor, borderRadius, textSize, fontStyle);

#endif
            defaultLog(message);
        }

        [HideInCallstack]
        public static void NestingLogEnd(string id)
        {
#if UNITY_EDITOR
            newestLogData = new logData();
            newestLogData.Nesting_id = id;
            newestLogData.customVisualLog = true;
            newestLogData.nesting_ender = true;


#endif
            defaultLog("End of nesting log: " + id);
        }


        private static void createNestingLogData(string id, string textColor = "forestgreen",
            string backgroundColor = "yellowgreen", int borderRadius = 2,
            int textSize = 12,
            FontStyle fontStyle = FontStyle.Normal,
            GameObject gameObjectContext = null)
        {
            activatedNestingLog = new logData();
            activatedNestingLog.Nesting_id = id;
            activatedNestingLog.nestedLogs = new List<logData>();

            // Colors for UI button:
            color_data selectedColor = VisualConsole_SO.instance.colorList.Find(x => x.name == textColor);
            if (selectedColor == null) selectedColor = VisualConsole_SO.instance.colorList.Find(x => x.name == "yellowgreen");
            textColor = selectedColor.getColorHex();
            selectedColor = VisualConsole_SO.instance.colorList.Find(x => x.name == backgroundColor);
            if (selectedColor == null) selectedColor = VisualConsole_SO.instance.colorList.Find(x => x.name == "forestgreen");
            backgroundColor = selectedColor.getColorHex();

            addCommonLogData(activatedNestingLog as logData, textColor, backgroundColor, borderRadius, textSize, fontStyle, gameObjectContext);
        }

        private static void createNormalLogData(string textColor, string backgroundColor, int borderRadius, 
            int textSize = 12,
            FontStyle fontStyle = FontStyle.Normal,
            GameObject gameObjectContext = null)
        {
            newestLogData = new logData();
            addCommonLogData(newestLogData, textColor, backgroundColor, borderRadius, textSize, fontStyle, gameObjectContext);
        }

        private static void addCommonLogData(ICommonLogData targetLogData,
            string textColor,
            string backgroundColor, int borderRadius,
            int textSize = 12,
            FontStyle fontStyle = FontStyle.Normal,
            GameObject gameObjectContext = null)
        {
            targetLogData.customVisualLog = true;
            targetLogData.textColor = textColor;
            targetLogData.backgroundColor = backgroundColor;
            targetLogData.borerRadius = borderRadius;
            targetLogData.textSize = textSize;
            targetLogData.fontStyle = fontStyle;
            targetLogData.gameObjectContext = gameObjectContext;
        }

        internal static logData getNewestNormalLogData()
        {
            return newestLogData;
        }

        /// <summary>
        /// Check to see if a nested log was started or not.
        /// </summary>
        /// <returns></returns>
        internal static INestingLogData getNewNestingLogData()
        {
            return activatedNestingLog;
        }

        internal static void resetNewestNormalLogData()
        {
            newestLogData = null;
        }

        internal static void resetNewNestingLogData()
        {
            activatedNestingLog = null;
        }

    }

    

    public class logData : INestingLogData, ICommonLogData
    {
        public string Nesting_id { get; set; } = "";
        public List<logData> nestedLogs { get; set; }
        public bool isFolded { get; set; } = false;
        public float finalWidth { get; set; } = 10f;
        public VisualElement folded_spacer_width { get; set; }
        public VisualElement folded_spacer_nestedLogs { get; set; }
        public bool customVisualLog { get; set; } = false;
        public Button button { get; set; }
        public bool active { get; set; } = true;
        public bool nesting_ender { get; set; } = false;
        public int rowIndex { get; set; } = 0;
        public string textColor { get; set; } = "";
        public string backgroundColor { get; set; } = "";
        public int borerRadius { get; set; } = 4;
        public int textSize { get; set; } = 12;
        public FontStyle fontStyle { get; set; } = FontStyle.Normal;
        public GameObject gameObjectContext { get; set; }
        public string logString { get; set; }
        public string stackTrace { get; set; }
        public LogType type { get; set; }
        public VisualElement folded_icon { get; set; }
    }

    public interface ICommonLogData
    {
        /// <summary>
        /// Indicate if this log was created by the default Unity debug log, or by the custom Visual Debug.
        /// </summary>
        public bool customVisualLog { get; set; }//= false;
        public Button button { get; set; }

        /// <summary>
        /// Indicates that its width is updated with each new log or not.
        /// </summary>
        public bool active { get; set; }//= true;
        /// <summary>
        /// Indicates if this is a type of log with the only purpose is to end another nesting log.
        /// </summary>
        public bool nesting_ender { get; set; }//= false;

        public int rowIndex { get; set; }//= 0;

        public string textColor { get; set; }// = "";
        public string backgroundColor { get; set; }//= "";
        public int borerRadius { get; set; }//= 4;
        public int textSize { get; set; }//= 12;
        public FontStyle fontStyle { get; set; }//= FontStyle.Normal;

        public GameObject gameObjectContext { get; set; }

        public string logString { get; set; }
        public string stackTrace { get; set; }
        public LogType type { get; set; }
    }

    public interface INestingLogData : ICommonLogData
    {
        /// <summary>
        /// Indicate if this log has a nesting id, if it doesn't it means it a normal log.
        /// </summary>
        public string Nesting_id { get; set; }//= "";

        /// <summary>
        /// Stores all the logs that are nested by this log, only used if this is a nesting log.
        /// </summary>
        public List<logData> nestedLogs { get; set; }
        /// <summary>
        /// Indicates if this log is folded or not, hiding its nested logs.
        /// </summary>
        public bool isFolded { get; set; }//= false;
        public float finalWidth { get; set; }//= 10f;

        /// <summary>
        /// Spacer created when the nesting log is folded. Used to fill the shrinked width space.
        /// </summary>        
        public VisualElement folded_spacer_width { get; set; }
        /// <summary>
        ///  Spacer created when the nesting log is folded. Used to fill the nested logs space.
        /// </summary>
        public VisualElement folded_spacer_nestedLogs { get; set; }

        public VisualElement folded_icon { get; set; }
    }
}
