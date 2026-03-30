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
        state.RequireForUpdate<WallIndexState>();
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

        var occEntity = SystemAPI.GetSingletonEntity<GridOccupancy>();
        var occ = SystemAPI.GetBuffer<OccCell>(occEntity).AsNativeArray();

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
            assignment.Target = Entity.Null;
            assignment.SlotCell = default;
            assignment.SlotIndex = 0;
            assignment.HasSlot = 0;
            assignment.StandbyCell = default;
            assignment.HasStandby = 0;

            var worldPos = tr.ValueRO.Position.xy;
            var zombieCell = IsoGridUtility.WorldToGrid(cfg, worldPos);

            Entity bestAttackTarget = Entity.Null;
            int2 bestAttackSlotCell = default;
            byte bestAttackSlotIndex = 0;
            float bestAttackScore = float.MaxValue;

            Entity bestStandbyTarget = Entity.Null;
            int2 bestStandbyCell = default;
            float bestStandbyScore = float.MaxValue;

            if (TryReclaimCurrentSlot(
                    cfg,
                    width,
                    occ,
                    wallMap,
                    slotMap,
                    zombieCell,
                    entity,
                    slotAssignment.ValueRO,
                    ref bestAttackTarget,
                    ref bestAttackSlotCell,
                    ref bestAttackSlotIndex,
                    ref bestAttackScore))
            {
                assignment.Target = bestAttackTarget;
                assignment.SlotCell = bestAttackSlotCell;
                assignment.SlotIndex = bestAttackSlotIndex;
                assignment.HasSlot = 1;
                assignment.HasStandby = 0;
                slotAssignment.ValueRW = assignment;
                continue;
            }

            TryReclaimCurrentStandby(
                cfg,
                width,
                occ,
                wallMap,
                zombieCell,
                slotAssignment.ValueRO,
                ref bestStandbyTarget,
                ref bestStandbyCell,
                ref bestStandbyScore);

            const int searchRadiusCells = 4;

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

                    var slotConfig = slotConfigLookup[targetEntity];

                    TryConsiderTargetSlots(
                        cfg,
                        width,
                        occ,
                        wallMap,
                        slotMap,
                        targetCell,
                        targetEntity,
                        slotConfig,
                        zombieCell,
                        ref bestAttackTarget,
                        ref bestAttackSlotCell,
                        ref bestAttackSlotIndex,
                        ref bestAttackScore);

                    TryConsiderTargetStandbyCells(
                        cfg,
                        width,
                        occ,
                        wallMap,
                        targetCell,
                        targetEntity,
                        zombieCell,
                        ref bestStandbyTarget,
                        ref bestStandbyCell,
                        ref bestStandbyScore);
                }
            }

            if (bestAttackTarget != Entity.Null)
            {
                var slotKey = AttackSlotUtility.MakeSlotKey(bestAttackTarget, bestAttackSlotIndex);
                slotMap.TryAdd(slotKey, entity);

                assignment.Target = bestAttackTarget;
                assignment.SlotCell = bestAttackSlotCell;
                assignment.SlotIndex = bestAttackSlotIndex;
                assignment.HasSlot = 1;
                assignment.HasStandby = 0;

                slotAssignment.ValueRW = assignment;
                continue;
            }

            if (bestStandbyTarget != Entity.Null)
            {
                assignment.Target = bestStandbyTarget;
                assignment.StandbyCell = bestStandbyCell;
                assignment.HasStandby = 1;
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
        ref float bestScore)
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

        var score = ComputeAttackSlotScore(targetCell, oldAssignment.SlotCell, zombieCell, oldAssignment.SlotIndex, true);

        slotMap.TryAdd(slotKey, zombieEntity);

        bestTarget = oldAssignment.Target;
        bestSlotCell = oldAssignment.SlotCell;
        bestSlotIndex = oldAssignment.SlotIndex;
        bestScore = score;
        return true;
    }

    void TryReclaimCurrentStandby(
        GridConfig cfg,
        int width,
        NativeArray<OccCell> occ,
        NativeParallelHashMap<int, Entity> wallMap,
        int2 zombieCell,
        AttackSlotAssignment oldAssignment,
        ref Entity bestTarget,
        ref int2 bestStandbyCell,
        ref float bestScore)
    {
        if (oldAssignment.HasStandby == 0)
            return;

        if (oldAssignment.Target == Entity.Null)
            return;

        if (!gridCellLookup.HasComponent(oldAssignment.Target))
            return;

        var targetCell = gridCellLookup[oldAssignment.Target].Value;

        if (!IsStandbyCellValid(cfg, width, occ, wallMap, targetCell, oldAssignment.StandbyCell))
            return;

        if (!IsStandbyReachableSide(targetCell, oldAssignment.StandbyCell, zombieCell))
            return;

        var score = ComputeStandbyScore(targetCell, oldAssignment.StandbyCell, zombieCell, true);
        bestTarget = oldAssignment.Target;
        bestStandbyCell = oldAssignment.StandbyCell;
        bestScore = score;
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
        ref float bestScore)
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

            var score = ComputeAttackSlotScore(targetCell, slotCell, zombieCell, i, false);
            if (score >= bestScore)
                continue;

            bestTarget = targetEntity;
            bestSlotCell = slotCell;
            bestSlotIndex = (byte)i;
            bestScore = score;
        }
    }

    void TryConsiderTargetStandbyCells(
        GridConfig cfg,
        int width,
        NativeArray<OccCell> occ,
        NativeParallelHashMap<int, Entity> wallMap,
        int2 targetCell,
        Entity targetEntity,
        int2 zombieCell,
        ref Entity bestTarget,
        ref int2 bestStandbyCell,
        ref float bestScore)
    {
        for (int i = 0; i < 12; i++)
        {
            var offset = GetStandbyOffset(i);
            var standbyCell = targetCell + offset;

            if (!IsStandbyCellValid(cfg, width, occ, wallMap, targetCell, standbyCell))
                continue;

            if (!IsStandbyReachableSide(targetCell, standbyCell, zombieCell))
                continue;

            var score = ComputeStandbyScore(targetCell, standbyCell, zombieCell, false);
            if (score >= bestScore)
                continue;

            bestTarget = targetEntity;
            bestStandbyCell = standbyCell;
            bestScore = score;
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

    bool IsStandbyCellValid(
        GridConfig cfg,
        int width,
        NativeArray<OccCell> occ,
        NativeParallelHashMap<int, Entity> wallMap,
        int2 targetCell,
        int2 standbyCell)
    {
        if (!IsoGridUtility.InBounds(cfg, standbyCell))
            return false;

        var d = standbyCell - targetCell;
        var chebyshev = math.max(math.abs(d.x), math.abs(d.y));
        if (chebyshev != 2)
            return false;

        var key = GridKeyUtility.CellKey(standbyCell, width);
        if (wallMap.ContainsKey(key))
            return false;

        var idx = standbyCell.y * width + standbyCell.x;
        return occ[idx].Value == 0;
    }

    bool IsSlotReachableSide(int2 targetCell, int2 slotCell, int2 zombieCell)
    {
        var a = math.normalizesafe(new float2(slotCell.x - targetCell.x, slotCell.y - targetCell.y));
        var b = math.normalizesafe(new float2(zombieCell.x - targetCell.x, zombieCell.y - targetCell.y));

        if (math.lengthsq(a) < 0.0001f || math.lengthsq(b) < 0.0001f)
            return true;

        return math.dot(a, b) >= -0.15f;
    }

    bool IsStandbyReachableSide(int2 targetCell, int2 standbyCell, int2 zombieCell)
    {
        var a = math.normalizesafe(new float2(standbyCell.x - targetCell.x, standbyCell.y - targetCell.y));
        var b = math.normalizesafe(new float2(zombieCell.x - targetCell.x, zombieCell.y - targetCell.y));

        if (math.lengthsq(a) < 0.0001f || math.lengthsq(b) < 0.0001f)
            return true;

        return math.dot(a, b) >= 0.10f;
    }

    float ComputeAttackSlotScore(int2 targetCell, int2 slotCell, int2 zombieCell, int slotIndex, bool reclaimBonus)
    {
        var delta = slotCell - zombieCell;
        var distSq = math.lengthsq(new float2(delta.x, delta.y));

        var slotDir = slotCell - targetCell;
        var cardinalPenalty = (math.abs(slotDir.x) + math.abs(slotDir.y) == 1) ? 0f : 0.35f;

        var toZombie = zombieCell - targetCell;
        var sideDot = math.dot(
            math.normalizesafe(new float2(slotDir.x, slotDir.y)),
            math.normalizesafe(new float2(toZombie.x, toZombie.y)));

        var sidePenalty = (1f - math.max(0f, sideDot)) * 0.75f;
        var reclaimBias = reclaimBonus ? -0.2f : 0f;

        return distSq + cardinalPenalty + sidePenalty + reclaimBias + slotIndex * 0.001f;
    }

    float ComputeStandbyScore(int2 targetCell, int2 standbyCell, int2 zombieCell, bool reclaimBonus)
    {
        var delta = standbyCell - zombieCell;
        var distSq = math.lengthsq(new float2(delta.x, delta.y));

        var radial = standbyCell - targetCell;
        var axisPenalty = (math.abs(radial.x) == 2 && math.abs(radial.y) == 0) ||
                          (math.abs(radial.x) == 0 && math.abs(radial.y) == 2)
            ? 0f
            : 0.15f;

        var toZombie = zombieCell - targetCell;
        var sideDot = math.dot(
            math.normalizesafe(new float2(radial.x, radial.y)),
            math.normalizesafe(new float2(toZombie.x, toZombie.y)));

        var sidePenalty = (1f - math.max(0f, sideDot)) * 0.55f;
        var reclaimBias = reclaimBonus ? -0.15f : 0f;

        return distSq + axisPenalty + sidePenalty + reclaimBias;
    }

    int2 GetStandbyOffset(int index)
    {
        switch (index)
        {
            case 0: return new int2(2, 0);
            case 1: return new int2(-2, 0);
            case 2: return new int2(0, 2);
            case 3: return new int2(0, -2);
            case 4: return new int2(2, 1);
            case 5: return new int2(2, -1);
            case 6: return new int2(-2, 1);
            case 7: return new int2(-2, -1);
            case 8: return new int2(1, 2);
            case 9: return new int2(-1, 2);
            case 10: return new int2(1, -2);
            case 11: return new int2(-1, -2);
            default: return int2.zero;
        }
    }
}