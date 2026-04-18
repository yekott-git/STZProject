using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(ZombieMoveSystem))]
[UpdateBefore(typeof(ZombieAttackSystem))]
public partial struct BuildSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<GridOccupancy>();
        state.RequireForUpdate<StaticOccupancy>();
        state.RequireForUpdate<WallPrefabRef>();
        state.RequireForUpdate<TurretPrefabRef>();
        state.RequireForUpdate<WallIndexState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var width = cfg.Size.x;

        var dynamicOccEntity = SystemAPI.GetSingletonEntity<GridOccupancy>();
        var dynamicOcc = SystemAPI.GetBuffer<OccCell>(dynamicOccEntity);

        var staticOccEntity = SystemAPI.GetSingletonEntity<StaticOccupancy>();
        var staticOcc = SystemAPI.GetBuffer<StaticOccCell>(staticOccEntity);

        var wallPrefab = SystemAPI.GetSingleton<WallPrefabRef>().Prefab;
        var turretPrefab = SystemAPI.GetSingleton<TurretPrefabRef>().Prefab;
        var defenseMap = SystemAPI.GetSingleton<WallIndexState>().Map;

        var query = SystemAPI.QueryBuilder().WithAll<CmdBuild>().Build();

        using var cmdEntities = query.ToEntityArray(Allocator.Temp);
        using var cmds = query.ToComponentDataArray<CmdBuild>(Allocator.Temp);

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        for (int i = 0; i < cmdEntities.Length; i++)
        {
            var cmdEntity = cmdEntities[i];
            var cmd = cmds[i];
            var cell = cmd.Cell;

            if (!IsoGridUtility.InBounds(cfg, cell))
            {
                ecb.DestroyEntity(cmdEntity);
                continue;
            }

            var idx = cell.y * width + cell.x;
            if (staticOcc[idx].Value != 0 || dynamicOcc[idx].Value != 0)
            {
                ecb.DestroyEntity(cmdEntity);
                continue;
            }

            var isWall = cmd.BuildingType == 0;
            var prefab = isWall ? wallPrefab : turretPrefab;

            if (prefab == Entity.Null)
            {
                ecb.DestroyEntity(cmdEntity);
                continue;
            }

            dynamicOcc[idx] = new OccCell { Value = 1 };

            var spawned = state.EntityManager.Instantiate(prefab);
            var pos = IsoGridUtility.GridToWorld(cfg, cell);

            state.EntityManager.SetComponentData(spawned, LocalTransform.FromPosition(pos));

            if (!state.EntityManager.HasComponent<GridCell>(spawned))
                state.EntityManager.AddComponentData(spawned, new GridCell { Value = cell });
            else
                state.EntityManager.SetComponentData(spawned, new GridCell { Value = cell });

            if (!state.EntityManager.HasComponent<DefenseStructureTag>(spawned))
                state.EntityManager.AddComponent<DefenseStructureTag>(spawned);

            var key = GridKeyUtility.CellKey(cell, width);
            defenseMap[key] = spawned;

            ecb.DestroyEntity(cmdEntity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}