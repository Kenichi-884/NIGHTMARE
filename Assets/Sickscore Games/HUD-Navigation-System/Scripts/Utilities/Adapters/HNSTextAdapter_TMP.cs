#if TMP_PRESENT
using TMPro;

namespace SickscoreGames.HUDNavigationSystem.Adapters
{
    public class HNSTextAdapter_TMP : IHNSTextAdapter
    {
        private TextMeshProUGUI _text;

        public HNSTextAdapter_TMP(TextMeshProUGUI text)
        {
            _text = text;
        }

        public void SetText(string text)
        {
            _text.text = text;
        }
    }
}
#endif