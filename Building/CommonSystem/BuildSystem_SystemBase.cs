using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial class BuildSystem_SystemBase : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GridConfig>();
        RequireForUpdate<WallPrefabRef>();
        RequireForUpdate<WallIndexState>();
        RequireForUpdate<TurretPrefabRef>();
    }

    protected override void OnUpdate()
    {
        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var gridEntity = SystemAPI.GetSingletonEntity<GridConfig>();
        var occBuf = EntityManager.GetBuffer<OccCell>(gridEntity); // write
        int width = cfg.Size.x;
        
        var wallPrefab = SystemAPI.GetSingleton<WallPrefabRef>().Prefab;
        var turretPrefab = SystemAPI.GetSingleton<TurretPrefabRef>().Prefab;
        if (wallPrefab == Entity.Null) return;

        var map = SystemAPI.GetSingleton<WallIndexState>().Map;

        // CmdBuild들을 직접 루프로 처리 (즉시 Instantiate + 즉시 Map 등록)
        var query = SystemAPI.QueryBuilder().WithAll<CmdBuild>().Build();
        using var cmdEntities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        using var cmds = query.ToComponentDataArray<CmdBuild>(Unity.Collections.Allocator.Temp);

        for (int i = 0; i < cmdEntities.Length; i++)
        {
            var cmdEntity = cmdEntities[i];
            var cmd = cmds[i];

            int2 c = cmd.Cell;
            if (!IsoGridUtility.InBounds(cfg, c))
            {
                EntityManager.DestroyEntity(cmdEntity);
                continue;
            }

            int idx = c.y * width + c.x;

            if (occBuf[idx].Value != 0)
            {
                EntityManager.DestroyEntity(cmdEntity);
                continue;
            }

            // 점유
            occBuf[idx] = new OccCell { Value = 1 };

            Entity prefabToSpawn = cmd.BuildingType == 0 ? wallPrefab : turretPrefab;
            if (prefabToSpawn == Entity.Null) { EntityManager.DestroyEntity(cmdEntity); continue; }

            
            Entity b = EntityManager.Instantiate(prefabToSpawn);

            // 공통으로 셀 저장(나중에 터렛도 파괴되면 점유 해제용)
            float3 pos = IsoGridUtility.GridToWorld(cfg, c);
            EntityManager.SetComponentData(b, LocalTransform.FromPosition(pos));

            if (!EntityManager.HasComponent<GridCell>(b))
                EntityManager.AddComponentData(b, new GridCell { Value = c });
            else
                EntityManager.SetComponentData(b, new GridCell { Value = c });

            // 벽이면 WallIndex(Map) 등록
            if (cmd.BuildingType == 0)
            {
                int key = GridKeyUtility.CellKey(c, width);
                map[key] = b; // map은 GetSingleton<WallIndexState>().Map 핸들로 잡아둔 것
            }

            // 커맨드 제거
            EntityManager.DestroyEntity(cmdEntity);
        }
    }
}