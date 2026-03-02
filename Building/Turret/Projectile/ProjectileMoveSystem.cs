using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;

public partial class ProjectileMoveSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<ProjectileTag>();
    }

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        Entities
            .WithAll<ProjectileTag>()
            .ForEach((Entity e, ref LocalTransform tr, ref Projectile p) =>
            {
                tr.Position.x += p.Velocity.x * dt;
                tr.Position.y += p.Velocity.y * dt;

                p.Lifetime -= dt;
                if (p.Lifetime <= 0f)
                    ecb.DestroyEntity(e);

            }).Run();

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}