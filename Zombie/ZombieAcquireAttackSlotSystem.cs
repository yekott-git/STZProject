using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ZombieSpatialHashBuildSystem))]
[UpdateBefore(typeof(ZombieSeparationSystem))]
public partial struct ZombieAcquireAttackSlotSystem : ISystem
{
    ComponentLookup<GridCell> gridCellLookup;
    ComponentLookup<AttackSlotConfig> slotConfigLookup;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<GridOccupancy>();
        state.RequireForUpdate<AttackSlotState>();

        gridCellLookup = state.GetComponentLookup<GridCell>(true);
        slotConfigLookup = state.GetComponentLookup<AttackSlotConfig>(true);
    }

    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        gridCellLookup.Update(ref state);
        slotConfigLookup.Update(ref state);

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var width = cfg.Size.x;

        var gridEntity = SystemAPI.GetSingletonEntity<GridConfig>();
        var occ = SystemAPI.GetBuffer<OccCell>(gridEntity).AsNativeArray();

        var wallMap = SystemAPI.GetSingleton<WallIndexState>().Map;

        var slotStateRW = SystemAPI.GetSingletonRW<AttackSlotState>();
        var slotMap = slotStateRW.ValueRW.SlotOwnerMap;
        slotMap.Clear();

        var zombieQuery = SystemAPI.QueryBuilder()
            .WithAll<ZombieTag, LocalTransform, AttackSlotAssignment>()
            .Build();

        var zombieCount = zombieQuery.CalculateEntityCount();
        var desiredCapacity = math.max(256, zombieCount * 2);
        if (slotMap.Capacity < desiredCapacity)
            slotMap.Capacity = desiredCapacity;

        foreach (var (tr, slotAssignment, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRW<AttackSlotAssignment>>()
                     .WithAll<ZombieTag>()
                     .WithEntityAccess())
        {
            var assignment = slotAssignment.ValueRW;
            assignment.HasSlot = 0;
            assignment.Target = Entity.Null;

            var worldPos = tr.ValueRO.Position.xy;
            var zombieCell = IsoGridUtility.WorldToGrid(cfg, worldPos);

            Entity bestTarget = Entity.Null;
            int2 bestSlotCell = default;
            byte bestSlotIndex = 0;
            float bestDistSq = float.MaxValue;

            if (TryReclaimCurrentSlot(
                    cfg,
                    width,
                    occ,
                    wallMap,
                    slotMap,
                    zombieCell,
                    entity,
                    slotAssignment.ValueRO,
                    ref bestTarget,
                    ref bestSlotCell,
                    ref bestSlotIndex,
                    ref bestDistSq))
            {
                assignment.HasSlot = 1;
                assignment.Target = bestTarget;
                assignment.SlotCell = bestSlotCell;
                assignment.SlotIndex = bestSlotIndex;
                slotAssignment.ValueRW = assignment;
                continue;
            }

            const int searchRadiusCells = 2;

            for (int y = -searchRadiusCells; y <= searchRadiusCells; y++)
            {
                for (int x = -searchRadiusCells; x <= searchRadiusCells; x++)
                {
                    var targetCell = zombieCell + new int2(x, y);
                    if (!IsoGridUtility.InBounds(cfg, targetCell))
                        continue;

                    var key = GridKeyUtility.CellKey(targetCell, width);

                    if (!wallMap.TryGetValue(key, out var targetEntity))
                        continue;

                    if (!slotConfigLookup.HasComponent(targetEntity))
                        continue;

                    TryConsiderTargetSlots(
                        cfg,
                        width,
                        occ,
                        wallMap,
                        slotMap,
                        targetCell,
                        targetEntity,
                        slotConfigLookup[targetEntity],
                        zombieCell,
                        ref bestTarget,
                        ref bestSlotCell,
                        ref bestSlotIndex,
                        ref bestDistSq);
                }
            }

            if (bestTarget != Entity.Null)
            {
                var slotKey = AttackSlotUtility.MakeSlotKey(bestTarget, bestSlotIndex);
                slotMap.TryAdd(slotKey, entity);

                assignment.HasSlot = 1;
                assignment.Target = bestTarget;
                assignment.SlotCell = bestSlotCell;
                assignment.SlotIndex = bestSlotIndex;
            }

            slotAssignment.ValueRW = assignment;
        }
    }

    bool TryReclaimCurrentSlot(
        GridConfig cfg,
        int width,
        NativeArray<OccCell> occ,
        NativeParallelHashMap<int, Entity> wallMap,
        NativeParallelHashMap<int, Entity> slotMap,
        int2 zombieCell,
        Entity zombieEntity,
        AttackSlotAssignment oldAssignment,
        ref Entity bestTarget,
        ref int2 bestSlotCell,
        ref byte bestSlotIndex,
        ref float bestDistSq)
    {
        if (oldAssignment.HasSlot == 0)
            return false;

        if (oldAssignment.Target == Entity.Null)
            return false;

        if (!gridCellLookup.HasComponent(oldAssignment.Target))
            return false;

        if (!slotConfigLookup.HasComponent(oldAssignment.Target))
            return false;

        var targetCell = gridCellLookup[oldAssignment.Target].Value;
        var slotConfig = slotConfigLookup[oldAssignment.Target];

        if (!AttackSlotUtility.IsAdjacentForPattern(slotConfig.Pattern, targetCell, oldAssignment.SlotCell))
            return false;

        if (!IsSlotCellValid(cfg, width, occ, wallMap, oldAssignment.SlotCell))
            return false;

        if (!IsSlotReachableSide(targetCell, oldAssignment.SlotCell, zombieCell))
            return false;

        var slotKey = AttackSlotUtility.MakeSlotKey(oldAssignment.Target, oldAssignment.SlotIndex);
        if (slotMap.ContainsKey(slotKey))
            return false;

        var delta = oldAssignment.SlotCell - zombieCell;
        var distSq = math.lengthsq(new float2(delta.x, delta.y));

        slotMap.TryAdd(slotKey, zombieEntity);

        bestTarget = oldAssignment.Target;
        bestSlotCell = oldAssignment.SlotCell;
        bestSlotIndex = oldAssignment.SlotIndex;
        bestDistSq = distSq;
        return true;
    }

    void TryConsiderTargetSlots(
        GridConfig cfg,
        int width,
        NativeArray<OccCell> occ,
        NativeParallelHashMap<int, Entity> wallMap,
        NativeParallelHashMap<int, Entity> slotMap,
        int2 targetCell,
        Entity targetEntity,
        AttackSlotConfig slotConfig,
        int2 zombieCell,
        ref Entity bestTarget,
        ref int2 bestSlotCell,
        ref byte bestSlotIndex,
        ref float bestDistSq)
    {
        var slotCount = AttackSlotUtility.GetSlotCount(slotConfig.Pattern, slotConfig.MaxAttackers);

        for (int i = 0; i < slotCount; i++)
        {
            var dir = AttackSlotUtility.GetDirection(slotConfig.Pattern, i);
            var slotCell = targetCell + dir;

            if (!IsSlotCellValid(cfg, width, occ, wallMap, slotCell))
                continue;

            if (!IsSlotReachableSide(targetCell, slotCell, zombieCell))
                continue;

            var slotKey = AttackSlotUtility.MakeSlotKey(targetEntity, i);
            if (slotMap.ContainsKey(slotKey))
                continue;

            var delta = slotCell - zombieCell;
            var distSq = math.lengthsq(new float2(delta.x, delta.y));

            if (distSq >= bestDistSq)
                continue;

            bestTarget = targetEntity;
            bestSlotCell = slotCell;
            bestSlotIndex = (byte)i;
            bestDistSq = distSq;
        }
    }

    bool IsSlotCellValid(
        GridConfig cfg,
        int width,
        NativeArray<OccCell> occ,
        NativeParallelHashMap<int, Entity> wallMap,
        int2 slotCell)
    {
        if (!IsoGridUtility.InBounds(cfg, slotCell))
            return false;

        var key = GridKeyUtility.CellKey(slotCell, width);
        if (wallMap.ContainsKey(key))
            return false;

        var idx = slotCell.y * width + slotCell.x;
        return occ[idx].Value == 0;
    }

    bool IsSlotReachableSide(int2 targetCell, int2 slotCell, int2 zombieCell)
    {
        var toZombie = zombieCell - targetCell;
        var slotDir = slotCell - targetCell;

        if (slotDir.x != 0 && toZombie.x != 0)
            return math.sign(slotDir.x) == math.sign(toZombie.x);

        if (slotDir.y != 0 && toZombie.y != 0)
            return math.sign(slotDir.y) == math.sign(toZombie.y);

        return true;
    }
}