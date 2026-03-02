using Unity.Entities;
using Unity.Mathematics;

public struct ZombieMove : IComponentData
{
    public float Speed;
    public int2 TargetCell;
}