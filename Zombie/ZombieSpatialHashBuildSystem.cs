using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(ZombieSeparationSystem))]
public partial struct ZombieSpatialHashBuildSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<ZombieSpatialHashTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var hashEntity = SystemAPI.GetSingletonEntity<ZombieSpatialHashTag>();
        var hashStateRW = SystemAPI.GetComponentRW<ZombieSpatialHashState>(hashEntity);

        var map = hashStateRW.ValueRW.Map;
        map.Clear();

        var zombieQuery = SystemAPI.QueryBuilder()
            .WithAll<ZombieTag, LocalTransform>()
            .Build();

        var zombieCount = zombieQuery.CalculateEntityCount();
        if (zombieCount == 0)
            return;

        if (map.Capacity < zombieCount)
            map.Capacity = zombieCount;

        foreach (var (tr, entity) in SystemAPI
                 .Query<RefRO<LocalTransform>>()
                 .WithAll<ZombieTag>()
                 .WithEntityAccess())
        {
            var cell = IsoGridUtility.WorldToGrid(cfg, tr.ValueRO.Position.xy);
            var hash = ZombieSpatialHashUtility.Hash(cell);
            map.Add(hash, entity);
        }
    }
}