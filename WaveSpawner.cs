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

    public byte State; // 0 = Break, 1 = Spawning
}