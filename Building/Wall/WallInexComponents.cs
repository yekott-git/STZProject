using Unity.Entities;
using Unity.Collections;

public struct WallIndex : IComponentData
{
}

// 과도기 1차:
// 이름은 유지하지만 실제 역할은 "방어 구조물 맵".
// 벽 / 포탑 / 코어 / 이후 바리케이드까지 셀 -> 엔티티 조회용으로 사용.
public struct WallIndexState : ICleanupComponentData
{
    public NativeParallelHashMap<int, Entity> Map;
}

public struct DefenseStructureTag : IComponentData
{
}

public struct DefenseTargetPriority : IComponentData
{
    public byte Value;
}