using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

public partial class WallDestroySystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GridConfig>();
        RequireForUpdate<GridOccupancy>();
        RequireForUpdate<WallIndexState>();
        RequireForUpdate<FlowFieldState>();
    }

    protected override void OnUpdate()
    {
        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var gridEntity = SystemAPI.GetSingletonEntity<GridConfig>();
        var occ = EntityManager.GetBuffer<OccCell>(gridEntity);
        int width = cfg.Size.x;

        var map = SystemAPI.GetSingleton<WallIndexState>().Map;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        bool dirtyNeeded = false;

        Entities
            .WithAll<WallTag>()
            .ForEach((Entity e, in GridCell cell, in Health hp) =>
            {
                if (hp.Value > 0)
                    return;

                int key = GridKeyUtility.CellKey(cell.Value, width);
                map.Remove(key);

                int idx = cell.Value.y * width + cell.Value.x;
                if ((uint)cell.Value.x < (uint)cfg.Size.x && (uint)cell.Value.y < (uint)cfg.Size.y)
                {
                    occ[idx] = new OccCell { Value = 0 };
                }

                dirtyNeeded = true;
                ecb.DestroyEntity(e);
            })
            .WithoutBurst()
            .Run();

        ecb.Playback(EntityManager);
        ecb.Dispose();

        if (dirtyNeeded)
        {
            Entity flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
            var flow = SystemAPI.GetComponentRW<FlowFieldState>(flowEntity);
            flow.ValueRW.Dirty = 1;
        }
    }
}