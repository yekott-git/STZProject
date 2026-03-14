using Unity.Entities;
using Unity.Transforms;

public partial struct HealthDeathSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WaveSpawner>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        var waveSpawnerRW = SystemAPI.GetSingletonRW<WaveSpawner>();

        foreach (var (hp, entity) in
                 SystemAPI.Query<RefRO<Health>>()
                 .WithNone<DeadTag>()
                 .WithEntityAccess())
        {
            if (hp.ValueRO.Value > 0)
                continue;

            ecb.AddComponent<DeadTag>(entity);

            if (SystemAPI.HasComponent<ZombieTag>(entity))
            {
                waveSpawnerRW.ValueRW.ZombiesAlive--;
            }

            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
    }
}