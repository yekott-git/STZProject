using Unity.Entities;

public struct WaveSpawner : IComponentData
{
    public float Timer;
    public int Wave;
}