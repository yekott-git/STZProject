using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

public partial class TurretAttackSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<ZombieTag>();
        RequireForUpdate<TurretTag>();
        RequireForUpdate<ProjectilePrefabRef>();
    }

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        // ✅ 쿼리에 LocalTransform을 반드시 포함
        var zQuery = SystemAPI.QueryBuilder()
            .WithAll<ZombieTag, LocalTransform>()
            .Build();

        var tQuery = SystemAPI.QueryBuilder()
            .WithAll<TurretTag, LocalTransform>()
            .WithAllRW<TurretAttack>() // TurretAttack을 읽고/쓸 거라 RW
            .Build();

        using var zEntities   = zQuery.ToEntityArray(Allocator.Temp);
        using var zTransforms = zQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        using var tEntities   = tQuery.ToEntityArray(Allocator.Temp);
        using var tTransforms = tQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using var tAttacks    = tQuery.ToComponentDataArray<TurretAttack>(Allocator.Temp);

        if (zEntities.Length == 0 || tEntities.Length == 0)
            return;

        var projPrefab = SystemAPI.GetSingleton<ProjectilePrefabRef>().Prefab;
        if (projPrefab == Entity.Null) return;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        for (int ti = 0; ti < tEntities.Length; ti++)
        {
            Entity turret = tEntities[ti];
            var atk = tAttacks[ti];

            atk.Timer -= dt;
            if (atk.Timer > 0f)
            {
                // ✅ 배열 수정 금지 → 엔티티에 바로 Set
                EntityManager.SetComponentData(turret, atk);
                continue;
            }

            float2 tPos = tTransforms[ti].Position.xy;
            float rangeSq = atk.Range * atk.Range;

            int bestZi = -1;
            float bestDistSq = float.MaxValue;

            for (int zi = 0; zi < zEntities.Length; zi++)
            {
                float2 zPos = zTransforms[zi].Position.xy;
                float dSq = math.lengthsq(zPos - tPos);
                if (dSq <= rangeSq && dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    bestZi = zi;
                }
            }

            if (bestZi != -1)
            {
                float2 zPos = zTransforms[bestZi].Position.xy;

                float2 dir = math.normalizesafe(zPos - tPos);
                if (math.lengthsq(dir) < 0.0001f) dir = new float2(0, 1);

                var p = EntityManager.Instantiate(projPrefab);

                // 총알 시작 위치 = 터렛 위치
                EntityManager.SetComponentData(p, LocalTransform.FromPosition(new float3(tPos.x, tPos.y, 0)));

                // Projectile 데이터 세팅(속도/데미지/수명)
                var proj = EntityManager.GetComponentData<Projectile>(p);

                // 프리팹의 speed를 쓰고 싶으면 Authoring에서 speed를 Projectile에 넣는 구조로 바꿔도 됨.
                // 지금은 "dir * 18"처럼 고정해도 되고, 아래처럼 lifetime/damage는 프리팹값 사용.
                proj.Velocity = dir * 18f;
                EntityManager.SetComponentData(p, proj);

                atk.Timer = atk.Cooldown;
                EntityManager.SetComponentData(turret, atk);
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}