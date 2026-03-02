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
                Entity zombie = zEntities[bestZi];

                if (EntityManager.Exists(zombie) && EntityManager.HasComponent<Health>(zombie))
                {
                    var hp = EntityManager.GetComponentData<Health>(zombie);
                    hp.Value -= atk.Damage;
                    EntityManager.SetComponentData(zombie, hp);

                    if (hp.Value <= 0)
                        ecb.DestroyEntity(zombie);
                }

                atk.Timer = atk.Cooldown;
                EntityManager.SetComponentData(turret, atk);
            }
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}