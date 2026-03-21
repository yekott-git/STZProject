using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ZombieSpatialHashBuildSystem))]
[UpdateBefore(typeof(ZombieMoveSystem))]
public partial struct ZombieSeparationSystem : ISystem
{
    ComponentLookup<LocalTransform> transformLookup;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<ZombieSpatialHashTag>();
        transformLookup = state.GetComponentLookup<LocalTransform>(true);
    }

    public void OnUpdate(ref SystemState state)
    {
        transformLookup.Update(ref state);

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var hashEntity = SystemAPI.GetSingletonEntity<ZombieSpatialHashTag>();
        var zombieMap = SystemAPI.GetComponent<ZombieSpatialHashState>(hashEntity).Map;

        foreach (var (tr, move, separation, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRO<ZombieMove>, RefRW<ZombieSeparation>>()
                     .WithAll<ZombieTag>()
                     .WithEntityAccess())
        {
            var radius = move.ValueRO.SeparationRadius;
            if (radius <= 0f)
            {
                separation.ValueRW.Force = float2.zero;
                continue;
            }

            var worldPos = tr.ValueRO.Position.xy;
            var currentCell = IsoGridUtility.WorldToGrid(cfg, worldPos);
            var radiusSq = radius * radius;

            var force = float2.zero;

            for (var oy = -1; oy <= 1; oy++)
            {
                for (var ox = -1; ox <= 1; ox++)
                {
                    var cell = currentCell + new int2(ox, oy);
                    if (!IsoGridUtility.InBounds(cfg, cell))
                        continue;

                    var hash = ZombieSpatialHashUtility.Hash(cell);

                    if (!zombieMap.TryGetFirstValue(hash, out var otherEntity, out var it))
                        continue;

                    do
                    {
                        if (otherEntity == entity)
                            continue;

                        if (!transformLookup.HasComponent(otherEntity))
                            continue;

                        var otherPos = transformLookup[otherEntity].Position.xy;
                        var delta = worldPos - otherPos;
                        var distSq = math.lengthsq(delta);

                        if (distSq <= 0.000001f || distSq > radiusSq)
                            continue;

                        var dist = math.sqrt(distSq);
                        var away = delta / dist;
                        var strength = 1f - (dist / radius);

                        force += away * strength;
                    }
                    while (zombieMap.TryGetNextValue(out otherEntity, ref it));
                }
            }

            var lenSq = math.lengthsq(force);
            if (lenSq < 0.0001f)
                separation.ValueRW.Force = float2.zero;
            else
                separation.ValueRW.Force = force * math.rsqrt(lenSq);
        }
    }
}