using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial class TurretAimSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<TurretAttack>();
    }

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (tr, tt) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<TurretTarget>>())
        {
            if (tt.ValueRO.Target == Entity.Null) continue;

            var target = tt.ValueRO.Target;
            if (!SystemAPI.Exists(target) || !SystemAPI.HasComponent<LocalTransform>(target))
            {
                var tmp = tt.ValueRW;
                tmp.Target = Entity.Null;
                tt.ValueRW = tmp;
                continue;
            }

            float2 tPos = tr.ValueRO.Position.xy;
            float2 zPos = SystemAPI.GetComponent<LocalTransform>(target).Position.xy;

            float2 desired = math.normalizesafe(zPos - tPos);
            if (math.lengthsq(desired) < 0.0001f) continue;

            float lerpT = math.saturate(tt.ValueRO.TurnSpeed * dt);
            float2 aim = math.normalizesafe(math.lerp(tt.ValueRO.AimDir, desired, lerpT));

            var ttNew = tt.ValueRW;
            ttNew.AimDir = aim;
            tt.ValueRW = ttNew;

            float ang = math.atan2(aim.y, aim.x);
            tr.ValueRW.Rotation = quaternion.RotateZ(ang);
        }
    }
}