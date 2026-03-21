using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ZombieSpatialHashBuildSystem))]
public partial struct ProjectileHitSystem : ISystem
{
    ComponentLookup<Health> healthLookup;
    ComponentLookup<LocalTransform> transformLookup;
    EntityQuery projectileQuery;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<ProjectileTag>();
        state.RequireForUpdate<DamageEventQueueTag>();
        state.RequireForUpdate<ZombieSpatialHashTag>();

        healthLookup = state.GetComponentLookup<Health>(true);
        transformLookup = state.GetComponentLookup<LocalTransform>(true);

        projectileQuery = SystemAPI.QueryBuilder()
            .WithAll<ProjectileTag, LocalTransform, Projectile>()
            .Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        var projectileCount = projectileQuery.CalculateEntityCount();
        if (projectileCount == 0)
            return;

        healthLookup.Update(ref state);
        transformLookup.Update(ref state);

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var hashEntity = SystemAPI.GetSingletonEntity<ZombieSpatialHashTag>();
        var zombieMap = SystemAPI.GetComponent<ZombieSpatialHashState>(hashEntity).Map;

        var queueEntity = SystemAPI.GetSingletonEntity<DamageEventQueueTag>();
        var damageBuffer = SystemAPI.GetBuffer<DamageEvent>(queueEntity);

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        const float hitRadius = 0.45f;
        var hitRadiusSq = hitRadius * hitRadius;

        foreach (var (tr, projectile, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<Projectile>>()
                     .WithAll<ProjectileTag>()
                     .WithEntityAccess())
        {
            var projectilePos = tr.ValueRO.Position.xy;
            var centerCell = IsoGridUtility.WorldToGrid(cfg, projectilePos);

            Entity bestTarget = Entity.Null;
            var bestDistSq = float.MaxValue;

            for (var oy = -1; oy <= 1; oy++)
            {
                for (var ox = -1; ox <= 1; ox++)
                {
                    var cell = centerCell + new int2(ox, oy);

                    if (!IsoGridUtility.InBounds(cfg, cell))
                        continue;

                    var hash = ZombieSpatialHashUtility.Hash(cell);

                    if (!zombieMap.TryGetFirstValue(hash, out var zombieEntity, out var it))
                        continue;

                    do
                    {
                        if (!healthLookup.HasComponent(zombieEntity))
                            continue;

                        if (!transformLookup.HasComponent(zombieEntity))
                            continue;

                        var zombiePos = transformLookup[zombieEntity].Position.xy;
                        var distSq = math.lengthsq(zombiePos - projectilePos);

                        if (distSq > hitRadiusSq)
                            continue;

                        if (distSq < bestDistSq)
                        {
                            bestDistSq = distSq;
                            bestTarget = zombieEntity;
                        }
                    }
                    while (zombieMap.TryGetNextValue(out zombieEntity, ref it));
                }
            }

            if (bestTarget == Entity.Null)
                continue;

            damageBuffer.Add(new DamageEvent
            {
                Target = bestTarget,
                Value = projectile.ValueRO.Damage
            });

            if (projectile.ValueRO.ImpactPrefab != Entity.Null)
            {
                var impact = ecb.Instantiate(projectile.ValueRO.ImpactPrefab);
                ecb.SetComponent(
                    impact,
                    LocalTransform.FromPosition(new float3(projectilePos.x, projectilePos.y, 0f)));
            }

            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}