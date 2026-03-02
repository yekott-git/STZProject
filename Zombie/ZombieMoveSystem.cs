using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial class ZombieMoveSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GridConfig>();
        RequireForUpdate<GridOccupancy>();
    }

    protected override void OnUpdate()
    {
        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var gridEntity = SystemAPI.GetSingletonEntity<GridConfig>();
        var occ = EntityManager.GetBuffer<OccCell>(gridEntity, true); // read-only
        int width = cfg.Size.x;

        float dt = SystemAPI.Time.DeltaTime;

        Entities
            .WithAll<ZombieTag>()
            .ForEach((ref LocalTransform tr, in ZombieMove move) =>
            {
                // 1) 목표 셀 중심의 월드 좌표
                float3 targetWorld = IsoGridUtility.GridToWorld(cfg, move.TargetCell);

                // 2) 목표까지 방향(월드 좌표에서)
                float2 pos = tr.Position.xy;
                float2 to = targetWorld.xy;
                float2 delta = to - pos;

                float dist = math.length(delta);
                if (dist < 0.01f)
                    return;

                float2 dir = delta / dist;
                float2 nextPos = pos + dir * move.Speed * dt;

                // 3) 다음 위치가 속할 셀을 계산해서 점유 확인
                int2 nextCell = IsoGridUtility.WorldToGrid(cfg, nextPos);
                if (!IsoGridUtility.InBounds(cfg, nextCell))
                    return;

                int idx = nextCell.y * width + nextCell.x;

                // 점유면 정지 (나중에 "벽 공격"으로 바꿀 자리)
                if (occ[idx].Value != 0)
                    return;

                tr.Position.x = nextPos.x;
                tr.Position.y = nextPos.y;

            }).Run(); // 지금은 Run 유지
    }
}