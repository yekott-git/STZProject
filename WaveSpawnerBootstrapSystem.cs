using Unity.Entities;

public partial struct WaveSpawnerBootstrapSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        if (SystemAPI.HasSingleton<WaveSpawner>())
            return;

        var entity = state.EntityManager.CreateEntity(typeof(WaveSpawner));
        state.EntityManager.SetComponentData(entity, new WaveSpawner
        {
            Timer = 0f,
            Wave = 0,
            SpawnInterval = 0.5f,
            ZombiesToSpawn = 0,
            ZombiesSpawned = 0,
            ZombiesAlive = 0,
            BreakTimer = 2f,
            BreakDuration = 2f,
            SpawnSide = 0,
            State = 0,
            DebugOverrideSpawnCount = 100,
            DebugOverrideBurstCount = 100
        });
    }

    public void OnUpdate(ref SystemState state)
    {
    }
}