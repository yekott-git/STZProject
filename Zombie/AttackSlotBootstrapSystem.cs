using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct AttackSlotBootstrapSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        var entity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(entity, new AttackSlotState
        {
            SlotOwnerMap = new NativeParallelHashMap<int, Entity>(256, Allocator.Persistent)
        });
    }

    public void OnDestroy(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingletonRW<AttackSlotState>(out var slotState))
            return;

        if (slotState.ValueRO.SlotOwnerMap.IsCreated)
            slotState.ValueRW.SlotOwnerMap.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
    }
}