using UnityEngine.UI;

namespace SickscoreGames.HUDNavigationSystem.Adapters
{
    public class HNSTextAdapter_UI : IHNSTextAdapter
    {
        private Text _text;

        public HNSTextAdapter_UI(Text text)
        {
            _text = text;
        }

        public void SetText(string text)
        {
            _text.text = text;
        }
    }
}
