using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StoryData", menuName = "Scriptable Objects/TestWorld/Story Data")]
public class StoryData : ScriptableObject       
{
    [Header("Story Info")]
    public string StoryName;    // "default adventure" 등 스토리 이름

    
    //public string StartRegionID;

    [Header("--- Fixed Regions ---")]
    public List<RegionData> FixedRegions; // 고정 구역 (순서대로)
    [Header("--- Side Regions ---")]
    public List<RegionData> SideRegions; // 서브 구역 (랜덤)
    [Range(0f, 2f)]
    public float SideRegionRatio = 0.5f; // 기본값 50%
    [Header("Starting Conditions")]
    public RegionData StartRegion; // 시작 구역
}