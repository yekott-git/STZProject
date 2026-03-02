using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

public partial class TurretAttackSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<TurretAttack>();
    }

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (atk, tt, tr) in
                    SystemAPI.Query<RefRW<TurretAttack>, RefRO<TurretTarget>, RefRO<LocalTransform>>())
        {
            // 타겟 없으면 쿨다운만 줄이고 끝
            var atkData = atk.ValueRW;
            atkData.Timer -= dt;

            if (tt.ValueRO.Target == Entity.Null)
            {
                atk.ValueRW = atkData;
                continue;
            }

            if (atkData.Timer > 0f)
            {
                atk.ValueRW = atkData;
                continue;
            }

            // 발사
            atkData.Timer = atkData.Cooldown;
            atk.ValueRW = atkData;

            float2 dir = tt.ValueRO.AimDir;
            if (math.lengthsq(dir) < 0.0001f)
                dir = new float2(0, 1);

            float2 tPos = tr.ValueRO.Position.xy;
            
            if (atkData.ProjectilePrefab == Entity.Null)
                continue;

            Entity projEntity = EntityManager.Instantiate(atkData.ProjectilePrefab);
            EntityManager.SetComponentData(
                projEntity,
                LocalTransform.FromPosition(new float3(tPos.x, tPos.y, 0))
            );

            var proj = EntityManager.GetComponentData<Projectile>(projEntity);

            // ✅ 터렛 Damage를 투사체에 주입 (Projectile에 Damage 필드가 있으니 맞음)
            proj.Damage = atkData.Damage;

            // ✅ Speed는 투사체 프리팹 값 사용
            proj.Velocity = dir * proj.Speed;

            EntityManager.SetComponentData(projEntity, proj);
        }
    }
}
