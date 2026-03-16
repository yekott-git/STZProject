using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public partial struct HealthDeathSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WaveSpawner>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var waveSpawnerRW = SystemAPI.GetSingletonRW<WaveSpawner>();

        foreach (var (hp, entity) in SystemAPI
                     .Query<RefRO<Health>>()
                     .WithAll<ZombieTag>()
                     .WithEntityAccess())
        {
            if (hp.ValueRO.Value > 0)
                continue;

            waveSpawnerRW.ValueRW.ZombiesAlive =
                math.max(0, waveSpawnerRW.ValueRO.ZombiesAlive - 1);

            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}