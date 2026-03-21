using Unity.Entities;
using Unity.Mathematics;

public struct ZombieMove : IComponentData
{
    public float Speed;
    public int2 TargetCell;
    public int2 CurrentStepCell;
    public byte HasStepCell;
    public float SeparationRadius;
    public float SeparationWeight;
}