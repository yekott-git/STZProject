using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public partial struct FlowFieldBuildSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<FlowFieldState>();
        state.RequireForUpdate<StaticOccupancy>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();

        var flowState = SystemAPI.GetComponentRW<FlowFieldState>(flowEntity);
        if (flowState.ValueRO.Dirty == 0)
            return;

        var cells = SystemAPI.GetBuffer<FlowFieldCell>(flowEntity);

        var staticOccEntity = SystemAPI.GetSingletonEntity<StaticOccupancy>();
        var staticOcc = SystemAPI.GetBuffer<StaticOccCell>(staticOccEntity);

        var width = cfg.Size.x;
        var height = cfg.Size.y;
        var cellCount = width * height;

        for (int i = 0; i < cellCount; i++)
        {
            var cell = cells[i];
            cell.Cost = staticOcc[i].Value != 0 ? (byte)255 : (byte)1;
            cell.Integration = ushort.MaxValue;
            cell.DirX = 0;
            cell.DirY = 0;
            cells[i] = cell;
        }

        var targetCell = flowState.ValueRO.TargetCell;
        if (!IsoGridUtility.InBounds(cfg, targetCell))
        {
            flowState.ValueRW.Dirty = 0;
            return;
        }

        var queue = new NativeQueue<int>(Allocator.Temp);

        var dirs = new NativeArray<int2>(4, Allocator.Temp);
        dirs[0] = new int2(1, 0);
        dirs[1] = new int2(-1, 0);
        dirs[2] = new int2(0, 1);
        dirs[3] = new int2(0, -1);

        int goalCount = 0;

        for (int i = 0; i < dirs.Length; i++)
        {
            var goal = targetCell + dirs[i];
            if (!IsoGridUtility.InBounds(cfg, goal))
                continue;

            var goalIndex = ToIndex(goal, width);
            if (staticOcc[goalIndex].Value != 0)
                continue;

            var goalCell = cells[goalIndex];
            goalCell.Cost = 1;
            goalCell.Integration = 0;
            goalCell.DirX = 0;
            goalCell.DirY = 0;
            cells[goalIndex] = goalCell;

            queue.Enqueue(goalIndex);
            goalCount++;
        }

        if (goalCount == 0)
        {
            queue.Dispose();
            dirs.Dispose();
            flowState.ValueRW.Dirty = 0;
            return;
        }

        while (queue.Count > 0)
        {
            var currentIndex = queue.Dequeue();
            var currentCell = ToCell(currentIndex, width);
            var currentIntegration = (int)cells[currentIndex].Integration;

            for (int i = 0; i < dirs.Length; i++)
            {
                var nextCell = currentCell + dirs[i];
                if (!IsoGridUtility.InBounds(cfg, nextCell))
                    continue;

                var nextIndex = ToIndex(nextCell, width);
                var next = cells[nextIndex];

                if (next.Cost == 255)
                    continue;

                var candidate = currentIntegration + 10;

                if (candidate < next.Integration)
                {
                    next.Integration = (ushort)candidate;
                    cells[nextIndex] = next;
                    queue.Enqueue(nextIndex);
                }
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var cellPos = new int2(x, y);
                var index = ToIndex(cellPos, width);

                var current = cells[index];
                if (current.Cost == 255 || current.Integration == ushort.MaxValue)
                    continue;

                int bestScore = current.Integration;
                int2 bestDir = int2.zero;

                for (int i = 0; i < dirs.Length; i++)
                {
                    var nextCell = cellPos + dirs[i];
                    if (!IsoGridUtility.InBounds(cfg, nextCell))
                        continue;

                    var nextIndex = ToIndex(nextCell, width);
                    var next = cells[nextIndex];

                    if (next.Cost == 255 || next.Integration == ushort.MaxValue)
                        continue;

                    if (next.Integration < bestScore)
                    {
                        bestScore = next.Integration;
                        bestDir = dirs[i];
                    }
                }

                current.DirX = (sbyte)bestDir.x;
                current.DirY = (sbyte)bestDir.y;
                cells[index] = current;
            }
        }

        queue.Dispose();
        dirs.Dispose();

        flowState.ValueRW.Dirty = 0;
    }

    static int ToIndex(int2 cell, int width)
    {
        return cell.y * width + cell.x;
    }

    static int2 ToCell(int index, int width)
    {
        return new int2(index % width, index / width);
    }
}