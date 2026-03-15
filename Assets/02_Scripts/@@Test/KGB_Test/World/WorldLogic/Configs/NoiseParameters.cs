using UnityEngine;

[CreateAssetMenu(fileName = "NoiseParameters", menuName = "Scriptable Objects/NoiseParameters")]
public class NoiseParameters : ScriptableObject
{
    
    [Tooltip("지역 안의 최소 봉우리 횟수 ")]
    public float MinBumps;

    [Tooltip("지역 안의 봉우리 최대 횟수")]
    public float MaxBumps;

    [Tooltip("지형의 굴곡이 최대 몇 블록 높이까지 생기는가?")]
    public float HeightVarianceBlocks;

    [Header("Fractal Noise (fBm) Settings")]
    [Tooltip("노이즈 겹침 횟수 (1이면 매끄러움, 높을수록 원래의 노이즈 값 안에서 요동침.)")]
    public int Octaves;

    [Tooltip("다음 옥타브의 진폭(영향력) 감소 비율 (기본 0.4~0.5)")]
    public float Persistence;

    [Tooltip("다음 옥타브의 주파수(촘촘함) 증가 비율 (기본 1.5~2.0)")]
    public float Lacunarity;


}
