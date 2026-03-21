using Unity.Entities;

public struct WaveSpawner : IComponentData
{
    public float Timer;
    public int Wave;

    public float SpawnInterval;
    public int ZombiesToSpawn;
    public int ZombiesSpawned;
    public int ZombiesAlive;

    public float BreakTimer;
    public float BreakDuration;

    public int SpawnSide;
    public byte State;

    public int DebugOverrideSpawnCount;
    public int DebugOverrideBurstCount;
}