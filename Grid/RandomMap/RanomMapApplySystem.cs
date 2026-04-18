using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(RandomMapGenerateSystem))]
[UpdateAfter(typeof(StaticOccupancyBootstrapSystem))]
[UpdateBefore(typeof(BuildSystem))]
public partial struct RandomMapApplySystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<StaticOccupancy>();
        state.RequireForUpdate<RandomMapState>();
        state.RequireForUpdate<FlowFieldState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var mapEntity = SystemAPI.GetSingletonEntity<RandomMapState>();
        var mapState = SystemAPI.GetComponent<RandomMapState>(mapEntity);

        if (mapState.Generated == 0 || mapState.Applied != 0)
            return;

        state.CompleteDependency();

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var width = cfg.Size.x;

        var gridEntity = SystemAPI.GetSingletonEntity<GridConfig>();
        var staticOcc = SystemAPI.GetBuffer<StaticOccCell>(gridEntity);

        var obstaclePrefab = Entity.Null;
        if (SystemAPI.HasSingleton<ObstaclePrefabRef>())
            obstaclePrefab = SystemAPI.GetSingleton<ObstaclePrefabRef>().Prefab;

        var obstacleBuffer = SystemAPI.GetBuffer<RandomMapObstacleCell>(mapEntity);
        var obstacleCells = new NativeArray<int2>(obstacleBuffer.Length, Allocator.Temp);

        for (int i = 0; i < obstacleBuffer.Length; i++)
            obstacleCells[i] = obstacleBuffer[i].Value;

        for (int i = 0; i < staticOcc.Length; i++)
            staticOcc[i] = new StaticOccCell { Value = 0 };

        for (int i = 0; i < obstacleCells.Length; i++)
        {
            var cell = obstacleCells[i];
            if (!IsoGridUtility.InBounds(cfg, cell))
                continue;

            var idx = cell.y * width + cell.x;
            staticOcc[idx] = new StaticOccCell { Value = 1 };
        }

        if (obstaclePrefab != Entity.Null)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < obstacleCells.Length; i++)
            {
                var cell = obstacleCells[i];
                if (!IsoGridUtility.InBounds(cfg, cell))
                    continue;

                var spawned = ecb.Instantiate(obstaclePrefab);
                var pos = IsoGridUtility.GridToWorld(cfg, cell);

                ecb.SetComponent(spawned, LocalTransform.FromPosition(pos));
                ecb.AddComponent<ObstacleTag>(spawned);
                ecb.AddComponent(spawned, new GridCell { Value = cell });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        obstacleCells.Dispose();

        var flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
        var flowState = SystemAPI.GetComponent<FlowFieldState>(flowEntity);
        flowState.Dirty = 1;
        state.EntityManager.SetComponentData(flowEntity, flowState);

        mapState.Applied = 1;
        state.EntityManager.SetComponentData(mapEntity, mapState);
    }
}