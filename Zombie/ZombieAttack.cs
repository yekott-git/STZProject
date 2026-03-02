using Unity.Entities;

public struct ZombieAttack : IComponentData
{
    public float Cooldown;     // 공격 간격(초)
    public float Timer;        // 남은 시간
    public int Damage;         // 데미지
}