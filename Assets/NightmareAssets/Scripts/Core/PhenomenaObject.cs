using UnityEngine;
using System.Collections;

/// <summary>
/// 怪奇現象が発生したとき、対応する 3D シーンオブジェクトをアクティブ化する。
///
/// 使い方:
///   1. 現象を演出したいエリアに空の GameObject を作る（スクリプト用ルート）
///   2. その子に実際のビジュアル（モデル・パーティクル・ライト等）を配置し "Visual" 等の名前を付ける
///   3. このコンポーネントを追加し、triggerType と visualRoot を設定する
///   4. 実行中に対応する PhenomenaType が発生すると visualRoot が activeDuration 秒間アクティブになる
///
/// カメラ暗転中は映像自体が映らないため、特別な制御は不要。
/// </summary>
public class PhenomenaObject : MonoBehaviour
{
    [Header("トリガー設定")]
    [Tooltip("この PhenomenaType が発生したときにアクティブになる")]
    [SerializeField] private PhenomenaType triggerType;

    [Tooltip("アクティブを維持する秒数（0 = PhenomenaManager 依存）")]
    [SerializeField] private float activeDuration = 5f;

    [Tooltip("ランダムに duration をばらつかせる幅（±）")]
    [SerializeField] private float durationVariance = 1f;

    [Header("ビジュアルルート")]
    [Tooltip("アクティブ化する GameObject。未設定の場合は子オブジェクト全て")]
    [SerializeField] private GameObject visualRoot;

    [Header("フェード（Renderer を持つ場合）")]
    [Tooltip("フェードイン/アウトの秒数。0 = 即時切替")]
    [SerializeField] private float fadeTime = 0.25f;

    [Header("デバッグ / 配置確認")]
    [SerializeField] private FacilityLocation associatedLocation;

    // ─────────────────────────────────────────────────────────────

    private Renderer[]      _renderers;
    private ParticleSystem[]_particles;
    private Light[]         _lights;
    private Coroutine       _routine;
    private bool            _fadeSupported;

    private void Start()
    {
        // visualRoot が未設定の場合は直接の子を全てまとめた仮想ルートとして扱う
        GameObject target = visualRoot != null ? visualRoot : gameObject;

        _renderers = target.GetComponentsInChildren<Renderer>(true);
        _particles = target.GetComponentsInChildren<ParticleSystem>(true);
        _lights    = target.GetComponentsInChildren<Light>(true);
        _fadeSupported = fadeTime > 0f && _renderers.Length > 0;

        // 初期状態: 非表示
        SetVisible(false);

        if (PhenomenaManager.Instance != null)
            PhenomenaManager.Instance.OnPhenomenaTriggered += OnTriggered;
        else
            Debug.LogWarning($"[PhenomenaObject] PhenomenaManager が見つかりません ({name})");
    }

    private void OnDestroy()
    {
        if (PhenomenaManager.Instance != null)
            PhenomenaManager.Instance.OnPhenomenaTriggered -= OnTriggered;
    }

    // ─────────────────────────────────────────────────────────────

    private void OnTriggered(PhenomenaType type)
    {
        if (type != triggerType) return;

        // 既に再生中なら最初からやり直す
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ActiveRoutine());
    }

    private IEnumerator ActiveRoutine()
    {
        float dur = activeDuration + Random.Range(-durationVariance, durationVariance);
        dur = Mathf.Max(0.5f, dur);

        // ─ フェードイン ─
        if (_fadeSupported)
        {
            SetMaterialAlpha(0f);
            SetVisible(true);
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                SetMaterialAlpha(Mathf.Clamp01(t / fadeTime));
                yield return null;
            }
            SetMaterialAlpha(1f);
        }
        else
        {
            SetVisible(true);
        }

        // ─ アクティブ維持 ─
        yield return new WaitForSeconds(dur - fadeTime * 2f);

        // ─ フェードアウト ─
        if (_fadeSupported)
        {
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                SetMaterialAlpha(Mathf.Clamp01(1f - t / fadeTime));
                yield return null;
            }
        }

        SetVisible(false);
        _routine = null;
    }

    // ─── ビジュアル制御 ───────────────────────────────────────────

    private void SetVisible(bool on)
    {
        foreach (var r in _renderers)  r.enabled = on;
        foreach (var l in _lights)     l.enabled = on;

        foreach (var ps in _particles)
        {
            if (on) { ps.gameObject.SetActive(true); ps.Play(); }
            else    { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        }
    }

    private void SetMaterialAlpha(float alpha)
    {
        foreach (var r in _renderers)
        {
            // Standard / URP Lit / Unlit どちらでも動くよう Color プロパティを直接操作
            foreach (var mat in r.materials)
            {
                if (mat.HasProperty("_Color"))
                {
                    var c = mat.color; c.a = alpha; mat.color = c;
                }
                else if (mat.HasProperty("_BaseColor"))
                {
                    var c = mat.GetColor("_BaseColor"); c.a = alpha; mat.SetColor("_BaseColor", c);
                }
            }
        }
    }

    // ─── エディタ補助 ─────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Color col = triggerType switch
        {
            PhenomenaType.CameraFlicker    => new Color(0.3f, 0.8f, 1f),
            PhenomenaType.PowerFluctuation => new Color(1f, 0.8f, 0.1f),
            PhenomenaType.LightFailure     => new Color(0.2f, 0.2f, 0.8f),
            PhenomenaType.AudioDistortion  => new Color(0.8f, 0.4f, 0.8f),
            PhenomenaType.TimeWarp         => new Color(0.2f, 1f, 0.6f),
            PhenomenaType.GhostSignal      => new Color(0.9f, 0.9f, 1f),
            PhenomenaType.Blackout         => new Color(0.1f, 0.1f, 0.1f),
            PhenomenaType.TemperatureDrop  => new Color(0.5f, 0.8f, 1f),
            PhenomenaType.DoorBang         => new Color(1f, 0.4f, 0.1f),
            PhenomenaType.StaticBurst      => new Color(1f, 1f, 0.2f),
            _ => Color.white
        };
        Gizmos.color = col;
        Gizmos.DrawWireSphere(transform.position, 0.35f);

        var style = new GUIStyle { fontSize = 9 };
        style.normal.textColor = col;
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.55f,
            $"{triggerType}\n{associatedLocation}\n{activeDuration}s",
            style);
    }
#endif
}
