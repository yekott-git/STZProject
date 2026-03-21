using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

public partial struct ProjectileMoveSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ProjectileTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (tr, projectile, entity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<Projectile>>()
                     .WithAll<ProjectileTag>()
                     .WithEntityAccess())
        {
            tr.ValueRW.Position.x += projectile.ValueRO.Velocity.x * dt;
            tr.ValueRW.Position.y += projectile.ValueRO.Velocity.y * dt;

            projectile.ValueRW.Lifetime -= dt;
            if (projectile.ValueRO.Lifetime <= 0f)
                ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}