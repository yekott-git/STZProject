using Unity.Entities;
using Unity.Collections;

public partial class ImpactLifetimeSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<Impact>();
    }

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        Entities
            .WithAll<Impact>()
            .ForEach((Entity e, ref Impact im) =>
            {
                im.Lifetime -= dt;
                if (im.Lifetime <= 0f) ecb.DestroyEntity(e);
            })
            .Run();

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}