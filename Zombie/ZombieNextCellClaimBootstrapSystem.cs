using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct ZombieNextCellClaimBootstrapSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
    }

    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.HasSingleton<ZombieNextCellClaimState>())
        {
            state.Enabled = false;
            return;
        }

        var entity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(entity, new ZombieNextCellClaimState
        {
            Map = new NativeParallelHashMap<int, Entity>(1024, Allocator.Persistent)
        });

        state.Enabled = false;
    }
}