using Unity.Collections;
using Unity.Entities;

public partial struct ZombieSpatialHashBootstrapSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        if (SystemAPI.HasSingleton<ZombieSpatialHashTag>())
            return;

        var entity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponent<ZombieSpatialHashTag>(entity);
        state.EntityManager.AddComponentData(entity, new ZombieSpatialHashState
        {
            Map = new NativeParallelMultiHashMap<int, Entity>(4096, Allocator.Persistent)
        });
    }

    public void OnDestroy(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<ZombieSpatialHashTag>())
            return;

        var hashEntity = SystemAPI.GetSingletonEntity<ZombieSpatialHashTag>();
        var hashState = state.EntityManager.GetComponentData<ZombieSpatialHashState>(hashEntity);

        if (hashState.Map.IsCreated)
            hashState.Map.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
    }
}