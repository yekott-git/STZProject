using Unity.Entities;
using Unity.Mathematics;

public struct ZombieMove : IComponentData
{
    public float Speed;
    public int2 TargetCell;        // 기존 유지
    public int2 CurrentStepCell;   // 현재 향하고 있는 다음 셀
    public byte HasStepCell;       // 0 = 없음, 1 = 있음
    public float SeparationRadius;
    public float SeparationWeight;
}