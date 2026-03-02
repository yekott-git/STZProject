using Unity.Entities;
using Unity.Collections;

public struct WallIndex : IComponentData { }

// Persistent Native container를 담는 싱글톤 상태
public struct WallIndexState : ICleanupComponentData
{
    public NativeParallelHashMap<int, Entity> Map;
}