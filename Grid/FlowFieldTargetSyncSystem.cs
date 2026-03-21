using Unity.Entities;
using Unity.Mathematics;

public partial struct FlowFieldTargetSyncSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<FlowFieldState>();
        state.RequireForUpdate<CoreTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
        var flow = SystemAPI.GetComponentRW<FlowFieldState>(flowEntity);

        foreach (var coreCell in SystemAPI.Query<RefRO<GridCell>>().WithAll<CoreTag>())
        {
            var targetCell = coreCell.ValueRO.Value;

            if (!targetCell.Equals(flow.ValueRO.TargetCell))
            {
                flow.ValueRW.TargetCell = targetCell;
                flow.ValueRW.Dirty = 1;
            }

            break;
        }
    }
}