using Unity.Entities;
using Unity.Mathematics;

public struct ZombieMove : IComponentData
{
    public float Speed;
    public float FlowWeight;
    public float LaneBiasStrength;
    public float SeparationRadius;
    public float SeparationWeight;

    public int2 LastGridCell;
    public byte StuckFrames;
    public float2 LastMoveDir;

    // 과도기 호환용
    public int2 TargetCell;
    public int2 CurrentStepCell;
    public byte HasStepCell;
}