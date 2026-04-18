using Unity.Collections;
using Unity.Entities;

public struct ZombieNextCellClaimState : ICleanupComponentData
{
    public NativeParallelHashMap<int, Entity> Map;
}