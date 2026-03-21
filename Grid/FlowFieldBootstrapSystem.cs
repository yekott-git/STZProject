using Unity.Entities;
using Unity.Mathematics;

public partial struct FlowFieldBootstrapSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.HasSingleton<FlowFieldState>())
            return;

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var entity = state.EntityManager.CreateEntity();

        state.EntityManager.AddComponentData(entity, new FlowFieldState
        {
            TargetCell = int2.zero,
            Dirty = 1
        });

        var buffer = state.EntityManager.AddBuffer<FlowFieldCell>(entity);
        var cellCount = cfg.Size.x * cfg.Size.y;
        buffer.ResizeUninitialized(cellCount);

        for (var i = 0; i < cellCount; i++)
        {
            buffer[i] = new FlowFieldCell
            {
                Cost = 1,
                Integration = ushort.MaxValue,
                DirX = 0,
                DirY = 0
            };
        }
    }
}