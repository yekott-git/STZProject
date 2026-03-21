using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ZombieSpatialHashBuildSystem))]
[UpdateBefore(typeof(ZombieMoveSystem))]
public partial struct ZombieSeparationSystem : ISystem
{
    ComponentLookup<LocalTransform> transformLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<ZombieSpatialHashTag>();
        transformLookup = state.GetComponentLookup<LocalTransform>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        transformLookup.Update(ref state);

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var hashEntity = SystemAPI.GetSingletonEntity<ZombieSpatialHashTag>();
        var zombieMap = SystemAPI.GetComponent<ZombieSpatialHashState>(hashEntity).Map;

        var job = new ZombieSeparationJob
        {
            Cfg = cfg,
            ZombieMap = zombieMap,
            TransformLookup = transformLookup
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct ZombieSeparationJob : IJobEntity
    {
        [ReadOnly] public GridConfig Cfg;
        [ReadOnly] public NativeParallelMultiHashMap<int, Entity> ZombieMap;
        [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

        void Execute(
            Entity entity,
            in LocalTransform transform,
            in ZombieMove move,
            ref ZombieSeparation separation,
            in ZombieTag zombieTag)
        {
            var radius = move.SeparationRadius;
            if (radius <= 0f)
            {
                separation.Force = float2.zero;
                return;
            }

            var worldPos = transform.Position.xy;
            var currentCell = IsoGridUtility.WorldToGrid(Cfg, worldPos);
            var radiusSq = radius * radius;

            var force = float2.zero;

            for (var oy = -1; oy <= 1; oy++)
            {
                for (var ox = -1; ox <= 1; ox++)
                {
                    var cell = currentCell + new int2(ox, oy);
                    if (!IsoGridUtility.InBounds(Cfg, cell))
                        continue;

                    var hash = ZombieSpatialHashUtility.Hash(cell);

                    if (!ZombieMap.TryGetFirstValue(hash, out var otherEntity, out var it))
                        continue;

                    do
                    {
                        if (otherEntity == entity)
                            continue;

                        if (!TransformLookup.HasComponent(otherEntity))
                            continue;

                        var otherPos = TransformLookup[otherEntity].Position.xy;
                        var delta = worldPos - otherPos;
                        var distSq = math.lengthsq(delta);

                        if (distSq <= 0.000001f || distSq > radiusSq)
                            continue;

                        var dist = math.sqrt(distSq);
                        var away = delta / dist;

                        var t = 1f - (dist / radius);
                        var strength = t * t * t;

                        force += away * strength * 2.2f;
                    }
                    while (ZombieMap.TryGetNextValue(out otherEntity, ref it));
                }
            }

            var lenSq = math.lengthsq(force);
            if (lenSq < 0.0001f)
                separation.Force = float2.zero;
            else
                separation.Force = force * math.rsqrt(lenSq);
        }
    }
}