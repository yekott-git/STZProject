using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial class ZombieAttackSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<GridConfig>();
        RequireForUpdate<WallIndexState>();
    }

    protected override void OnUpdate()
    {
        var cfg = SystemAPI.GetSingleton<GridConfig>();
        int width = cfg.Size.x;
        var map = SystemAPI.GetSingleton<WallIndexState>().Map;
        float dt = SystemAPI.Time.DeltaTime;

        Entities
            .WithAll<ZombieTag>()
            .ForEach((ref ZombieAttack atk, in LocalTransform tr, in ZombieMove mv) =>
            {
                atk.Timer -= dt;
                if (atk.Timer > 0f) return;

                // 목표 방향 probe로 frontCell 계산 (패치 A 포함)
                float3 targetWorld = IsoGridUtility.GridToWorld(cfg, mv.TargetCell);
                float2 pos = tr.Position.xy;
                float2 dirW = math.normalizesafe(targetWorld.xy - pos);
                float2 probePos = pos + dirW * 0.25f;
                int2 frontCell = IsoGridUtility.WorldToGrid(cfg, probePos);

                if (!IsoGridUtility.InBounds(cfg, frontCell)) return;

                int key = GridKeyUtility.CellKey(frontCell, width);

                if (map.TryGetValue(key, out var wallEntity) && wallEntity != Entity.Null)
                {
                    // ✅ EntityManager 대신 SystemAPI로 직접 RW 접근
                    if (SystemAPI.HasComponent<Health>(wallEntity))
                    {
                        var hpRW = SystemAPI.GetComponentRW<Health>(wallEntity);
                        hpRW.ValueRW.Value -= atk.Damage;
                        atk.Timer = atk.Cooldown;
                    }
                }
            })
            .Run(); // Run이면 지금 단계에선 충분히 안정적
    }
}