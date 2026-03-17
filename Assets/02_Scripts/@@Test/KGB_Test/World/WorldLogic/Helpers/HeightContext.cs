using UnityEngine;

public struct HeightContext
{
    // 노이즈 연산용 좌표와 오프셋
    public float BaseSampleX;
    public float BaseSampleY;
    public Vector2[] OctaveOffsets;

    // 높이 및 환경 데이터
    public float TargetBaseHeight;
    public float MinHeight;
    public float DistToEdge;
    public float EdgeFade;
    public float CoastlineInfluence;
    public float EdgeInfluence;

    public NoiseParameters NoiseParams;
}
