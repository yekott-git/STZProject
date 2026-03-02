using Unity.Entities;
using Unity.Mathematics;

public struct TurretTarget : IComponentData
{
    public Entity Target;
    public float2 AimDir;     // 마지막 유효 에임 방향
    public float TurnSpeed;   // rad/sec (또는 deg/sec로 쓰고 변환해도 됨)
}