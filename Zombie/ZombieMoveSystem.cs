using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ZombieSeparationSystem))]
public partial struct ZombieMoveSystem : ISystem
{
    ComponentLookup<GridCell> targetCellLookup;
    ComponentLookup<AttackSlotConfig> targetSlotConfigLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<FlowFieldState>();
        state.RequireForUpdate<GridOccupancy>();
        state.RequireForUpdate<WallIndexState>();
        state.RequireForUpdate<AttackSlotState>();

        targetCellLookup = state.GetComponentLookup<GridCell>(true);
        targetSlotConfigLookup = state.GetComponentLookup<AttackSlotConfig>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        targetCellLookup.Update(ref state);
        targetSlotConfigLookup.Update(ref state);

        var cfg = SystemAPI.GetSingleton<GridConfig>();

        var flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
        var flowCells = SystemAPI.GetBuffer<FlowFieldCell>(flowEntity).AsNativeArray();

        var occEntity = SystemAPI.GetSingletonEntity<GridOccupancy>();
        var occupancy = SystemAPI.GetBuffer<OccCell>(occEntity).AsNativeArray();

        var wallMap = SystemAPI.GetSingleton<WallIndexState>().Map;
        var slotMap = SystemAPI.GetSingleton<AttackSlotState>().SlotOwnerMap;

        var job = new ZombieMoveJob
        {
            Cfg = cfg,
            Width = cfg.Size.x,
            FlowCells = flowCells,
            Occupancy = occupancy,
            WallMap = wallMap,
            SlotMap = slotMap,
            TargetCellLookup = targetCellLookup,
            TargetSlotConfigLookup = targetSlotConfigLookup,
            Dt = SystemAPI.Time.DeltaTime
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct ZombieMoveJob : IJobEntity
    {
        [ReadOnly] public GridConfig Cfg;
        [ReadOnly] public int Width;
        [ReadOnly] public NativeArray<FlowFieldCell> FlowCells;
        [ReadOnly] public NativeArray<OccCell> Occupancy;
        [ReadOnly] public NativeParallelHashMap<int, Entity> WallMap;
        [ReadOnly] public NativeParallelHashMap<int, Entity> SlotMap;
        [ReadOnly] public ComponentLookup<GridCell> TargetCellLookup;
        [ReadOnly] public ComponentLookup<AttackSlotConfig> TargetSlotConfigLookup;
        public float Dt;

        void Execute(
            Entity entity,
            ref LocalTransform transform,
            ref ZombieMove move,
            in ZombieSeparation separation,
            in AttackSlotAssignment slotAssignment,
            in ZombieTag zombieTag)
        {
            var worldPos = transform.Position.xy;
            var currentCell = IsoGridUtility.WorldToGrid(Cfg, worldPos);

            if (!IsoGridUtility.InBounds(Cfg, currentCell))
                return;

            if (TryMoveToAttackSlot(entity, ref transform, ref move, in separation, in slotAssignment, currentCell, worldPos))
                return;

            var nearWall = HasAdjacentWall(currentCell);

            if (move.HasStepCell == 0)
            {
                var index = currentCell.y * Width + currentCell.x;
                var flow = FlowCells[index];

                var dir = new int2(flow.DirX, flow.DirY);
                if (dir.x == 0 && dir.y == 0)
                    return;

                var nextCell = currentCell + dir;
                if (!IsoGridUtility.InBounds(Cfg, nextCell))
                    return;

                var nextIdx = nextCell.y * Width + nextCell.x;
                if (Occupancy[nextIdx].Value != 0)
                    return;

                move.CurrentStepCell = nextCell;
                move.HasStepCell = 1;
            }

            MoveTowardStep(ref transform, ref move, worldPos, nearWall, separation, entity);
        }

        bool TryMoveToAttackSlot(
            Entity entity,
            ref LocalTransform transform,
            ref ZombieMove move,
            in ZombieSeparation separation,
            in AttackSlotAssignment slotAssignment,
            int2 currentCell,
            float2 worldPos)
        {
            if (slotAssignment.HasSlot == 0)
                return false;

            if (slotAssignment.Target == Entity.Null)
                return false;

            if (!TargetCellLookup.HasComponent(slotAssignment.Target))
                return false;

            if (!TargetSlotConfigLookup.HasComponent(slotAssignment.Target))
                return false;

            var slotKey = AttackSlotUtility.MakeSlotKey(slotAssignment.Target, slotAssignment.SlotIndex);
            if (!SlotMap.TryGetValue(slotKey, out var owner) || owner != entity)
                return false;

            var targetCell = TargetCellLookup[slotAssignment.Target].Value;
            var slotConfig = TargetSlotConfigLookup[slotAssignment.Target];

            if (!AttackSlotUtility.IsAdjacentForPattern(slotConfig.Pattern, targetCell, slotAssignment.SlotCell))
                return false;

            var slotWorld = IsoGridUtility.GridToWorld(Cfg, slotAssignment.SlotCell);
            var visualOffset = ComputeSlotVisualOffset(entity, slotAssignment.SlotIndex, targetCell, slotAssignment.SlotCell);
            var visualTarget = slotWorld.xy + visualOffset;

            if (currentCell.x == slotAssignment.SlotCell.x && currentCell.y == slotAssignment.SlotCell.y)
            {
                var toVisual = visualTarget - worldPos;
                var visualDist = math.length(toVisual);

                if (visualDist < 0.03f)
                {
                    transform.Position = new float3(visualTarget.x, visualTarget.y, transform.Position.z);
                    move.HasStepCell = 0;
                    return true;
                }

                var dir = toVisual / math.max(visualDist, 0.0001f);
                var step = math.min(move.Speed * Dt, visualDist);
                var movedPos = worldPos + dir * step;

                transform.Position = new float3(movedPos.x, movedPos.y, transform.Position.z);
                move.HasStepCell = 0;
                return true;
            }

            var desiredDir = slotAssignment.SlotCell - currentCell;
            desiredDir.x = math.clamp(desiredDir.x, -1, 1);
            desiredDir.y = math.clamp(desiredDir.y, -1, 1);

            int2 nextCell = currentCell;

            if (math.abs(desiredDir.x) >= math.abs(desiredDir.y))
            {
                if (TryPickSlotStep(currentCell, new int2(desiredDir.x, 0), out nextCell) ||
                    TryPickSlotStep(currentCell, new int2(0, desiredDir.y), out nextCell))
                {
                    move.CurrentStepCell = nextCell;
                    move.HasStepCell = 1;
                    MoveTowardStep(ref transform, ref move, worldPos, true, separation, entity);
                    return true;
                }
            }
            else
            {
                if (TryPickSlotStep(currentCell, new int2(0, desiredDir.y), out nextCell) ||
                    TryPickSlotStep(currentCell, new int2(desiredDir.x, 0), out nextCell))
                {
                    move.CurrentStepCell = nextCell;
                    move.HasStepCell = 1;
                    MoveTowardStep(ref transform, ref move, worldPos, true, separation, entity);
                    return true;
                }
            }

            move.HasStepCell = 0;
            return false;
        }

        bool TryPickSlotStep(int2 currentCell, int2 dir, out int2 nextCell)
        {
            nextCell = currentCell;

            if (dir.x == 0 && dir.y == 0)
                return false;

            var candidate = currentCell + dir;
            if (!IsoGridUtility.InBounds(Cfg, candidate))
                return false;

            var idx = candidate.y * Width + candidate.x;
            if (Occupancy[idx].Value != 0)
                return false;

            nextCell = candidate;
            return true;
        }

        void MoveTowardStep(
            ref LocalTransform transform,
            ref ZombieMove move,
            float2 worldPos,
            bool nearWall,
            in ZombieSeparation separation,
            Entity entity)
        {
            var stepCell = move.CurrentStepCell;
            if (!IsoGridUtility.InBounds(Cfg, stepCell))
            {
                move.HasStepCell = 0;
                return;
            }

            var stepIdx = stepCell.y * Width + stepCell.x;
            if (Occupancy[stepIdx].Value != 0)
            {
                move.HasStepCell = 0;
                return;
            }

            var stepWorld3 = IsoGridUtility.GridToWorld(Cfg, stepCell);
            var toStep = stepWorld3.xy - worldPos;
            var dist = math.length(toStep);

            if (dist < 0.05f)
            {
                transform.Position = stepWorld3;
                move.HasStepCell = 0;
                return;
            }

            var flowDirWorld = toStep / dist;
            var sideDir = new float2(-flowDirWorld.y, flowDirWorld.x);

            var flowWeight = math.max(0.1f, move.FlowWeight);
            var sepWeight = math.max(0f, move.SeparationWeight);
            var wallSepScale = nearWall ? 0.35f : 1f;

            var laneBias = ComputeLaneBias(entity);
            var laneOffset = sideDir * laneBias * move.LaneBiasStrength;

            var combined =
                flowDirWorld * flowWeight +
                separation.Force * sepWeight * wallSepScale +
                laneOffset;

            var combinedLenSq = math.lengthsq(combined);
            if (combinedLenSq < 0.0001f)
                combined = flowDirWorld;
            else
                combined *= math.rsqrt(combinedLenSq);

            var moveStep = math.min(move.Speed * Dt, dist);
            var movedPos = worldPos + combined * moveStep;

            transform.Position = new float3(movedPos.x, movedPos.y, transform.Position.z);
        }

        float2 ComputeSlotVisualOffset(Entity entity, byte slotIndex, int2 targetCell, int2 slotCell)
        {
            var dir = slotCell - targetCell;
            var outward = math.normalizesafe(new float2(dir.x, dir.y));
            var tangent = new float2(-outward.y, outward.x);

            var h = (uint)entity.Index;
            h ^= (uint)(slotIndex * 193u + 17u);
            h ^= 2747636419u;
            h *= 2654435769u;
            h ^= h >> 16;
            h *= 2654435769u;
            h ^= h >> 16;

            var a = ((h & 255u) / 255f) * 2f - 1f;
            var b = (((h >> 8) & 255u) / 255f) * 2f - 1f;

            var tangentOffset = tangent * (a * 0.12f);
            var outwardOffset = outward * (-0.05f + b * 0.04f);

            return tangentOffset + outwardOffset;
        }

        float ComputeLaneBias(Entity entity)
        {
            var h = (uint)entity.Index;
            h ^= 2747636419u;
            h *= 2654435769u;
            h ^= h >> 16;
            h *= 2654435769u;
            h ^= h >> 16;

            var t = (h & 1023u) / 1023f;
            return t * 2f - 1f;
        }

        bool HasAdjacentWall(int2 currentCell)
        {
            return HasWall(currentCell + new int2(1, 0)) ||
                   HasWall(currentCell + new int2(-1, 0)) ||
                   HasWall(currentCell + new int2(0, 1)) ||
                   HasWall(currentCell + new int2(0, -1));
        }

        bool HasWall(int2 cell)
        {
            if (!IsoGridUtility.InBounds(Cfg, cell))
                return false;

            var key = GridKeyUtility.CellKey(cell, Width);
            return WallMap.TryGetValue(key, out var entity) && entity != Entity.Null;
        }
    }
}