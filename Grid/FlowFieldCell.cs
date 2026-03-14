using Unity.Entities;
using Unity.Mathematics;

public struct FlowFieldCell : IBufferElementData
{
    public byte Cost;          // 1 = normal, 255 = blocked
    public ushort Integration; // 65535 = unreachable
    public sbyte DirX;         // -1, 0, 1
    public sbyte DirY;         // -1, 0, 1
}