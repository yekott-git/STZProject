using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ZombieSpatialHashBuildSystem))]
[UpdateBefore(typeof(ZombieSeparationSystem))]
public partial struct ZombieTargetSelectSystem : ISystem
{
    ComponentLookup<GridCell> gridCellLookup;
    ComponentLookup<DefenseTargetPriority> priorityLookup;
    ComponentLookup<Health> healthLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<WallIndexState>();

        gridCellLookup = state.GetComponentLookup<GridCell>(true);
        priorityLookup = state.GetComponentLookup<DefenseTargetPriority>(true);
        healthLookup = state.GetComponentLookup<Health>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        gridCellLookup.Update(ref state);
        priorityLookup.Update(ref state);
        healthLookup.Update(ref state);

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var defenseMap = SystemAPI.GetSingleton<WallIndexState>().Map;

        var job = new ZombieTargetSelectJob
        {
            Cfg = cfg,
            Width = cfg.Size.x,
            DefenseMap = defenseMap,
            GridCellLookup = gridCellLookup,
            PriorityLookup = priorityLookup,
            HealthLookup = healthLookup
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct ZombieTargetSelectJob : IJobEntity
    {
        [ReadOnly] public GridConfig Cfg;
        [ReadOnly] public int Width;
        [ReadOnly] public NativeParallelHashMap<int, Entity> DefenseMap;
        [ReadOnly] public ComponentLookup<GridCell> GridCellLookup;
        [ReadOnly] public ComponentLookup<DefenseTargetPriority> PriorityLookup;
        [ReadOnly] public ComponentLookup<Health> HealthLookup;

        void Execute(
            ref ZombieCurrentTarget currentTarget,
            in LocalTransform transform,
            in ZombieTag zombieTag)
        {
            var zombieCell = IsoGridUtility.WorldToGrid(Cfg, transform.Position.xy);
            if (!IsoGridUtility.InBounds(Cfg, zombieCell))
            {
                currentTarget.Value = Entity.Null;
                return;
            }

            var oldTarget = currentTarget.Value;
            if (IsValidTarget(oldTarget))
            {
                var oldCell = GridCellLookup[oldTarget].Value;
                var oldDelta = oldCell - zombieCell;
                var oldDist = math.abs(oldDelta.x) + math.abs(oldDelta.y);

                if (oldDist <= 3)
                    return;
            }

            Entity bestTarget = Entity.Null;
            float bestScore = float.MaxValue;

            const int searchRadius = 4;

            for (int y = -searchRadius; y <= searchRadius; y++)
            {
                for (int x = -searchRadius; x <= searchRadius; x++)
                {
                    var cell = zombieCell + new int2(x, y);
                    if (!IsoGridUtility.InBounds(Cfg, cell))
                        continue;

                    var key = GridKeyUtility.CellKey(cell, Width);
                    if (!DefenseMap.TryGetValue(key, out var target))
                        continue;

                    if (!IsValidTarget(target))
                        continue;

                    var targetCell = GridCellLookup[target].Value;
                    var delta = targetCell - zombieCell;
                    var distSq = math.lengthsq(new float2(delta.x, delta.y));

                    byte priority = 100;
                    if (PriorityLookup.HasComponent(target))
                        priority = PriorityLookup[target].Value;

                    var score = distSq - priority * 0.05f;

                    if (target == oldTarget)
                        score -= 1.5f;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestTarget = target;
                    }
                }
            }

            currentTarget.Value = bestTarget;
        }

        bool IsValidTarget(Entity target)
        {
            if (target == Entity.Null)
                return false;

            if (!GridCellLookup.HasComponent(target))
                return false;

            if (HealthLookup.HasComponent(target) && HealthLookup[target].Value <= 0)
                return false;

            return true;
        }
    }
}