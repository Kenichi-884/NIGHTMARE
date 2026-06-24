using UnityEngine;

// 子 GameObject (AudioSource 1つ) にアタッチして使う電話DSPフィルター。
// ・2段 HP + 2段 LP (24dB/oct) で帯域を急峻にカット
// ・1.5kHz ピークEQで電話らしい鼻声感を付加
// ・ソフトサチュレーション + ホワイトノイズヒス
[RequireComponent(typeof(AudioSource))]
public class PhoneVoiceFilter : MonoBehaviour
{
    [Header("帯域フィルター (2段 = 24dB/oct)")]
    [Range(200f, 1200f)]  public float highPassFreq = 1200f;
    [Range(1500f, 5000f)] public float lowPassFreq  = 4500f;

    [Header("ピークEQ (電話の鼻声感)")]
    [Range(500f, 3000f)]  public float peakFreq     = 1500f;
    [Range(0f, 12f)]      public float peakGainDB   = 10f;
    [Range(0.5f, 4f)]     public float peakQ        = 2f;

    [Header("歪み・ノイズ")]
    [Range(0f, 1f)]        public float saturation  = 1f;
    [Range(0f, 0.05f)]     public float noiseLevel  = 0.05f;
    [Range(0.5f, 2.5f)]    public float outputGain  = 2.5f;

    // ─── Biquad ステート (2段 HP + 2段 LP + PEQ, 各チャンネル) ──
    private float[] _hp1X1, _hp1X2, _hp1Y1, _hp1Y2; // HP 1段目
    private float[] _hp2X1, _hp2X2, _hp2Y1, _hp2Y2; // HP 2段目
    private float[] _lp1X1, _lp1X2, _lp1Y1, _lp1Y2; // LP 1段目
    private float[] _lp2X1, _lp2X2, _lp2Y1, _lp2Y2; // LP 2段目
    private float[] _peqX1, _peqX2, _peqY1, _peqY2;  // ピークEQ

    // ─── Biquad 係数 ──────────────────────────────────────────
    private float _hpB0, _hpB1, _hpB2, _hpA1, _hpA2;
    private float _lpB0, _lpB1, _lpB2, _lpA1, _lpA2;
    private float _pB0,  _pB1,  _pB2,  _pA1,  _pA2;

    // キャッシュ (変化検知用)
    private float _cHp, _cLp, _cPFreq, _cPGain, _cPQ;
    private int   _cachedSr;

    // ─── ノイズバッファ ────────────────────────────────────────
    private float[] _noise;
    private int     _noisePos;
    private int     _sampleRate;

    private void Awake()
    {
        _sampleRate = AudioSettings.outputSampleRate;

        // ノイズバッファ (1秒分, System.Random はスレッドセーフ)
        var rng = new System.Random(9999);
        _noise = new float[_sampleRate];
        for (int i = 0; i < _noise.Length; i++)
            _noise[i] = (float)(rng.NextDouble() * 2.0 - 1.0);

        // ステート配列を初期化
        int ch = 8;
        _hp1X1=new float[ch]; _hp1X2=new float[ch]; _hp1Y1=new float[ch]; _hp1Y2=new float[ch];
        _hp2X1=new float[ch]; _hp2X2=new float[ch]; _hp2Y1=new float[ch]; _hp2Y2=new float[ch];
        _lp1X1=new float[ch]; _lp1X2=new float[ch]; _lp1Y1=new float[ch]; _lp1Y2=new float[ch];
        _lp2X1=new float[ch]; _lp2X2=new float[ch]; _lp2Y1=new float[ch]; _lp2Y2=new float[ch];
        _peqX1=new float[ch]; _peqX2=new float[ch]; _peqY1=new float[ch]; _peqY2=new float[ch];
    }

    // PhoneCallSystem から再生タイミングに合わせて切り替える
    public bool isActive = false;

    // ─── DSP メインループ ─────────────────────────────────────
    private void OnAudioFilterRead(float[] data, int channels)
    {
        // 非アクティブ時は素通し（ノイズを乗せない）
        if (!isActive) return;

        // パラメーター変化時のみ係数を再計算
        if (_cachedSr == 0
            || Mathf.Abs(highPassFreq - _cHp)     > 0.5f
            || Mathf.Abs(lowPassFreq  - _cLp)     > 0.5f
            || Mathf.Abs(peakFreq     - _cPFreq)  > 0.5f
            || Mathf.Abs(peakGainDB   - _cPGain)  > 0.05f
            || Mathf.Abs(peakQ        - _cPQ)     > 0.01f)
        {
            ComputeCoeffs();
        }

        int   noiseLen = _noise.Length;
        float drive    = 1f + saturation * 5f;

        for (int i = 0; i < data.Length; i++)
        {
            int   c = i % channels;
            float x = data[i];

            // ── HP 1段目 ──
            float h1 = _hpB0*x   + _hpB1*_hp1X1[c] + _hpB2*_hp1X2[c]
                                  - _hpA1*_hp1Y1[c]  - _hpA2*_hp1Y2[c];
            _hp1X2[c]=_hp1X1[c]; _hp1X1[c]=x;
            _hp1Y2[c]=_hp1Y1[c]; _hp1Y1[c]=h1;

            // ── HP 2段目 (傾き2倍) ──
            float h2 = _hpB0*h1  + _hpB1*_hp2X1[c] + _hpB2*_hp2X2[c]
                                  - _hpA1*_hp2Y1[c]  - _hpA2*_hp2Y2[c];
            _hp2X2[c]=_hp2X1[c]; _hp2X1[c]=h1;
            _hp2Y2[c]=_hp2Y1[c]; _hp2Y1[c]=h2;

            // ── LP 1段目 ──
            float l1 = _lpB0*h2  + _lpB1*_lp1X1[c] + _lpB2*_lp1X2[c]
                                  - _lpA1*_lp1Y1[c]  - _lpA2*_lp1Y2[c];
            _lp1X2[c]=_lp1X1[c]; _lp1X1[c]=h2;
            _lp1Y2[c]=_lp1Y1[c]; _lp1Y1[c]=l1;

            // ── LP 2段目 (傾き2倍) ──
            float l2 = _lpB0*l1  + _lpB1*_lp2X1[c] + _lpB2*_lp2X2[c]
                                  - _lpA1*_lp2Y1[c]  - _lpA2*_lp2Y2[c];
            _lp2X2[c]=_lp2X1[c]; _lp2X1[c]=l1;
            _lp2Y2[c]=_lp2Y1[c]; _lp2Y1[c]=l2;

            // ── ピークEQ ──
            float pe = _pB0*l2   + _pB1*_peqX1[c]  + _pB2*_peqX2[c]
                                  - _pA1*_peqY1[c]   - _pA2*_peqY2[c];
            _peqX2[c]=_peqX1[c]; _peqX1[c]=l2;
            _peqY2[c]=_peqY1[c]; _peqY1[c]=pe;

            // ── ソフトサチュレーション ──
            float sat = pe * drive;
            float out_ = sat / (1f + Mathf.Abs(sat));

            // ── ホワイトノイズヒス ──
            float hiss = _noise[(_noisePos + i / channels) % noiseLen] * noiseLevel;

            data[i] = Mathf.Clamp((out_ + hiss) * outputGain, -1f, 1f);
        }

        _noisePos = (_noisePos + data.Length / channels) % noiseLen;
    }

    // ─── 係数計算 ─────────────────────────────────────────────

    private void ComputeCoeffs()
    {
        _cachedSr = _sampleRate;
        _cHp = highPassFreq; _cLp = lowPassFreq;
        _cPFreq = peakFreq;  _cPGain = peakGainDB; _cPQ = peakQ;

        const float Q = 0.7071f;
        CalcHP(highPassFreq, _sampleRate, Q,
            out _hpB0, out _hpB1, out _hpB2, out _hpA1, out _hpA2);
        CalcLP(lowPassFreq,  _sampleRate, Q,
            out _lpB0, out _lpB1, out _lpB2, out _lpA1, out _lpA2);
        CalcPeakEQ(peakFreq, _sampleRate, peakQ, peakGainDB,
            out _pB0, out _pB1, out _pB2, out _pA1, out _pA2);
    }

    // Butterworth 2次ハイパス
    private static void CalcHP(float fc, int sr, float q,
        out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float w = 2f * Mathf.PI * fc / sr;
        float cw = Mathf.Cos(w), sw = Mathf.Sin(w);
        float alpha = sw / (2f * q);
        float n = 1f + alpha;
        b0 =  (1f + cw) * 0.5f / n;
        b1 = -(1f + cw)        / n;
        b2 =  (1f + cw) * 0.5f / n;
        a1 = -2f * cw          / n;
        a2 =  (1f - alpha)     / n;
    }

    // Butterworth 2次ローパス
    private static void CalcLP(float fc, int sr, float q,
        out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float w = 2f * Mathf.PI * fc / sr;
        float cw = Mathf.Cos(w), sw = Mathf.Sin(w);
        float alpha = sw / (2f * q);
        float n = 1f + alpha;
        b0 =  (1f - cw) * 0.5f / n;
        b1 =  (1f - cw)        / n;
        b2 =  (1f - cw) * 0.5f / n;
        a1 = -2f * cw          / n;
        a2 =  (1f - alpha)     / n;
    }

    // ピーキングEQ
    private static void CalcPeakEQ(float fc, int sr, float q, float dBgain,
        out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float A  = Mathf.Pow(10f, dBgain / 40f);
        float w  = 2f * Mathf.PI * fc / sr;
        float cw = Mathf.Cos(w);
        float alpha = Mathf.Sin(w) / (2f * q);
        float n = 1f + alpha / A;
        b0 = (1f + alpha * A) / n;
        b1 = -2f * cw         / n;
        b2 = (1f - alpha * A) / n;
        a1 = -2f * cw         / n;
        a2 = (1f - alpha / A) / n;
    }
}
