using UnityEngine.Events;
using UnityEngine;
using UnityEngine.UI;

namespace RoslynCSharp.Demo
{
    public sealed class CodeEditorPanel : MonoBehaviour
    {
        // Events
        public readonly UnityEvent OnVisibilityChanged = new();
        public readonly UnityEvent OnRunCode = new();

        // Private
        [SerializeField]
        private GameObject root;
        [SerializeField]
        private Button runButton;
        [SerializeField]
        private Button closeButton;

        // Public
        public Color WarningColor = Color.yellow;
        public Color ErrorColor = Color.red;

        // Properties
        public bool IsShown => root.activeSelf;

        // Methods
        private void Awake()
        {
            runButton.onClick.AddListener(() => OnRunCode.Invoke());
            closeButton.onClick.AddListener(Hide);
        }

        public void Show()
        {
            root.SetActive(true);

            // Trigger event
            OnVisibilityChanged.Invoke();
        }

        public void Hide()
        {
            root.SetActive(false);

            // Trigger event
            OnVisibilityChanged.Invoke();
        }
    }
}
