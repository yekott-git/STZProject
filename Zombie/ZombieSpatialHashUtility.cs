using Unity.Mathematics;

public static class ZombieSpatialHashUtility
{
    public static int Hash(int2 cell)
    {
        unchecked
        {
            return cell.x * 73856093 ^ cell.y * 19349663;
        }
    }
}