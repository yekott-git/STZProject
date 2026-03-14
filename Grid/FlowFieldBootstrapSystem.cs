using Unity.Collections;
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
        var e = state.EntityManager.CreateEntity();

        state.EntityManager.AddComponentData(e, new FlowFieldState
        {
            TargetCell = int2.zero,
            Dirty = 1
        });

        var buf = state.EntityManager.AddBuffer<FlowFieldCell>(e);
        int cellCount = cfg.Size.x * cfg.Size.y;
        buf.ResizeUninitialized(cellCount);

        for (int i = 0; i < cellCount; i++)
        {
            buf[i] = new FlowFieldCell
            {
                Cost = 1,
                Integration = ushort.MaxValue,
                DirX = 0,
                DirY = 0
            };
        }
    }
}