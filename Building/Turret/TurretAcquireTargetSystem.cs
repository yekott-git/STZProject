using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

public partial class TurretAcquireTargetSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<TurretAttack>();
    }

    protected override void OnUpdate()
    {
        // 좀비 스냅샷(메인스레드)
        var zQuery = SystemAPI.QueryBuilder()
            .WithAll<ZombieTag, LocalTransform>()
            .Build();

        using var zEntities = zQuery.ToEntityArray(Allocator.Temp);
        using var zTr = zQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        if (zEntities.Length == 0) return;

        var ttLookup = SystemAPI.GetComponentLookup<TurretTarget>(false);

        foreach (var (atk, tTr, turretEntity) in
                    SystemAPI.Query<RefRO<TurretAttack>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            // TurretTarget 없는 터렛은 스킵 (또는 여기서 AddComponent 하고 싶으면 ECB로)
            if (!ttLookup.HasComponent(turretEntity))
                continue;

            var tt = ttLookup[turretEntity];

            float2 tPos = tTr.ValueRO.Position.xy;
            float rangeSq = atk.ValueRO.Range * atk.ValueRO.Range;

            // 1) 기존 타겟 유지 검사
            if (tt.Target != Entity.Null && SystemAPI.Exists(tt.Target) && SystemAPI.HasComponent<LocalTransform>(tt.Target))
            {
                float2 curZPos = SystemAPI.GetComponent<LocalTransform>(tt.Target).Position.xy;
                if (math.lengthsq(curZPos - tPos) <= rangeSq)
                {
                    // 유지
                    ttLookup[turretEntity] = tt;
                    continue;
                }
            }

            // 2) 새 타겟 탐색
            tt.Target = Entity.Null;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < zEntities.Length; i++)
            {
                float2 zPos = zTr[i].Position.xy;
                float dSq = math.lengthsq(zPos - tPos);

                if (dSq <= rangeSq && dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    tt.Target = zEntities[i];
                }
            }

            ttLookup[turretEntity] = tt;
        }
    }
}