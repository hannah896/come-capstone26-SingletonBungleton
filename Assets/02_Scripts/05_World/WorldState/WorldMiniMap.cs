using System;

/// <summary>
/// 이전 씬/프리팹이 Missing Script가 되지 않도록 남겨 둔 호환 컴포넌트다.
/// 새 코드에서는 <see cref="WorldMap"/>을 사용한다.
/// </summary>
[Obsolete("WorldMiniMap has been replaced by WorldMap. Use WorldMap instead.")]
public sealed class WorldMiniMap : WorldMap
{
}
