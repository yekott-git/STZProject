using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(GameOverOnDeathSystem))]
public partial struct WallDestroySystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<GridOccupancy>();
        state.RequireForUpdate<WallIndexState>();
        state.RequireForUpdate<AttackSlotState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var gridEntity = SystemAPI.GetSingletonEntity<GridOccupancy>();
        var occ = SystemAPI.GetBuffer<OccCell>(gridEntity);
        var width = cfg.Size.x;

        var wallMap = SystemAPI.GetSingleton<WallIndexState>().Map;
        var slotStateRW = SystemAPI.GetSingletonRW<AttackSlotState>();
        var slotMap = slotStateRW.ValueRW.SlotOwnerMap;

        var flowDirty = false;
        using var deadBuildings = new NativeList<Entity>(Allocator.Temp);

        foreach (var (cell, hp, entity) in
                 SystemAPI.Query<RefRO<GridCell>, RefRO<Health>>()
                     .WithAll<BuildingTag, DestroyOnDeath>()
                     .WithEntityAccess())
        {
            if (hp.ValueRO.Value > 0)
                continue;

            var c = cell.ValueRO.Value;

            if ((uint)c.x < (uint)cfg.Size.x && (uint)c.y < (uint)cfg.Size.y)
            {
                var idx = c.y * width + c.x;
                occ[idx] = new OccCell { Value = 0 };
            }

            if (state.EntityManager.HasComponent<WallTag>(entity))
            {
                var key = GridKeyUtility.CellKey(c, width);
                wallMap.Remove(key);
                flowDirty = true;
            }

            deadBuildings.Add(entity);
        }

        if (deadBuildings.Length == 0)
            return;

        slotMap.Clear();

        foreach (var slotAssignment in SystemAPI.Query<RefRW<AttackSlotAssignment>>().WithAll<ZombieTag>())
        {
            var assignment = slotAssignment.ValueRW;
            if (assignment.HasSlot == 0 || assignment.Target == Entity.Null)
                continue;

            for (int i = 0; i < deadBuildings.Length; i++)
            {
                if (assignment.Target != deadBuildings[i])
                    continue;

                assignment.HasSlot = 0;
                assignment.Target = Entity.Null;
                assignment.SlotCell = default;
                assignment.SlotIndex = 0;
                slotAssignment.ValueRW = assignment;
                break;
            }
        }

        for (int i = 0; i < deadBuildings.Length; i++)
            state.EntityManager.DestroyEntity(deadBuildings[i]);

        if (flowDirty && SystemAPI.HasSingleton<FlowFieldState>())
        {
            var flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
            var flow = SystemAPI.GetComponentRW<FlowFieldState>(flowEntity);
            flow.ValueRW.Dirty = 1;
        }
    }
}