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

        void Execute(ref LocalTransform transform, ref ZombieMove move, in ZombieSeparation separation, in ZombieTag zombieTag)
        {
            var worldPos = transform.Position.xy;
            var currentCell = IsoGridUtility.WorldToGrid(Cfg, worldPos);

            if (!IsoGridUtility.InBounds(Cfg, currentCell))
                return;

            var nearWall = HasAdjacentWall(currentCell);

            if (nearWall)
            {
                move.HasStepCell = 0;
                return;
            }

            float2 flowDirWorld = float2.zero;

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

            flowDirWorld = toStep / dist;

            var separationWeight = nearWall ? 0f : move.SeparationWeight;
            var combined = flowDirWorld + separation.Force * separationWeight;

            var combinedLenSq = math.lengthsq(combined);
            if (combinedLenSq < 0.0001f)
                combined = flowDirWorld;
            else
                combined *= math.rsqrt(combinedLenSq);

            var step = math.min(move.Speed * Dt, dist);
            var nextPos = worldPos + combined * step;

            transform.Position = new float3(nextPos.x, nextPos.y, transform.Position.z);
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