using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(ZombieSeparationSystem))]
public partial struct ZombieSpatialHashBuildSystem : ISystem
{
    EntityQuery zombieQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<ZombieSpatialHashTag>();

        zombieQuery = SystemAPI.QueryBuilder()
            .WithAll<ZombieTag, LocalTransform>()
            .Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var zombieCount = zombieQuery.CalculateEntityCount();
        if (zombieCount == 0)
            return;

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var hashEntity = SystemAPI.GetSingletonEntity<ZombieSpatialHashTag>();
        var hashStateRW = SystemAPI.GetComponentRW<ZombieSpatialHashState>(hashEntity);

        var map = hashStateRW.ValueRW.Map;
        map.Clear();

        if (map.Capacity < zombieCount)
            map.Capacity = zombieCount;

        var job = new BuildZombieSpatialHashJob
        {
            Cfg = cfg,
            Writer = map.AsParallelWriter()
        };

        state.Dependency = job.ScheduleParallel(zombieQuery, state.Dependency);
    }

    [BurstCompile]
    public partial struct BuildZombieSpatialHashJob : IJobEntity
    {
        [ReadOnly] public GridConfig Cfg;
        public NativeParallelMultiHashMap<int, Entity>.ParallelWriter Writer;

        void Execute(Entity entity, in LocalTransform transform, in ZombieTag zombieTag)
        {
            var cell = IsoGridUtility.WorldToGrid(Cfg, transform.Position.xy);
            var hash = ZombieSpatialHashUtility.Hash(cell);
            Writer.Add(hash, entity);
        }
    }
}