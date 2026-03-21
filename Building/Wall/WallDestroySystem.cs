using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DamageEventSystem))]
public partial struct WallDestroySystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<GridOccupancy>();
        state.RequireForUpdate<WallIndexState>();
        state.RequireForUpdate<FlowFieldState>();
        state.RequireForUpdate<AttackSlotState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var gridEntity = SystemAPI.GetSingletonEntity<GridConfig>();
        var occ = SystemAPI.GetBuffer<OccCell>(gridEntity);
        var width = cfg.Size.x;

        var wallMap = SystemAPI.GetSingleton<WallIndexState>().Map;
        var slotStateRW = SystemAPI.GetSingletonRW<AttackSlotState>();
        var slotMap = slotStateRW.ValueRW.SlotOwnerMap;

        var flowDirty = false;
        using var deadWalls = new NativeList<Entity>(Allocator.Temp);

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

            deadWalls.Add(entity);
            flowDirty = true;
        }

        if (deadWalls.Length == 0)
            return;

        slotMap.Clear();

        foreach (var slotAssignment in SystemAPI.Query<RefRW<AttackSlotAssignment>>().WithAll<ZombieTag>())
        {
            var assignment = slotAssignment.ValueRW;
            if (assignment.HasSlot == 0 || assignment.Target == Entity.Null)
                continue;

            for (int i = 0; i < deadWalls.Length; i++)
            {
                if (assignment.Target != deadWalls[i])
                    continue;

                assignment.HasSlot = 0;
                assignment.Target = Entity.Null;
                assignment.SlotCell = default;
                assignment.SlotIndex = 0;
                slotAssignment.ValueRW = assignment;
                break;
            }
        }

        for (int i = 0; i < deadWalls.Length; i++)
            state.EntityManager.DestroyEntity(deadWalls[i]);

        if (flowDirty)
        {
            var flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
            var flow = SystemAPI.GetComponentRW<FlowFieldState>(flowEntity);
            flow.ValueRW.Dirty = 1;
        }
    }
}