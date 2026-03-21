using Unity.Collections;
using Unity.Entities;

public struct AttackSlotState : IComponentData
{
    public NativeParallelHashMap<int, Entity> SlotOwnerMap;
}