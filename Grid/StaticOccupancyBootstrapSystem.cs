using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct StaticOccupancyBootstrapSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var gridEntity = SystemAPI.GetSingletonEntity<GridConfig>();
        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var cellCount = cfg.Size.x * cfg.Size.y;

        if (!state.EntityManager.HasComponent<StaticOccupancy>(gridEntity))
            state.EntityManager.AddComponent<StaticOccupancy>(gridEntity);

        if (!state.EntityManager.HasBuffer<StaticOccCell>(gridEntity))
        {
            var buffer = state.EntityManager.AddBuffer<StaticOccCell>(gridEntity);
            buffer.ResizeUninitialized(cellCount);

            for (int i = 0; i < cellCount; i++)
                buffer[i] = new StaticOccCell { Value = 0 };
        }

        state.Enabled = false;
    }
}