using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ZombieSpatialHashBuildSystem))]
public partial struct TurretAcquireTargetSystem : ISystem
{
    ComponentLookup<LocalTransform> localTransformLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TurretAttack>();
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<ZombieSpatialHashTag>();

        localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        localTransformLookup.Update(ref state);

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var hashEntity = SystemAPI.GetSingletonEntity<ZombieSpatialHashTag>();
        var zombieMap = SystemAPI.GetComponent<ZombieSpatialHashState>(hashEntity).Map;

        foreach (var (atk, tTr, turretTarget) in
                 SystemAPI.Query<RefRO<TurretAttack>, RefRO<LocalTransform>, RefRW<TurretTarget>>())
        {
            var tt = turretTarget.ValueRW;
            var turretPos = tTr.ValueRO.Position.xy;
            var range = atk.ValueRO.Range;
            var rangeSq = range * range;

            if (tt.Target != Entity.Null && localTransformLookup.HasComponent(tt.Target))
            {
                var currentTargetPos = localTransformLookup[tt.Target].Position.xy;
                if (math.lengthsq(currentTargetPos - turretPos) <= rangeSq)
                {
                    turretTarget.ValueRW = tt;
                    continue;
                }
            }

            tt.Target = Entity.Null;
            var bestDistSq = float.MaxValue;

            var minWorld = turretPos - new float2(range, range);
            var maxWorld = turretPos + new float2(range, range);

            var minCell = IsoGridUtility.WorldToGrid(cfg, minWorld);
            var maxCell = IsoGridUtility.WorldToGrid(cfg, maxWorld);

            minCell.x = math.clamp(minCell.x, 0, cfg.Size.x - 1);
            minCell.y = math.clamp(minCell.y, 0, cfg.Size.y - 1);
            maxCell.x = math.clamp(maxCell.x, 0, cfg.Size.x - 1);
            maxCell.y = math.clamp(maxCell.y, 0, cfg.Size.y - 1);

            for (var y = minCell.y; y <= maxCell.y; y++)
            {
                for (var x = minCell.x; x <= maxCell.x; x++)
                {
                    var cell = new int2(x, y);
                    var hash = ZombieSpatialHashUtility.Hash(cell);

                    if (!zombieMap.TryGetFirstValue(hash, out var zombieEntity, out var it))
                        continue;

                    do
                    {
                        if (!localTransformLookup.HasComponent(zombieEntity))
                            continue;

                        var zombiePos = localTransformLookup[zombieEntity].Position.xy;
                        var distSq = math.lengthsq(zombiePos - turretPos);

                        if (distSq <= rangeSq && distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            tt.Target = zombieEntity;
                        }
                    }
                    while (zombieMap.TryGetNextValue(out zombieEntity, ref it));
                }
            }

            turretTarget.ValueRW = tt;
        }
    }
}