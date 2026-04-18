using Unity.Entities;
using Unity.Mathematics;

public struct RandomMapState : IComponentData
{
    public uint Seed;
    public int PatchCount;
    public int PatchRadiusMin;
    public int PatchRadiusMax;
    public int SafeRadius;
    public int CorridorHalfWidth;
    public int ScatterCount;
    public byte Generated;
    public byte Applied;
}

public struct RandomMapObstacleCell : IBufferElementData
{
    public int2 Value;
}