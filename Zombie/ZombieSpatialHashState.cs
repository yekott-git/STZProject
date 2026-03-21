using Unity.Collections;
using Unity.Entities;

public struct ZombieSpatialHashTag : IComponentData
{
}

public struct ZombieSpatialHashState : ICleanupComponentData
{
    public NativeParallelMultiHashMap<int, Entity> Map;
}