using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StoryData", menuName = "Scriptable Objects/TestWorld/StoryData")]
public class StoryData : ScriptableObject
{
    [Header("Story Info")]
    [SerializeField] private string storyName;    // "default adventure" 등 스토리 이름
    public string StoryName => storyName;

    [Header("--- Fixed Regions ---")]
    [Tooltip("고정 Task")]
    [SerializeField] private List<RegionData> fixedRegions;
    public List<RegionData> FixedRegions => fixedRegions; // 고정 구역 (순서대로)

    [Header("--- Side Regions ---")]
    [Tooltip("랜덤 Task")]
    [SerializeField] private List<RegionData> sideRegions; // 서브 구역 (랜덤)
    public List<RegionData> SideRegions => sideRegions;

    [Header("Starting Conditions")]
    [SerializeField] private RegionData startRegion; // 시작 구역
    public RegionData StartRegion => startRegion;
}