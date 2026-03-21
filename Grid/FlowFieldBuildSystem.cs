using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public partial struct FlowFieldBuildSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<FlowFieldState>();
        state.RequireForUpdate<GridOccupancy>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();

        var flowState = SystemAPI.GetComponentRW<FlowFieldState>(flowEntity);
        if (flowState.ValueRO.Dirty == 0)
            return;

        var cells = SystemAPI.GetBuffer<FlowFieldCell>(flowEntity);

        var occEntity = SystemAPI.GetSingletonEntity<GridOccupancy>();
        var occ = SystemAPI.GetBuffer<OccCell>(occEntity);

        var width = cfg.Size.x;
        var height = cfg.Size.y;
        var cellCount = width * height;

        for (var i = 0; i < cellCount; i++)
        {
            var cell = cells[i];
            cell.Cost = occ[i].Value != 0 ? (byte)255 : (byte)1;
            cell.Integration = ushort.MaxValue;
            cell.DirX = 0;
            cell.DirY = 0;
            cells[i] = cell;
        }

        var coreCell = flowState.ValueRO.TargetCell;
        if (!IsoGridUtility.InBounds(cfg, coreCell))
        {
            flowState.ValueRW.Dirty = 0;
            return;
        }

        var queue = new NativeQueue<int>(Allocator.Temp);

        var dirs = new NativeArray<int2>(8, Allocator.Temp);
        dirs[0] = new int2(1, 0);
        dirs[1] = new int2(-1, 0);
        dirs[2] = new int2(0, 1);
        dirs[3] = new int2(0, -1);
        dirs[4] = new int2(1, 1);
        dirs[5] = new int2(1, -1);
        dirs[6] = new int2(-1, 1);
        dirs[7] = new int2(-1, -1);

        var goalDirs = new NativeArray<int2>(4, Allocator.Temp);
        goalDirs[0] = new int2(1, 0);
        goalDirs[1] = new int2(-1, 0);
        goalDirs[2] = new int2(0, 1);
        goalDirs[3] = new int2(0, -1);

        // 코어 인접 4칸을 goal로 사용
        for (var i = 0; i < goalDirs.Length; i++)
        {
            var goal = coreCell + goalDirs[i];
            if (!IsoGridUtility.InBounds(cfg, goal))
                continue;

            var goalIndex = ToIndex(goal, width);

            var goalCell = cells[goalIndex];
            goalCell.Cost = 1;
            goalCell.Integration = 0;
            goalCell.DirX = 0;
            goalCell.DirY = 0;
            cells[goalIndex] = goalCell;

            queue.Enqueue(goalIndex);
        }

        while (queue.Count > 0)
        {
            var currentIndex = queue.Dequeue();
            var currentCell = ToCell(currentIndex, width);
            var currentIntegration = (int)cells[currentIndex].Integration;

            for (var i = 0; i < dirs.Length; i++)
            {
                var dir = dirs[i];
                var nextCell = currentCell + dir;

                if (!IsoGridUtility.InBounds(cfg, nextCell))
                    continue;

                var isDiagonal = math.abs(dir.x) == 1 && math.abs(dir.y) == 1;
                if (isDiagonal)
                {
                    var sideA = currentCell + new int2(dir.x, 0);
                    var sideB = currentCell + new int2(0, dir.y);

                    if (!IsoGridUtility.InBounds(cfg, sideA) || !IsoGridUtility.InBounds(cfg, sideB))
                        continue;

                    var sideAIdx = ToIndex(sideA, width);
                    var sideBIdx = ToIndex(sideB, width);

                    if (cells[sideAIdx].Cost == 255 || cells[sideBIdx].Cost == 255)
                        continue;
                }

                var nextIndex = ToIndex(nextCell, width);
                var next = cells[nextIndex];

                if (next.Cost == 255)
                    continue;

                var moveCost = isDiagonal ? 14 : 10;
                var candidate = currentIntegration + moveCost;

                if (candidate < next.Integration)
                {
                    next.Integration = (ushort)candidate;
                    cells[nextIndex] = next;
                    queue.Enqueue(nextIndex);
                }
            }
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var cellPos = new int2(x, y);
                var index = ToIndex(cellPos, width);

                var current = cells[index];
                if (current.Cost == 255 || current.Integration == ushort.MaxValue)
                    continue;

                var bestScore = int.MaxValue;
                var bestCell = cellPos;

                for (var i = 0; i < dirs.Length; i++)
                {
                    var dir = dirs[i];
                    var nextCell = cellPos + dir;

                    if (!IsoGridUtility.InBounds(cfg, nextCell))
                        continue;

                    var isDiagonal = math.abs(dir.x) == 1 && math.abs(dir.y) == 1;
                    if (isDiagonal)
                    {
                        var sideA = cellPos + new int2(dir.x, 0);
                        var sideB = cellPos + new int2(0, dir.y);

                        if (!IsoGridUtility.InBounds(cfg, sideA) || !IsoGridUtility.InBounds(cfg, sideB))
                            continue;

                        var sideAIdx = ToIndex(sideA, width);
                        var sideBIdx = ToIndex(sideB, width);

                        if (cells[sideAIdx].Cost == 255 || cells[sideBIdx].Cost == 255)
                            continue;
                    }

                    var nextIndex = ToIndex(nextCell, width);
                    var next = cells[nextIndex];

                    if (next.Cost == 255 || next.Integration == ushort.MaxValue)
                        continue;

                    var moveCost = isDiagonal ? 14 : 10;
                    var score = next.Integration + moveCost;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestCell = nextCell;
                    }
                }

                var bestDir = bestCell - cellPos;
                current.DirX = (sbyte)math.clamp(bestDir.x, -1, 1);
                current.DirY = (sbyte)math.clamp(bestDir.y, -1, 1);
                cells[index] = current;
            }
        }

        dirs.Dispose();
        goalDirs.Dispose();
        queue.Dispose();

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