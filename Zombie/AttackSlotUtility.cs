using Unity.Mathematics;
using Unity.Entities;
public static class AttackSlotUtility
{
    public const byte PatternCardinal4 = 0;
    public const byte PatternAround8 = 1;

    public static int2 GetDirection(byte pattern, int slotIndex)
    {
        switch (pattern)
        {
            case PatternAround8:
                switch (slotIndex)
                {
                    case 0: return new int2(1, 0);
                    case 1: return new int2(-1, 0);
                    case 2: return new int2(0, 1);
                    case 3: return new int2(0, -1);
                    case 4: return new int2(1, 1);
                    case 5: return new int2(1, -1);
                    case 6: return new int2(-1, 1);
                    case 7: return new int2(-1, -1);
                    default: return int2.zero;
                }

            default:
                switch (slotIndex)
                {
                    case 0: return new int2(1, 0);
                    case 1: return new int2(-1, 0);
                    case 2: return new int2(0, 1);
                    case 3: return new int2(0, -1);
                    default: return int2.zero;
                }
        }
    }

    public static int GetSlotCount(byte pattern, byte maxAttackers)
    {
        int patternCount = pattern == PatternAround8 ? 8 : 4;
        return math.min(patternCount, math.max(1, maxAttackers));
    }

    public static int MakeSlotKey(Entity target, int slotIndex)
    {
        unchecked
        {
            return (target.Index * 397) ^ slotIndex;
        }
    }

    public static bool IsAdjacentForPattern(byte pattern, int2 targetCell, int2 slotCell)
    {
        var d = slotCell - targetCell;

        if (pattern == PatternAround8)
            return math.abs(d.x) <= 1 && math.abs(d.y) <= 1 && !(d.x == 0 && d.y == 0);

        return math.abs(d.x) + math.abs(d.y) == 1;
    }
}