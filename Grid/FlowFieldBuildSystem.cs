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

        // 1) Reset + occupancy 반영
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

        NativeArray<int2> dirs = new NativeArray<int2>(4, Allocator.Temp);
        dirs[0] = new int2(1, 0);
        dirs[1] = new int2(-1, 0);
        dirs[2] = new int2(0, 1);
        dirs[3] = new int2(0, -1);

        // 2) 코어 주변 4칸을 goal로 사용
        for (int i = 0; i < dirs.Length; i++)
        {
            int2 goal = core + dirs[i];
            if (!IsoGridUtility.InBounds(cfg, goal))
                continue;

            int goalIndex = ToIndex(goal, width);

            FlowFieldCell goalCell = cells[goalIndex];
            goalCell.Cost = 1; // goal은 진입 가능 처리
            goalCell.Integration = 0;
            goalCell.DirX = 0;
            goalCell.DirY = 0;
            cells[goalIndex] = goalCell;

            queue.Enqueue(goalIndex);
        }

        // 3) Integration field 생성
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

                int nextIndex = ToIndex(nextCell, width);
                FlowFieldCell next = cells[nextIndex];

                if (next.Cost == 255)
                    continue;

                int candidate = currentIntegration + next.Cost;
                if (candidate < next.Integration)
                {
                    next.Integration = (ushort)candidate;
                    cells[nextIndex] = next;
                    queue.Enqueue(nextIndex);
                }
            }
        }

        // 4) Best direction field 생성
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int2 cellPos = new int2(x, y);
                int index = ToIndex(cellPos, width);

                FlowFieldCell current = cells[index];

                if (current.Cost == 255 || current.Integration == ushort.MaxValue)
                    continue;

                ushort bestValue = current.Integration;
                int2 bestCell = cellPos;

                for (int i = 0; i < dirs.Length; i++)
                {
                    int2 nextCell = cellPos + dirs[i];
                    if (!IsoGridUtility.InBounds(cfg, nextCell))
                        continue;

                    int nextIndex = ToIndex(nextCell, width);
                    ushort nextIntegration = cells[nextIndex].Integration;

                    if (nextIntegration < bestValue)
                    {
                        bestValue = nextIntegration;
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