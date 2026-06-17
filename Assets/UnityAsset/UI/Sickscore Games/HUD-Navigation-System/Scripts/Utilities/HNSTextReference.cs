using SickscoreGames.HUDNavigationSystem.Adapters;
using UnityEngine;
using UnityEngine.UI;

#if TMP_PRESENT
using TMPro;
#endif

namespace SickscoreGames.HUDNavigationSystem
{
    [DisallowMultipleComponent]
    public class HNSTextReference : MonoBehaviour
    {
        #region Variables
        [SerializeField] private Component _textComponent;

        private IHNSTextAdapter _adapter;
        #endregion


        #region Main Methods
        private void Awake()
        {
            Initialize();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
                Initialize();
        }
#endif

        private void Initialize()
        {
            // create adapter from component
            if (_textComponent != null)
            {
                _adapter = CreateAdapter(_textComponent);
                if (_adapter != null)
                    return;

                Debug.LogWarning($"[HNSTextReference] Assigned component is not supported: {_textComponent.GetType().Name}", this);
            }

            // try to auto-detect text component
            _textComponent = AutoDetectComponent();
            if (_textComponent != null)
            {
                _adapter = CreateAdapter(_textComponent);
                return;
            }

            _adapter = null;
        }
        #endregion


        #region Utility Methods
        private Component AutoDetectComponent()
        {
#if TMP_PRESENT
            // TextMeshPro
            if (HUDNavigationExtensions.TryGet(this, out TextMeshProUGUI tmp))
                return tmp;
#endif

            // UI Text (legacy)
            if (HUDNavigationExtensions.TryGet(this, out Text uiText))
                return uiText;

            // Third-party adapter
            var adapters = GetComponents<IHNSTextAdapter>();
            if (adapters.Length > 0)
                // ReSharper disable once SuspiciousTypeConversion.Global
                return adapters[0] as Component;

            return null;
        }

        private IHNSTextAdapter CreateAdapter(Component component)
        {
#if TMP_PRESENT
            if (component is TextMeshProUGUI tmp)
                return new HNSTextAdapter_TMP(tmp);
#endif

            if (component is Text uiText)
                return new HNSTextAdapter_UI(uiText);

            // ReSharper disable once SuspiciousTypeConversion.Global
            if (component is IHNSTextAdapter adapter)
                return adapter;

            return null;
        }
        #endregion


        #region Public API
        public void SetText(string value)
        {
            _adapter?.SetText(value);
        }

        public IHNSTextAdapter GetAdapter() => _adapter;

        public Component GetTextComponent() => _textComponent;

        public void ForceInitialize() => Initialize();
        #endregion
    }
}