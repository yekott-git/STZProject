using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(RandomMapBootstrapSystem))]
public partial struct RandomMapGenerateSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<RandomMapState>();
        state.RequireForUpdate<CoreTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var mapEntity = SystemAPI.GetSingletonEntity<RandomMapState>();
        var mapState = SystemAPI.GetComponent<RandomMapState>(mapEntity);

        if (mapState.Generated != 0)
            return;

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var coreEntity = SystemAPI.GetSingletonEntity<CoreTag>();
        var coreCell = SystemAPI.GetComponent<GridCell>(coreEntity).Value;

        var obstacleBuffer = SystemAPI.GetBuffer<RandomMapObstacleCell>(mapEntity);
        obstacleBuffer.Clear();

        var random = new Unity.Mathematics.Random(math.max(1u, mapState.Seed));
        var used = new NativeParallelHashSet<int>(256, Allocator.Temp);

        int targetCount = math.max(24, (cfg.Size.x * cfg.Size.y) / 40);
        int tries = targetCount * 10;

        for (int i = 0; i < tries && obstacleBuffer.Length < targetCount; i++)
        {
            var cell = new int2(
                random.NextInt(0, cfg.Size.x),
                random.NextInt(0, cfg.Size.y));

            if (!IsoGridUtility.InBounds(cfg, cell))
                continue;

            if (IsProtected(cell, coreCell, 4))
                continue;

            int key = cell.y * cfg.Size.x + cell.x;
            if (!used.Add(key))
                continue;

            obstacleBuffer.Add(new RandomMapObstacleCell
            {
                Value = cell
            });
        }

        mapState.Generated = 1;
        mapState.Applied = 0;
        mapState.Seed = random.NextUInt();

        state.EntityManager.SetComponentData(mapEntity, mapState);

        used.Dispose();
    }

    static bool IsProtected(int2 cell, int2 coreCell, int safeRadius)
    {
        var dx = math.abs(cell.x - coreCell.x);
        var dy = math.abs(cell.y - coreCell.y);
        return math.max(dx, dy) <= safeRadius;
    }
}