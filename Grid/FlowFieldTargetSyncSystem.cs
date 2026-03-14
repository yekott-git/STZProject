using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct FlowFieldTargetSyncSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<FlowFieldState>();
        state.RequireForUpdate<CoreTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        GridConfig cfg = SystemAPI.GetSingleton<GridConfig>();
        Entity flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
        RefRW<FlowFieldState> flow = SystemAPI.GetComponentRW<FlowFieldState>(flowEntity);

        foreach (var tr in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<CoreTag>())
        {
            int2 coreCell = IsoGridUtility.WorldToGrid(cfg, tr.ValueRO.Position.xy);

            if (!coreCell.Equals(flow.ValueRO.TargetCell))
            {
                flow.ValueRW.TargetCell = coreCell;
                flow.ValueRW.Dirty = 1;
            }

            break;
        }
    }
}
