using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DamageEventSystem))]
public partial struct HealthDeathSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        var hasWaveSpawner = SystemAPI.HasSingleton<WaveSpawner>();
        var waveSpawnerRW = hasWaveSpawner
            ? SystemAPI.GetSingletonRW<WaveSpawner>()
            : default;

        foreach (var (hp, entity) in SystemAPI
                     .Query<RefRO<Health>>()
                     .WithAll<ZombieTag>()
                     .WithEntityAccess())
        {
            if (hp.ValueRO.Value > 0)
                continue;

            if (hasWaveSpawner)
            {
                waveSpawnerRW.ValueRW.ZombiesAlive =
                    math.max(0, waveSpawnerRW.ValueRO.ZombiesAlive - 1);
            }

            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}