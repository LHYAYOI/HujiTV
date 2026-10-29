using UnityEngine;

/// <summary>
/// ドアが開いたときに流れ出す霧の見た目。DoorAutoOpen に差すと、Play 時にドアの足元へ床霧と煙パーティクルを作る。
/// 同じ見た目を複数のドアで使い回せる。Play 中に値を変えるとすぐ反映される。
/// </summary>
[CreateAssetMenu(fileName = "DoorFog", menuName = "hatake/Door Fog Profile", order = 0)]
public sealed class DoorFogProfile : ScriptableObject
{
    [Header("床霧（ボリューム）")]
    [Tooltip("床を這う霧を出す。")]
    public bool m_groundFogFlag = true;
    [Range(0, 5), Tooltip("開ききったときの濃さ（1mあたり）。")]
    public float m_fogDensity = 1.5f;
    [Tooltip("霧の色。逆光で光る色はライト側の色も効く。")]
    public Color m_fogTint = new Color(0.8f, 0.87f, 1f);
    [Min(0.1f), Tooltip("霧が出る範囲の幅（m、ドアと平行）。")]
    public float m_areaWidth = 6;
    [Min(0.1f), Tooltip("霧が出る範囲の奥行き（m、ドアから流れ出す向き）。")]
    public float m_areaLength = 10;
    [Min(0.1f), Tooltip("霧の範囲の高さ（m）。実際に溜まる高さは Fill で決める。")]
    public float m_areaHeight = 1;
    [Range(0.05f, 1), Tooltip("範囲の高さのうち霧が溜まる割合。")]
    public float m_fill = 0.45f;
    [Range(0.01f, 1), Tooltip("霧の上端のぼかし幅。")]
    public float m_softness = 0.25f;
    [Min(0.01f), Tooltip("もこもこの細かさ。1mあたりの山の数。")]
    public float m_noiseScale = 1.4f;
    [Range(0, 1), Tooltip("上端の凸凹と内部のむらの強さ。")]
    public float m_noiseAmount = 0.75f;
    [Tooltip("模様がドアから流れ出す速さ（m/s）。")]
    public float m_flowSpeed = 0.35f;
    [Range(0.01f, 1), Tooltip("範囲の左右と奥の端のぼかし。")]
    public float m_edgeFade = 0.25f;
    [Min(0), Tooltip("他の霧と枠を取り合うときの優先度。")]
    public float m_priority = 2;

    [Header("広がり方")]
    [Min(0.1f), Tooltip("最大まで広がる半径（m）。")]
    public float m_maxRadius = 7;
    [Min(0.1f), Tooltip("最大まで広がる秒数。最初は速く、だんだん遅くなる。")]
    public float m_spreadSeconds = 4;
    [Min(0.01f), Tooltip("広がりの先端のぼかし幅（m）。")]
    public float m_spreadEdge = 1.2f;
    [Min(0), Tooltip("濃さが立ち上がる秒数。")]
    public float m_fadeInSeconds = 0.4f;
    [Min(0.01f), Tooltip("ドアが閉じてから霧が消える秒数。")]
    public float m_fadeOutSeconds = 3;

    [Header("煙パーティクル")]
    [Tooltip("ドアの隙間から押し出される煙を出す。")]
    public bool m_particlesFlag = true;
    [Tooltip("煙のマテリアル（custom/hatake/SmokePuff）。未設定ならシェーダーから作る。")]
    public Material m_particleMaterial;
    [Min(0), Tooltip("開いた直後の1秒あたりの粒の数。")]
    public float m_burstRate = 45;
    [Range(0, 1), Tooltip("開きっぱなしのとき、開いた直後に対して出し続ける割合。")]
    public float m_sustainRatio = 0.2f;
    [Min(0.01f), Tooltip("開いた直後の勢いが落ち着くまでの秒数。")]
    public float m_burstSeconds = 1.2f;
    public LightShaftLab.DoorSmokeParticles.Settings m_particles = LightShaftLab.DoorSmokeParticles.Settings.Default;

    /// <summary>値が変わったとき（Play 中の調整用）。</summary>
    public event System.Action Changed;

    private void OnValidate()
    {
        Changed?.Invoke();
    }
}
