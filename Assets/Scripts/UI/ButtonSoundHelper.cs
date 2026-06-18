using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Button にアタッチするだけでクリック音を再生するコンポーネント。
/// Inspector で soundKey を変えれば任意の SFX キーを使える。
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonSoundHelper : MonoBehaviour
{
    [SerializeField] private string soundKey = "button_click";

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => AudioManager.Instance?.Play(soundKey));
    }
}
