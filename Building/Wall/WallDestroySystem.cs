using Unity.Collections;
using Unity.Entities;

public partial struct WallDestroySystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<GridOccupancy>();
        state.RequireForUpdate<WallIndexState>();
        state.RequireForUpdate<FlowFieldState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var gridEntity = SystemAPI.GetSingletonEntity<GridConfig>();
        var occ = SystemAPI.GetBuffer<OccCell>(gridEntity);
        var width = cfg.Size.x;

        var wallMap = SystemAPI.GetSingleton<WallIndexState>().Map;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        var flowDirty = false;

        foreach (var (cell, hp, entity) in
                 SystemAPI.Query<RefRO<GridCell>, RefRO<Health>>()
                     .WithAll<WallTag>()
                     .WithEntityAccess())
        {
            if (hp.ValueRO.Value > 0)
                continue;

            var c = cell.ValueRO.Value;
            var key = GridKeyUtility.CellKey(c, width);
            wallMap.Remove(key);

            if ((uint)c.x < (uint)cfg.Size.x && (uint)c.y < (uint)cfg.Size.y)
            {
                var idx = c.y * width + c.x;
                occ[idx] = new OccCell { Value = 0 };
            }

            flowDirty = true;
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();

        if (flowDirty)
        {
            var flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
            var flow = SystemAPI.GetComponentRW<FlowFieldState>(flowEntity);
            flow.ValueRW.Dirty = 1;
        }
    }
}