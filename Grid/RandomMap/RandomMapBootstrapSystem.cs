using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct RandomMapBootstrapSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.HasSingleton<RandomMapState>())
            return;

        var entity = state.EntityManager.CreateEntity();

        state.EntityManager.AddComponentData(entity, new RandomMapState
        {
            Seed = 123456789u,
            PatchCount = 22,
            PatchRadiusMin = 2,
            PatchRadiusMax = 5,
            SafeRadius = 5,
            CorridorHalfWidth = 1,
            ScatterCount = 40,
            Generated = 0,
            Applied = 0
        });

        state.EntityManager.AddBuffer<RandomMapObstacleCell>(entity);
    }
}