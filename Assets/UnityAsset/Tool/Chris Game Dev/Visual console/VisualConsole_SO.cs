using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace chrisGameDev.VisualConsole
{
    //[CreateAssetMenu(fileName = "VisualConsole", menuName = "Scriptable Objects/VisualConsole")]
    public class VisualConsole_SO : ScriptableObject
    {
        public static VisualConsole_SO instance;
        public List<color_data> colorList;// = new List<color_data>(); // List of colors.
        public List<logPreset> presetList;// = new List<logPreset>(); // List of log presets.

        public void OnEnable()
        {
            VisualConsole_SO.instance = this;
            initDefaults();
        }

        [ContextMenu("Load Default Values")]
        private void initDefaults()
        {
            default_fillColorList();
            default_fillPresetList();
        }

        //public void OnDisable()
        //{
        //    colorList.Clear();
        //    presetList.Clear();
        //}

        public static void loadSingleton(VisualConsole_SO instance)
        {
            VisualConsole_SO.instance = instance;
        }

        private void default_fillPresetList()
        {
            if (presetList.Count > 0) return;
            presetList = new List<logPreset>();
            presetList.Add(new logPreset { name = "a", textColor = "#7fffd4", backgroundColor = "#3498DB", borderRadius = 5, textSize = 12, fontStyle = FontStyle.Normal });
            presetList.Add(new logPreset { name = "b", textColor = "#8b0000", backgroundColor = "#E74C3C", borderRadius = 10, textSize = 14, fontStyle = FontStyle.Bold });
            presetList.Add(new logPreset { name = "c", textColor = "#006400", backgroundColor = "#2ECC71", borderRadius = 15, textSize = 16, fontStyle = FontStyle.BoldAndItalic });
            presetList.Add(new logPreset { name = "d", textColor = "#8b4513", backgroundColor = "#F39C12", borderRadius = 20, textSize = 18, fontStyle = FontStyle.Bold });
            presetList.Add(new logPreset { name = "e", textColor = "#8b008b", backgroundColor = "#9B59B6", borderRadius = 5, textSize = 12, fontStyle = FontStyle.Bold });
            presetList.Add(new logPreset { name = "f", textColor = "#0000ff", backgroundColor = "#1ABC9C", borderRadius = 10, textSize = 14, fontStyle = FontStyle.BoldAndItalic });
            presetList.Add(new logPreset { name = "g", textColor = "#FFFFFF", backgroundColor = "#E67E22", borderRadius = 15, textSize = 16, fontStyle = FontStyle.Bold });
            presetList.Add(new logPreset { name = "h", textColor = "#6495ed", backgroundColor = "#34495E", borderRadius = 20, textSize = 18, fontStyle = FontStyle.Bold  });
            presetList.Add(new logPreset { name = "i", textColor = "#696969", backgroundColor = "#95A5A6", borderRadius = 5, textSize = 12, fontStyle = FontStyle.BoldAndItalic });
            presetList.Add(new logPreset { name = "j", textColor = "#000000", backgroundColor = "#D35400", borderRadius = 10, textSize = 14, fontStyle = FontStyle.Normal });
            presetList.Add(new logPreset { name = "k", textColor = "#ffd700", backgroundColor = "#27AE60", borderRadius = 15, textSize = 16, fontStyle = FontStyle.Bold });
            presetList.Add(new logPreset { name = "l", textColor = "#ffa500", backgroundColor = "#C0392B", borderRadius = 20, textSize = 18, fontStyle = FontStyle.Italic });
            presetList.Add(new logPreset { name = "m", textColor = "#FFFFFF", backgroundColor = "#16A085", borderRadius = 5, textSize = 12, fontStyle = FontStyle.Bold });
            presetList.Add(new logPreset { name = "n", textColor = "#000000", backgroundColor = "#8E44AD", borderRadius = 10, textSize = 14, fontStyle = FontStyle.BoldAndItalic });
            presetList.Add(new logPreset { name = "o", textColor = "#FFFFFF", backgroundColor = "#2980B9", borderRadius = 15, textSize = 16, fontStyle = FontStyle.Bold  });
            presetList.Add(new logPreset { name = "p", textColor = "#000000", backgroundColor = "#F1C40F", borderRadius = 20, textSize = 18, fontStyle = FontStyle.Bold });
            presetList.Add(new logPreset { name = "q", textColor = "#FFFFFF", backgroundColor = "#7F8C8D", borderRadius = 5, textSize = 12, fontStyle = FontStyle.BoldAndItalic });
            presetList.Add(new logPreset { name = "r", textColor = "#000000", backgroundColor = "#2C3E50", borderRadius = 10, textSize = 14, fontStyle = FontStyle.Bold });
            presetList.Add(new logPreset { name = "s", textColor = "#FFFFFF", backgroundColor = "#1F618D", borderRadius = 15, textSize = 16, fontStyle = FontStyle.Normal });
            presetList.Add(new logPreset { name = "t", textColor = "#000000", backgroundColor = "#A93226", borderRadius = 20, textSize = 18, fontStyle = FontStyle.Bold });
        }

        private void default_fillColorList()
        {
            if(colorList.Count > 0) return;
            colorList = new List<color_data>(); // List of colors.
            // Add color mappings (partial list, add the rest similarly)
            colorList.Add(new color_data("aliceblue", "#f0f8ff"));
            colorList.Add(new color_data("antiquewhite", "#faebd7"));
            colorList.Add(new color_data("aqua", "#00ffff"));
            colorList.Add(new color_data("aquamarine", "#7fffd4"));
            colorList.Add(new color_data("azure", "#f0ffff"));
            colorList.Add(new color_data("beige", "#f5f5dc"));
            colorList.Add(new color_data("bisque", "#ffe4c4"));
            colorList.Add(new color_data("black", "#000000"));
            colorList.Add(new color_data("blanchedalmond", "#ffebcd"));
            colorList.Add(new color_data("blue", "#0000ff"));
            colorList.Add(new color_data("blueviolet", "#8a2be2"));
            colorList.Add(new color_data("brown", "#a52a2a"));
            colorList.Add(new color_data("burlywood", "#deb887"));
            colorList.Add(new color_data("cadetblue", "#5f9ea0"));
            colorList.Add(new color_data("chartreuse", "#7fff00"));
            colorList.Add(new color_data("chocolate", "#d2691e"));
            colorList.Add(new color_data("coral", "#ff7f50"));
            colorList.Add(new color_data("cornflowerblue", "#6495ed"));
            colorList.Add(new color_data("cornsilk", "#fff8dc"));
            colorList.Add(new color_data("crimson", "#dc143c"));
            colorList.Add(new color_data("cyan", "#00ffff"));
            colorList.Add(new color_data("darkblue", "#00008b"));
            colorList.Add(new color_data("darkcyan", "#008b8b"));
            colorList.Add(new color_data("darkgoldenrod", "#b8860b"));
            colorList.Add(new color_data("darkgray", "#a9a9a9"));
            colorList.Add(new color_data("darkgreen", "#006400"));
            colorList.Add(new color_data("darkgrey", "#a9a9a9"));
            colorList.Add(new color_data("darkkhaki", "#bdb76b"));
            colorList.Add(new color_data("darkmagenta", "#8b008b"));
            colorList.Add(new color_data("darkolivegreen", "#556b2f"));
            colorList.Add(new color_data("darkorange", "#ff8c00"));
            colorList.Add(new color_data("darkorchid", "#9932cc"));
            colorList.Add(new color_data("darkred", "#8b0000"));
            colorList.Add(new color_data("darksalmon", "#e9967a"));
            colorList.Add(new color_data("darkseagreen", "#8fbc8f"));
            colorList.Add(new color_data("darkslateblue", "#483d8b"));
            colorList.Add(new color_data("darkslategray", "#2f4f4f"));
            colorList.Add(new color_data("darkslategrey", "#2f4f4f"));
            colorList.Add(new color_data("darkturquoise", "#00ced1"));
            colorList.Add(new color_data("darkviolet", "#9400d3"));
            colorList.Add(new color_data("deeppink", "#ff1493"));
            colorList.Add(new color_data("deepskyblue", "#00bfff"));
            colorList.Add(new color_data("dimgray", "#696969"));
            colorList.Add(new color_data("dimgrey", "#696969"));
            colorList.Add(new color_data("dodgerblue", "#1e90ff"));
            colorList.Add(new color_data("firebrick", "#b22222"));
            colorList.Add(new color_data("floralwhite", "#fffaf0"));
            colorList.Add(new color_data("forestgreen", "#228b22"));
            colorList.Add(new color_data("gainsboro", "#dcdcdc"));
            colorList.Add(new color_data("ghostwhite", "#f8f8ff"));
            colorList.Add(new color_data("gold", "#ffd700"));
            colorList.Add(new color_data("goldenrod", "#daa520"));
            colorList.Add(new color_data("green", "#008000"));
            colorList.Add(new color_data("greenyellow", "#adff2f"));
            colorList.Add(new color_data("grey", "#808080"));
            colorList.Add(new color_data("honeydew", "#f0fff0"));
            colorList.Add(new color_data("hotpink", "#ff69b4"));
            colorList.Add(new color_data("indianred", "#cd5c5c"));
            colorList.Add(new color_data("indigo", "#4b0082"));
            colorList.Add(new color_data("ivory", "#fffff0"));
            colorList.Add(new color_data("khaki", "#f0e68c"));
            colorList.Add(new color_data("lavender", "#e6e6fa"));
            colorList.Add(new color_data("lavenderblush", "#fff0f5"));
            colorList.Add(new color_data("lawngreen", "#7cfc00"));
            colorList.Add(new color_data("lemonchiffon", "#fffacd"));
            colorList.Add(new color_data("lightblue", "#add8e6"));
            colorList.Add(new color_data("lightcoral", "#f08080"));
            colorList.Add(new color_data("lightcyan", "#e0ffff"));
            colorList.Add(new color_data("lightgoldenrodyellow", "#fafad2"));
            colorList.Add(new color_data("lightgray", "#d3d3d3"));
            colorList.Add(new color_data("lightgreen", "#90ee90"));
            colorList.Add(new color_data("lightgrey", "#d3d3d3"));
            colorList.Add(new color_data("lightpink", "#ffb6c1"));
            colorList.Add(new color_data("lightsalmon", "#ffa07a"));
            colorList.Add(new color_data("lightseagreen", "#20b2aa"));
            colorList.Add(new color_data("lightskyblue", "#87cefa"));
            colorList.Add(new color_data("lightslategray", "#778899"));
            colorList.Add(new color_data("lightslategrey", "#778899"));
            colorList.Add(new color_data("lightsteelblue", "#b0c4de"));
            colorList.Add(new color_data("lightyellow", "#ffffe0"));
            colorList.Add(new color_data("lime", "#00ff00"));
            colorList.Add(new color_data("limegreen", "#32cd32"));
            colorList.Add(new color_data("linen", "#faf0e6"));
            colorList.Add(new color_data("magenta", "#ff00ff"));
            colorList.Add(new color_data("maroon", "#800000"));
            colorList.Add(new color_data("mediumaquamarine", "#66cdaa"));
            colorList.Add(new color_data("mediumblue", "#0000cd"));
            colorList.Add(new color_data("mediumorchid", "#ba55d3"));
            colorList.Add(new color_data("mediumpurple", "#9370db"));
            colorList.Add(new color_data("mediumseagreen", "#3cb371"));
            colorList.Add(new color_data("mediumslateblue", "#7b68ee"));
            colorList.Add(new color_data("mediumspringgreen", "#00fa9a"));
            colorList.Add(new color_data("mediumturquoise", "#48d1cc"));
            colorList.Add(new color_data("mediumvioletred", "#c71585"));
            colorList.Add(new color_data("midnightblue", "#191970"));
            colorList.Add(new color_data("mintcream", "#f5fffa"));
            colorList.Add(new color_data("mistyrose", "#ffe4e1"));
            colorList.Add(new color_data("moccasin", "#ffe4b5"));
            colorList.Add(new color_data("navajowhite", "#ffdead"));
            colorList.Add(new color_data("navy", "#000080"));
            colorList.Add(new color_data("oldlace", "#fdf5e6"));
            colorList.Add(new color_data("olive", "#808000"));
            colorList.Add(new color_data("olivedrab", "#6b8e23"));
            colorList.Add(new color_data("orange", "#ffa500"));
            colorList.Add(new color_data("orangered", "#ff4500"));
            colorList.Add(new color_data("orchid", "#da70d6"));
            colorList.Add(new color_data("palegoldenrod", "#eee8aa"));
            colorList.Add(new color_data("palegreen", "#98fb98"));
            colorList.Add(new color_data("paleturquoise", "#afeeee"));
            colorList.Add(new color_data("palevioletred", "#db7093"));
            colorList.Add(new color_data("papayawhip", "#ffefd5"));
            colorList.Add(new color_data("peachpuff", "#ffdab9"));
            colorList.Add(new color_data("peru", "#cd853f"));
            colorList.Add(new color_data("pink", "#ffc0cb"));
            colorList.Add(new color_data("plum", "#dda0dd"));
            colorList.Add(new color_data("powderblue", "#b0e0e6"));
            colorList.Add(new color_data("purple", "#800080"));
            colorList.Add(new color_data("rebeccapurple", "#663399"));
            colorList.Add(new color_data("red", "#ff0000"));
            colorList.Add(new color_data("rosybrown", "#bc8f8f"));
            colorList.Add(new color_data("royalblue", "#4169e1"));
            colorList.Add(new color_data("saddlebrown", "#8b4513"));
            colorList.Add(new color_data("salmon", "#fa8072"));
            colorList.Add(new color_data("sandybrown", "#f4a460"));
            colorList.Add(new color_data("seagreen", "#2e8b57"));
            colorList.Add(new color_data("seashell", "#fff5ee"));
            colorList.Add(new color_data("sienna", "#a0522d"));
            colorList.Add(new color_data("silver", "#c0c0c0"));
            colorList.Add(new color_data("skyblue", "#87ceeb"));
            colorList.Add(new color_data("slateblue", "#6a5acd"));
            colorList.Add(new color_data("slategray", "#708090"));
            colorList.Add(new color_data("slategrey", "#708090"));
            colorList.Add(new color_data("snow", "#fffafa"));
            colorList.Add(new color_data("springgreen", "#00ff7f"));
            colorList.Add(new color_data("steelblue", "#4682b4"));
            colorList.Add(new color_data("tan", "#d2b48c"));
            colorList.Add(new color_data("teal", "#008080"));
            colorList.Add(new color_data("thistle", "#d8bfd8"));
            colorList.Add(new color_data("tomato", "#ff6347"));
            colorList.Add(new color_data("transparent", "rgba(0,0,0,0)"));
            colorList.Add(new color_data("turquoise", "#40e0d0"));
            colorList.Add(new color_data("violet", "#ee82ee"));
            colorList.Add(new color_data("wheat", "#f5deb3"));
            colorList.Add(new color_data("white", "#ffffff"));
            colorList.Add(new color_data("whitesmoke", "#f5f5f5"));
            colorList.Add(new color_data("yellow", "#ffff00"));
            colorList.Add(new color_data("yellowgreen", "#9acd32"));
        }
    }

    [Serializable]
    public class color_data
    {
        public string name;
        private string colorHex;
        public Color MyColor;

        public color_data(string name, string colorHex)
        {
            this.name = name;
            this.colorHex = colorHex;
            ColorUtility.TryParseHtmlString(colorHex, out this.MyColor);
        }

        public string getColorHex() {
            colorHex = "#"+ ColorUtility.ToHtmlStringRGBA(MyColor);
            return colorHex; }
    }

    [Serializable]
    public class logPreset
    {
        public string name;
        public string textColor;
        public string backgroundColor;
        public int borderRadius;
        public int textSize;
        public FontStyle fontStyle;
    }
}
