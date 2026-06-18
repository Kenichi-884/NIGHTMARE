using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RoslynCSharp.Demo
{
    public sealed class ConsolePanel : MonoBehaviour
    {
        // Events
        public readonly UnityEvent OnVisibilityChanged = new();

        // Private
        private readonly List<TMP_Text> loggedMessages = new();

        [SerializeField]
        private GameObject root;
        [SerializeField]
        private Button clearButton;
        [SerializeField]
        private Button closeButton;
        [SerializeField]
        private TMP_Text messageTemplate;

        // Public
        public Color WarningColor = Color.yellow;
        public Color ErrorColor = Color.red;

        // Properties
        public bool IsShown => root.activeSelf;

        // Methods
        private void Awake()
        {
            clearButton.onClick.AddListener(Clear);
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

        public void Clear()
        {
            // Destroy the messages
            foreach(TMP_Text message in loggedMessages)
                Destroy(message.gameObject);

            // Clear list
            loggedMessages.Clear();
        }

        public void LogMessage(string text)
        {
            Log(text);
        }

        public void LogWarning(string text)
        {
            Log(text).color = WarningColor;
        }

        public void LogError(string text)
        {
            Log(text).color = ErrorColor;
        }

#if UNITY_EDITOR
        [ContextMenu("Test Log")]
        private void TestLog()
        {
            Log("Test Log Message");
        }
#endif

        private TMP_Text Log(string text)
        {
            // Create instance
            TMP_Text newMessage = Instantiate(messageTemplate);

            // Update parent
            newMessage.transform.SetParent(messageTemplate.transform.parent, false);

            // Enable object
            newMessage.gameObject.SetActive(true);

            // Add logged message
            loggedMessages.Add(newMessage);

            // Update text
            newMessage.text = text;
            return newMessage;
        }
    }
}
