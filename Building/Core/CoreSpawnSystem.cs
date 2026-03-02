using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial class CoreSpawnSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GridConfig>();
        RequireForUpdate<CorePrefabRef>();
    }

    protected override void OnStartRunning()
    {
        // 이미 코어가 있으면 생성하지 않음
        if (!SystemAPI.QueryBuilder().WithAll<CoreTag>().Build().IsEmpty) return;

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var prefab = SystemAPI.GetSingleton<CorePrefabRef>().Prefab;

        var core = EntityManager.Instantiate(prefab);

        // 원하는 위치(예: 중앙)
        int2 cell = new int2(cfg.Size.x / 2, cfg.Size.y / 2);
        float3 pos = IsoGridUtility.GridToWorld(cfg, cell);
        EntityManager.SetComponentData(core, LocalTransform.FromPosition(pos));
        EntityManager.AddComponentData(core, new GridCell { Value = cell });

        // GameState 싱글톤도 없으면 생성
        if (!SystemAPI.HasSingleton<GameState>())
        {
            var gs = EntityManager.CreateEntity(typeof(GameState));
            EntityManager.SetComponentData(gs, new GameState { IsGameOver = 0 });
        }
    }

    protected override void OnUpdate() { }
}