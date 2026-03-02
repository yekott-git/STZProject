using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial class WaveSpawnSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GridConfig>();
        RequireForUpdate<ZombiePrefabRef>();
        RequireForUpdate<WaveSpawner>();
        RequireForUpdate<GameState>();
    }

    protected override void OnUpdate()
    {
        var gs = SystemAPI.GetSingleton<GameState>();
        if (gs.IsGameOver != 0) return;

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var prefab = SystemAPI.GetSingleton<ZombiePrefabRef>().Prefab;
        var spRW = SystemAPI.GetSingletonRW<WaveSpawner>();

        float dt = SystemAPI.Time.DeltaTime;
        spRW.ValueRW.Timer -= dt;
        if (spRW.ValueRW.Timer > 0f) return;

        spRW.ValueRW.Wave += 1;
        int wave = spRW.ValueRW.Wave;

        // 다음 웨이브 타이머 (점점 짧게/길게 취향대로)
        spRW.ValueRW.Timer = math.max(3f, 10f - wave * 0.2f);

        // 스폰 수 증가
        int count = 5 + wave * 3;

        // 목표는 코어 셀
        var coreEntity = SystemAPI.GetSingletonEntity<CoreTag>();
        int2 coreCell = SystemAPI.GetComponent<GridCell>(coreEntity).Value;

        // 스폰 위치: 맵 가장자리 랜덤(간단)
        var rng = new Unity.Mathematics.Random((uint)(1234 + wave * 999));

        for (int i = 0; i < count; i++)
        {
            int side = rng.NextInt(4);
            int2 cell = side switch
            {
                0 => new int2(0, rng.NextInt(0, cfg.Size.y)),
                1 => new int2(cfg.Size.x - 1, rng.NextInt(0, cfg.Size.y)),
                2 => new int2(rng.NextInt(0, cfg.Size.x), 0),
                _ => new int2(rng.NextInt(0, cfg.Size.x), cfg.Size.y - 1),
            };

            var z = EntityManager.Instantiate(prefab);

            float3 pos = IsoGridUtility.GridToWorld(cfg, cell);
            EntityManager.SetComponentData(z, LocalTransform.FromPosition(pos));

            // 목표 설정
            var mv = EntityManager.GetComponentData<ZombieMove>(z);
            mv.TargetCell = coreCell;
            EntityManager.SetComponentData(z, mv);
        }
    }
}