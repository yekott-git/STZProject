using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

public partial class ProjectileHitSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<ZombieTag>();
        RequireForUpdate<ProjectileTag>();
    }

    protected override void OnUpdate()
    {
        // 스냅샷: 좀비 (위치만 필요)
        var zQuery = SystemAPI.QueryBuilder()
            .WithAll<ZombieTag, LocalTransform>()
            .Build();

        using var zEntities = zQuery.ToEntityArray(Allocator.Temp);
        using var zTr = zQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        if (zEntities.Length == 0) return;

        // 스냅샷: 투사체
        var pQuery = SystemAPI.QueryBuilder()
            .WithAll<ProjectileTag, LocalTransform, Projectile>()
            .Build();

        using var pEntities = pQuery.ToEntityArray(Allocator.Temp);
        using var pTr = pQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using var pData = pQuery.ToComponentDataArray<Projectile>(Allocator.Temp);

        if (pEntities.Length == 0) return;

        float hitRadius = 0.25f;
        float hitRadiusSq = hitRadius * hitRadius;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        for (int pi = 0; pi < pEntities.Length; pi++)
        {
            Entity pe = pEntities[pi];
            float2 pPos = pTr[pi].Position.xy;
            var proj = pData[pi];

            // 가장 먼저 맞는 좀비 1마리
            for (int zi = 0; zi < zEntities.Length; zi++)
            {
                Entity ze = zEntities[zi];

                // 월드 중간에 죽었을 수도 있으니 방어
                if (!EntityManager.Exists(ze)) continue;
                if (!EntityManager.HasComponent<Health>(ze)) continue;

                float2 zPos = zTr[zi].Position.xy;
                if (math.lengthsq(zPos - pPos) <= hitRadiusSq)
                {
                    // ✅ Health는 EntityManager로 직접 수정
                    var zhp = EntityManager.GetComponentData<Health>(ze);
                    zhp.Value -= proj.Damage;
                    EntityManager.SetComponentData(ze, zhp);

                    if (zhp.Value <= 0)
                    {
                        if(SystemAPI.HasSingleton<WaveSpawner>())
                        {
                            var spRW = SystemAPI.GetSingletonRW<WaveSpawner>();
                            var sp = spRW.ValueRW;
                            sp.ZombiesAlive = math.max(0, sp.ZombiesAlive - 1);
                            spRW.ValueRW = sp;
                        }
                        ecb.DestroyEntity(ze);
                    }
                    

                    // ✅ 임팩트 스폰 (옵션)
                    if (proj.ImpactPrefab != Entity.Null)
                    {
                        var im = EntityManager.Instantiate(proj.ImpactPrefab);
                        EntityManager.SetComponentData(im, LocalTransform.FromPosition(new float3(pPos.x, pPos.y, 0)));
                    }

                    // 투사체 제거
                    ecb.DestroyEntity(pe);
                    break;
                }
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}