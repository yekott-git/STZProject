using Unity.Entities;

public struct StaticOccupancy : IComponentData
{
}

public struct StaticOccCell : IBufferElementData
{
    public byte Value;
}