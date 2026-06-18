using chrisGameDev.VisualConsole;
using UnityEngine;

namespace chrisGameDev.VisualConsole
{
    public class VisualConsole_loader : MonoBehaviour
    {
        [SerializeField]
        private VisualConsole_SO VisualConsoleScriptableObject;

        public void Awake()
        {
            VisualConsole_SO.loadSingleton(VisualConsoleScriptableObject);
        }
    }
}
