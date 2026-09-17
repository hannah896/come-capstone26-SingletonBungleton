using UnityEngine;

/// <summary>
/// 플레이어가 설치한 구조물 루트에 붙는 표식. 설치 시 StructureSync(싱글) 또는 NetworkStructureDirector(멀티)가 런타임에 붙인다.
/// 멀티에서는 호스트가 발급한 NetworkId로 철거/줍기/보관함 요청 대상을 가리킨다.
/// </summary>
public class PlacedStructure : MonoBehaviour
{
    /// <summary>호스트가 발급한 구조물 ID. 0이면 네트워크로 관리되지 않는 구조물(싱글 설치 등).</summary>
    public int NetworkId { get; set; }

    /// <summary>설치한 아이템의 Addressable 키 (= ItemDataSO 이름).</summary>
    public string ItemKey { get; set; }
}
