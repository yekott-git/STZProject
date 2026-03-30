using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ZombieSeparationSystem))]
public partial struct ZombieMoveSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<FlowFieldState>();
        state.RequireForUpdate<GridOccupancy>();
        state.RequireForUpdate<WallIndexState>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var cfg = SystemAPI.GetSingleton<GridConfig>();

        var flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
        var flowCells = SystemAPI.GetBuffer<FlowFieldCell>(flowEntity).AsNativeArray();

        var occEntity = SystemAPI.GetSingletonEntity<GridOccupancy>();
        var occupancy = SystemAPI.GetBuffer<OccCell>(occEntity).AsNativeArray();

        var wallMap = SystemAPI.GetSingleton<WallIndexState>().Map;

        var job = new ZombieMoveJob
        {
            Cfg = cfg,
            Width = cfg.Size.x,
            FlowCells = flowCells,
            Occupancy = occupancy,
            WallMap = wallMap,
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

            MoveTowardStep(ref transform, ref move, worldPos, currentCell, nearWall, separation, slotAssignment, entity);
        }

        void MoveTowardStep(
            ref LocalTransform transform,
            ref ZombieMove move,
            float2 worldPos,
            int2 currentCell,
            bool nearWall,
            in ZombieSeparation separation,
            in AttackSlotAssignment slotAssignment,
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

            var flowDirWorld = toStep / math.max(dist, 0.0001f);
            var sideDir = new float2(-flowDirWorld.y, flowDirWorld.x);

            var flowWeight = math.max(0.35f, move.FlowWeight);
            var sepWeight = math.max(0f, move.SeparationWeight);

            var slotScale = slotAssignment.HasSlot != 0 ? 0.65f : 1f;
            var wallSepScale = nearWall ? 0.12f : 1f;
            var laneScale = nearWall ? 0.20f : 1f;

            var laneBias = ComputeLaneBias(entity);
            var laneOffset = sideDir * laneBias * move.LaneBiasStrength * laneScale * slotScale;
            var sep = separation.Force * sepWeight * wallSepScale * slotScale;

            var combined = flowDirWorld * flowWeight + sep + laneOffset;

            var combinedLenSq = math.lengthsq(combined);
            if (combinedLenSq < 0.0001f)
                combined = flowDirWorld;
            else
                combined *= math.rsqrt(combinedLenSq);

            var moveStep = math.min(move.Speed * Dt, dist);
            var movedPos = worldPos + combined * moveStep;
            var movedCell = IsoGridUtility.WorldToGrid(Cfg, movedPos);

            if (!IsoGridUtility.InBounds(Cfg, movedCell))
            {
                move.HasStepCell = 0;
                return;
            }

            if (movedCell.x != currentCell.x || movedCell.y != currentCell.y)
            {
                var movedIdx = movedCell.y * Width + movedCell.x;
                if (Occupancy[movedIdx].Value != 0)
                {
                    move.HasStepCell = 0;
                    return;
                }
            }

            transform.Position = new float3(movedPos.x, movedPos.y, transform.Position.z);
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