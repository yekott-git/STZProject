using Unity.Entities;
using Unity.Collections;

public struct GridOccupancy : IComponentData
{
    // DynamicBuffer로 실제 데이터 저장 (Size.x * Size.y)
    // 0: empty, 1: blocked
}

public struct OccCell : IBufferElementData
{
    public byte Value;
}