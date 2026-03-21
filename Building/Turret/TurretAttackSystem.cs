using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct TurretAttackSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TurretAttack>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var dt = SystemAPI.Time.DeltaTime;
        var projectileLookup = state.GetComponentLookup<Projectile>(true);

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (atk, target, tr) in
                 SystemAPI.Query<RefRW<TurretAttack>, RefRO<TurretTarget>, RefRO<LocalTransform>>())
        {
            atk.ValueRW.Timer -= dt;

            if (target.ValueRO.Target == Entity.Null)
                continue;

            if (atk.ValueRO.Timer > 0f)
                continue;

            if (atk.ValueRO.ProjectilePrefab == Entity.Null)
                continue;

            if (!projectileLookup.HasComponent(atk.ValueRO.ProjectilePrefab))
                continue;

            atk.ValueRW.Timer = atk.ValueRO.Cooldown;

            var dir = target.ValueRO.AimDir;
            if (math.lengthsq(dir) < 0.0001f)
                dir = new float2(0f, 1f);

            var projectileEntity = ecb.Instantiate(atk.ValueRO.ProjectilePrefab);
            ecb.SetComponent(projectileEntity, LocalTransform.FromPosition(tr.ValueRO.Position));

            var projectile = projectileLookup[atk.ValueRO.ProjectilePrefab];
            projectile.Damage = atk.ValueRO.Damage;
            projectile.Velocity = dir * projectile.Speed;

            ecb.SetComponent(projectileEntity, projectile);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}