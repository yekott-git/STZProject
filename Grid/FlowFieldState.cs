using Unity.Entities;
using Unity.Mathematics;

public struct FlowFieldState : IComponentData
{
    public int2 TargetCell;
    public byte Dirty; // 1이면 rebuild
}