using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial class CoreDamageSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
        RequireForUpdate<CoreTag>();
        RequireForUpdate<GridConfig>();
    }

    protected override void OnUpdate()
    {
        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var gsRW = SystemAPI.GetSingletonRW<GameState>();
        if (gsRW.ValueRW.IsGameOver != 0) return;

        // 코어 1개 가정
        var coreEntity = SystemAPI.GetSingletonEntity<CoreTag>();
        var coreCell = SystemAPI.GetComponent<GridCell>(coreEntity).Value;

        float dt = SystemAPI.Time.DeltaTime;

        // 코어에 붙어있는 좀비가 코어를 때림
        Entities.WithAll<ZombieTag>()
            .ForEach((ref ZombieAttack atk, in LocalTransform tr) =>
            {
                atk.Timer -= dt;
                if (atk.Timer > 0f) return;

                int2 zCell = IsoGridUtility.WorldToGrid(cfg, tr.Position.xy);

                // 코어 셀에 들어오면 공격
                if (zCell.x == coreCell.x && zCell.y == coreCell.y)
                {
                    var hpRW = SystemAPI.GetComponentRW<Health>(coreEntity);
                    hpRW.ValueRW.Value -= atk.Damage;
                    
                    atk.Timer = atk.Cooldown;
                }
            }).Run();

        // 게임오버 체크
        var coreHP = SystemAPI.GetComponent<Health>(coreEntity).Value;
        if (coreHP <= 0)
            gsRW.ValueRW.IsGameOver = 1;
    }
}