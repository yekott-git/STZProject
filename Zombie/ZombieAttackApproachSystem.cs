using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ZombieMoveSystem))]
[UpdateBefore(typeof(ZombieAttackSystem))]
public partial struct ZombieAttackApproachSystem : ISystem
{
    ComponentLookup<GridCell> targetCellLookup;
    ComponentLookup<AttackSlotConfig> targetSlotConfigLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<GridOccupancy>();
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

        var occEntity = SystemAPI.GetSingletonEntity<GridOccupancy>();
        var occupancy = SystemAPI.GetBuffer<OccCell>(occEntity).AsNativeArray();

        var slotMap = SystemAPI.GetSingleton<AttackSlotState>().SlotOwnerMap;

        var job = new ZombieAttackApproachJob
        {
            Cfg = cfg,
            Width = cfg.Size.x,
            Occupancy = occupancy,
            SlotMap = slotMap,
            TargetCellLookup = targetCellLookup,
            TargetSlotConfigLookup = targetSlotConfigLookup,
            Dt = SystemAPI.Time.DeltaTime
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct ZombieAttackApproachJob : IJobEntity
    {
        [ReadOnly] public GridConfig Cfg;
        [ReadOnly] public int Width;
        [ReadOnly] public NativeArray<OccCell> Occupancy;
        [ReadOnly] public NativeParallelHashMap<int, Entity> SlotMap;
        [ReadOnly] public ComponentLookup<GridCell> TargetCellLookup;
        [ReadOnly] public ComponentLookup<AttackSlotConfig> TargetSlotConfigLookup;
        public float Dt;

        void Execute(
            Entity entity,
            ref LocalTransform transform,
            ref ZombieMove move,
            in AttackSlotAssignment slotAssignment,
            in ZombieTag zombieTag)
        {
            if (TryApproachOwnedAttackSlot(entity, ref transform, ref move, slotAssignment))
                return;

            TryApproachStandby(entity, ref transform, ref move, slotAssignment);
        }

        bool TryApproachOwnedAttackSlot(
            Entity entity,
            ref LocalTransform transform,
            ref ZombieMove move,
            in AttackSlotAssignment slotAssignment)
        {
            if (!IsValidOwnedSlot(entity, slotAssignment, out var targetCell))
                return false;

            var worldPos = transform.Position.xy;
            var currentCell = IsoGridUtility.WorldToGrid(Cfg, worldPos);
            if (!IsoGridUtility.InBounds(Cfg, currentCell))
                return false;

            var slotCell = slotAssignment.SlotCell;
            var toTarget = targetCell - currentCell;
            var distToTarget = math.max(math.abs(toTarget.x), math.abs(toTarget.y));

            if (distToTarget > 2)
                return false;

            if (currentCell.x == slotCell.x && currentCell.y == slotCell.y)
            {
                var slotWorld = IsoGridUtility.GridToWorld(Cfg, slotCell).xy;
                var visualTarget = slotWorld + ComputeSlotVisualOffset(entity, slotAssignment.SlotIndex, targetCell, slotCell);

                MoveTowardPoint(ref transform, worldPos, visualTarget, move.Speed * 0.55f);
                move.HasStepCell = 0;
                return true;
            }

            var delta = slotCell - currentCell;
            var chebyshev = math.max(math.abs(delta.x), math.abs(delta.y));
            if (chebyshev > 1)
                return false;

            if (!IsCellOpen(slotCell))
            {
                move.HasStepCell = 0;
                return true;
            }

            var slotCenter = IsoGridUtility.GridToWorld(Cfg, slotCell).xy;
            MoveTowardPoint(ref transform, worldPos, slotCenter, move.Speed * 0.90f);
            move.HasStepCell = 0;
            return true;
        }

        bool TryApproachStandby(
            Entity entity,
            ref LocalTransform transform,
            ref ZombieMove move,
            in AttackSlotAssignment slotAssignment)
        {
            if (slotAssignment.HasStandby == 0)
                return false;

            if (slotAssignment.Target == Entity.Null)
                return false;

            if (!TargetCellLookup.HasComponent(slotAssignment.Target))
                return false;

            var targetCell = TargetCellLookup[slotAssignment.Target].Value;

            var worldPos = transform.Position.xy;
            var currentCell = IsoGridUtility.WorldToGrid(Cfg, worldPos);
            if (!IsoGridUtility.InBounds(Cfg, currentCell))
                return false;

            var toTarget = targetCell - currentCell;
            var distToTarget = math.max(math.abs(toTarget.x), math.abs(toTarget.y));
            if (distToTarget > 4)
                return false;

            var standbyCell = slotAssignment.StandbyCell;
            if (!IsCellOpen(standbyCell))
                return false;

            if (currentCell.x == standbyCell.x && currentCell.y == standbyCell.y)
            {
                var standbyWorld = IsoGridUtility.GridToWorld(Cfg, standbyCell).xy;
                var visualTarget = standbyWorld + ComputeStandbyVisualOffset(entity, targetCell, standbyCell);

                MoveTowardPoint(ref transform, worldPos, visualTarget, move.Speed * 0.40f);
                move.HasStepCell = 0;
                return true;
            }

            var delta = standbyCell - currentCell;
            var chebyshev = math.max(math.abs(delta.x), math.abs(delta.y));
            if (chebyshev > 1)
                return false;

            var standbyCenter = IsoGridUtility.GridToWorld(Cfg, standbyCell).xy;
            MoveTowardPoint(ref transform, worldPos, standbyCenter, move.Speed * 0.80f);
            move.HasStepCell = 0;
            return true;
        }

        bool IsValidOwnedSlot(Entity entity, in AttackSlotAssignment slotAssignment, out int2 targetCell)
        {
            targetCell = default;

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

            targetCell = TargetCellLookup[slotAssignment.Target].Value;
            var slotConfig = TargetSlotConfigLookup[slotAssignment.Target];

            return AttackSlotUtility.IsAdjacentForPattern(slotConfig.Pattern, targetCell, slotAssignment.SlotCell);
        }

        bool IsCellOpen(int2 cell)
        {
            if (!IsoGridUtility.InBounds(Cfg, cell))
                return false;

            var idx = cell.y * Width + cell.x;
            return Occupancy[idx].Value == 0;
        }

        void MoveTowardPoint(ref LocalTransform transform, float2 worldPos, float2 targetPos, float speed)
        {
            var toTarget = targetPos - worldPos;
            var dist = math.length(toTarget);

            if (dist < 0.02f)
            {
                transform.Position = new float3(targetPos.x, targetPos.y, transform.Position.z);
                return;
            }

            var dir = toTarget / math.max(dist, 0.0001f);
            var step = math.min(speed * Dt, dist);
            var movedPos = worldPos + dir * step;

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

            var tangentOffset = tangent * (a * 0.06f);
            var outwardOffset = outward * (-0.025f + b * 0.015f);

            return tangentOffset + outwardOffset;
        }

        float2 ComputeStandbyVisualOffset(Entity entity, int2 targetCell, int2 standbyCell)
        {
            var dir = standbyCell - targetCell;
            var outward = math.normalizesafe(new float2(dir.x, dir.y));
            var tangent = new float2(-outward.y, outward.x);

            var h = (uint)entity.Index;
            h ^= 2246822519u;
            h *= 3266489917u;
            h ^= h >> 16;
            h *= 668265263u;
            h ^= h >> 15;

            var a = ((h & 255u) / 255f) * 2f - 1f;
            var b = (((h >> 8) & 255u) / 255f) * 2f - 1f;

            var tangentOffset = tangent * (a * 0.05f);
            var outwardOffset = outward * (-0.015f + b * 0.01f);

            return tangentOffset + outwardOffset;
        }
    }
}