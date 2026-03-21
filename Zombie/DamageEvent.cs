using Unity.Entities;

public struct DamageEventQueueTag : IComponentData
{
}

public struct DamageEvent : IBufferElementData
{
    public Entity Target;
    public int Value;
}