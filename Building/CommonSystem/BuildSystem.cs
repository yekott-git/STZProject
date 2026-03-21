using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(ZombieMoveSystem))]
[UpdateBefore(typeof(ZombieAttackSystem))]
public partial struct BuildSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<WallPrefabRef>();
        state.RequireForUpdate<TurretPrefabRef>();
        state.RequireForUpdate<WallIndexState>();
        state.RequireForUpdate<FlowFieldState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var gridEntity = SystemAPI.GetSingletonEntity<GridConfig>();
        var occBuf = SystemAPI.GetBuffer<OccCell>(gridEntity);
        var width = cfg.Size.x;

        var wallPrefab = SystemAPI.GetSingleton<WallPrefabRef>().Prefab;
        var turretPrefab = SystemAPI.GetSingleton<TurretPrefabRef>().Prefab;
        var wallMap = SystemAPI.GetSingleton<WallIndexState>().Map;

        var query = SystemAPI.QueryBuilder().WithAll<CmdBuild>().Build();

        using var cmdEntities = query.ToEntityArray(Allocator.Temp);
        using var cmds = query.ToComponentDataArray<CmdBuild>(Allocator.Temp);

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var flowDirty = false;

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
            if (occBuf[idx].Value != 0)
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

            occBuf[idx] = new OccCell { Value = 1 };

            var spawned = state.EntityManager.Instantiate(prefab);
            var pos = IsoGridUtility.GridToWorld(cfg, cell);

            state.EntityManager.SetComponentData(spawned, LocalTransform.FromPosition(pos));

            if (!state.EntityManager.HasComponent<GridCell>(spawned))
                state.EntityManager.AddComponentData(spawned, new GridCell { Value = cell });
            else
                state.EntityManager.SetComponentData(spawned, new GridCell { Value = cell });

            if (isWall)
            {
                var key = GridKeyUtility.CellKey(cell, width);
                wallMap[key] = spawned;
                flowDirty = true;
            }

            ecb.DestroyEntity(cmdEntity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();

        if (flowDirty)
        {
            var flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
            var flow = SystemAPI.GetComponentRW<FlowFieldState>(flowEntity);
            flow.ValueRW.Dirty = 1;
        }
    }
}