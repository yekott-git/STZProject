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
        GridConfig cfg = SystemAPI.GetSingleton<GridConfig>();
        Entity flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();

        RefRW<FlowFieldState> flowState = SystemAPI.GetComponentRW<FlowFieldState>(flowEntity);
        if (flowState.ValueRO.Dirty == 0)
            return;

        DynamicBuffer<FlowFieldCell> cells = SystemAPI.GetBuffer<FlowFieldCell>(flowEntity);

        Entity occEntity = SystemAPI.GetSingletonEntity<GridOccupancy>();
        DynamicBuffer<OccCell> occ = SystemAPI.GetBuffer<OccCell>(occEntity);

        int width = cfg.Size.x;
        int height = cfg.Size.y;
        int cellCount = width * height;

        for (int i = 0; i < cellCount; i++)
        {
            FlowFieldCell c = cells[i];
            c.Cost = occ[i].Value != 0 ? (byte)255 : (byte)1;
            c.Integration = ushort.MaxValue;
            c.DirX = 0;
            c.DirY = 0;
            cells[i] = c;
        }

        int2 core = flowState.ValueRO.TargetCell;
        if (!IsoGridUtility.InBounds(cfg, core))
        {
            flowState.ValueRW.Dirty = 0;
            return;
        }

        NativeQueue<int> queue = new NativeQueue<int>(Allocator.Temp);

        NativeArray<int2> dirs = new NativeArray<int2>(8, Allocator.Temp);
        dirs[0] = new int2(1, 0);
        dirs[1] = new int2(-1, 0);
        dirs[2] = new int2(0, 1);
        dirs[3] = new int2(0, -1);
        dirs[4] = new int2(1, 1);
        dirs[5] = new int2(1, -1);
        dirs[6] = new int2(-1, 1);
        dirs[7] = new int2(-1, -1);

        NativeArray<int2> goalDirs = new NativeArray<int2>(4, Allocator.Temp);
        goalDirs[0] = new int2(1, 0);
        goalDirs[1] = new int2(-1, 0);
        goalDirs[2] = new int2(0, 1);
        goalDirs[3] = new int2(0, -1);

        // 코어 주변 8칸을 goal로 사용
        for (int i = 0; i < goalDirs.Length; i++)
        {
            int2 goal = core + goalDirs[i];
            if (!IsoGridUtility.InBounds(cfg, goal))
                continue;

            int goalIndex = ToIndex(goal, width);

            FlowFieldCell goalCell = cells[goalIndex];
            goalCell.Cost = 1;
            goalCell.Integration = 0;
            goalCell.DirX = 0;
            goalCell.DirY = 0;
            cells[goalIndex] = goalCell;

            queue.Enqueue(goalIndex);
        }

        while (queue.Count > 0)
        {
            int currentIndex = queue.Dequeue();
            int2 currentCellPos = ToCell(currentIndex, width);
            ushort currentIntegration = cells[currentIndex].Integration;

            for (int i = 0; i < dirs.Length; i++)
            {
                int2 nextCell = currentCellPos + dirs[i];
                if (!IsoGridUtility.InBounds(cfg, nextCell))
                    continue;

                // 대각선 코너 끼기 방지
                bool isDiagonal = math.abs(dirs[i].x) == 1 && math.abs(dirs[i].y) == 1;
                if (isDiagonal)
                {
                    int2 sideA = currentCellPos + new int2(dirs[i].x, 0);
                    int2 sideB = currentCellPos + new int2(0, dirs[i].y);

                    if (!IsoGridUtility.InBounds(cfg, sideA) || !IsoGridUtility.InBounds(cfg, sideB))
                        continue;

                    int sideAIdx = ToIndex(sideA, width);
                    int sideBIdx = ToIndex(sideB, width);

                    if (cells[sideAIdx].Cost == 255 || cells[sideBIdx].Cost == 255)
                        continue;
                }

                int nextIndex = ToIndex(nextCell, width);
                FlowFieldCell next = cells[nextIndex];

                if (next.Cost == 255)
                    continue;

                int moveCost = isDiagonal ? 14 : 10;
                int candidate = currentIntegration + moveCost;

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
                int2 cellPos = new int2(x, y);
                int index = ToIndex(cellPos, width);

                FlowFieldCell current = cells[index];
                if (current.Cost == 255 || current.Integration == ushort.MaxValue)
                    continue;

                int bestScore = int.MaxValue;
                int2 bestCell = cellPos;

                for (int i = 0; i < dirs.Length; i++)
                {
                    int2 nextCell = cellPos + dirs[i];
                    if (!IsoGridUtility.InBounds(cfg, nextCell)) continue;

                    bool isDiagonal = math.abs(dirs[i].x) == 1 && math.abs(dirs[i].y) == 1;
                    if (isDiagonal)
                    {
                        int2 sideA = cellPos + new int2(dirs[i].x, 0);
                        int2 sideB = cellPos + new int2(0, dirs[i].y);
                        if (!IsoGridUtility.InBounds(cfg, sideA) || !IsoGridUtility.InBounds(cfg, sideB)) continue;

                        int sideAIdx = ToIndex(sideA, width);
                        int sideBIdx = ToIndex(sideB, width);
                        if (cells[sideAIdx].Cost == 255 || cells[sideBIdx].Cost == 255) continue;
                    }

                    int nextIndex = ToIndex(nextCell, width);
                    FlowFieldCell next = cells[nextIndex];
                    if (next.Cost == 255 || next.Integration == ushort.MaxValue) continue;

                    int moveCost = isDiagonal ? 14 : 10;
                    int score = next.Integration + moveCost;

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestCell = nextCell;
                    }
                }

                int2 bestDir = bestCell - cellPos;
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

    private static int ToIndex(int2 cell, int width)
    {
        return cell.y * width + cell.x;
    }

    private static int2 ToCell(int index, int width)
    {
        return new int2(index % width, index / width);
    }
}