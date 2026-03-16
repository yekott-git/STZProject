using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial class CoreSpawnSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GridConfig>();
        RequireForUpdate<CorePrefabRef>();
        RequireForUpdate<WallIndexState>();
        RequireForUpdate<FlowFieldState>();
    }

    protected override void OnStartRunning()
    {
        // 이미 코어가 있으면 생성하지 않음
        if (!SystemAPI.QueryBuilder().WithAll<CoreTag>().Build().IsEmpty)
            return;

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var prefab = SystemAPI.GetSingleton<CorePrefabRef>().Prefab;

        if (prefab == Entity.Null)
            return;

        var core = EntityManager.Instantiate(prefab);

        // 중앙 셀에 생성
        int2 cell = new int2(cfg.Size.x / 2, cfg.Size.y / 2);
        float3 pos = IsoGridUtility.GridToWorld(cfg, cell);

        EntityManager.SetComponentData(core, LocalTransform.FromPosition(pos));

        if (!EntityManager.HasComponent<GridCell>(core))
            EntityManager.AddComponentData(core, new GridCell { Value = cell });
        else
            EntityManager.SetComponentData(core, new GridCell { Value = cell });

        // 코어 셀 occupancy 등록
        var gridEntity = SystemAPI.GetSingletonEntity<GridConfig>();
        var occBuf = EntityManager.GetBuffer<OccCell>(gridEntity);

        int width = cfg.Size.x;
        int idx = cell.y * width + cell.x;
        occBuf[idx] = new OccCell { Value = 1 };

        // 코어를 공격 대상 map에 등록
        var map = SystemAPI.GetSingleton<WallIndexState>().Map;
        int key = GridKeyUtility.CellKey(cell, width);
        map[key] = core;

        // flow field 재빌드 요청
        var flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
        var flow = SystemAPI.GetComponentRW<FlowFieldState>(flowEntity);
        flow.ValueRW.TargetCell = cell;
        flow.ValueRW.Dirty = 1;

        // GameState 싱글톤 없으면 생성
        if (!SystemAPI.HasSingleton<GameState>())
        {
            var gs = EntityManager.CreateEntity(typeof(GameState));
            EntityManager.SetComponentData(gs, new GameState { IsGameOver = 0 });
        }
    }

    protected override void OnUpdate()
    {
    }
}