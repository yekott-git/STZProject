using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ZombieSeparationSystem))]
[UpdateAfter(typeof(ZombieNextCellClaimClearSystem))]
public partial struct ZombieMoveSystem : ISystem
{
    ComponentLookup<GridCell> gridCellLookup;
    ComponentLookup<Health> healthLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<FlowFieldState>();
        state.RequireForUpdate<GridOccupancy>();
        state.RequireForUpdate<StaticOccupancy>();
        state.RequireForUpdate<ZombieNextCellClaimState>();

        gridCellLookup = state.GetComponentLookup<GridCell>(true);
        healthLookup = state.GetComponentLookup<Health>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        gridCellLookup.Update(ref state);
        healthLookup.Update(ref state);

        var cfg = SystemAPI.GetSingleton<GridConfig>();

        var flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
        var flowCells = SystemAPI.GetBuffer<FlowFieldCell>(flowEntity).AsNativeArray();

        var dynamicOccEntity = SystemAPI.GetSingletonEntity<GridOccupancy>();
        var dynamicOcc = SystemAPI.GetBuffer<OccCell>(dynamicOccEntity).AsNativeArray();

        var staticOccEntity = SystemAPI.GetSingletonEntity<StaticOccupancy>();
        var staticOcc = SystemAPI.GetBuffer<StaticOccCell>(staticOccEntity).AsNativeArray();

        var claimStateRW = SystemAPI.GetSingletonRW<ZombieNextCellClaimState>();
        var claimMap = claimStateRW.ValueRW.Map;

        var zombieQuery = SystemAPI.QueryBuilder()
            .WithAll<ZombieTag, ZombieMove, ZombieCurrentTarget, ZombieSeparation, LocalTransform>()
            .Build();

        var zombieCount = zombieQuery.CalculateEntityCount();
        var desiredCapacity = math.max(1024, zombieCount * 2);
        if (claimMap.Capacity < desiredCapacity)
            claimMap.Capacity = desiredCapacity;

        var job = new ZombieMoveJob
        {
            Cfg = cfg,
            Width = cfg.Size.x,
            FlowCells = flowCells,
            DynamicOcc = dynamicOcc,
            StaticOcc = staticOcc,
            GridCellLookup = gridCellLookup,
            HealthLookup = healthLookup,
            ClaimMap = claimMap.AsParallelWriter(),
            Dt = SystemAPI.Time.DeltaTime
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct ZombieMoveJob : IJobEntity
    {
        [ReadOnly] public GridConfig Cfg;
        [ReadOnly] public int Width;
        [ReadOnly] public NativeArray<FlowFieldCell> FlowCells;
        [ReadOnly] public NativeArray<OccCell> DynamicOcc;
        [ReadOnly] public NativeArray<StaticOccCell> StaticOcc;
        [ReadOnly] public ComponentLookup<GridCell> GridCellLookup;
        [ReadOnly] public ComponentLookup<Health> HealthLookup;
        public NativeParallelHashMap<int, Entity>.ParallelWriter ClaimMap;
        public float Dt;

        void Execute(
            Entity entity,
            ref LocalTransform transform,
            ref ZombieMove move,
            in ZombieCurrentTarget currentTarget,
            in ZombieSeparation separation,
            in ZombieTag zombieTag)
        {
            var worldPos = transform.Position.xy;
            var currentCell = IsoGridUtility.WorldToGrid(Cfg, worldPos);

            if (!IsoGridUtility.InBounds(Cfg, currentCell))
                return;

            UpdateStuckState(ref move, currentCell);

            int2 nextCell;
            bool hasNext = false;

            if (TryPickTargetAdjacentCell(entity, currentCell, currentTarget.Value, move.LastMoveDir, out nextCell))
            {
                hasNext = true;
            }
            else if (TryPickFlowCell(entity, currentCell, move.LastMoveDir, out nextCell))
            {
                hasNext = true;
            }

            if (!hasNext)
                return;

            if (nextCell.x == currentCell.x && nextCell.y == currentCell.y)
                return;

            var claimKey = GridKeyUtility.CellKey(nextCell, Width);
            if (!ClaimMap.TryAdd(claimKey, entity))
                return;

            MoveTowardCell(entity, ref transform, ref move, worldPos, currentCell, nextCell, separation);
        }

        void UpdateStuckState(ref ZombieMove move, int2 currentCell)
        {
            if (move.LastGridCell.x == currentCell.x && move.LastGridCell.y == currentCell.y)
            {
                if (move.StuckFrames < 255)
                    move.StuckFrames++;
            }
            else
            {
                move.LastGridCell = currentCell;
                move.StuckFrames = 0;
            }
        }

        bool TryPickTargetAdjacentCell(Entity entity, int2 currentCell, Entity target, float2 lastMoveDir, out int2 nextCell)
        {
            nextCell = currentCell;

            if (target == Entity.Null)
                return false;

            if (!GridCellLookup.HasComponent(target))
                return false;

            if (HealthLookup.HasComponent(target) && HealthLookup[target].Value <= 0)
                return false;

            var targetCell = GridCellLookup[target].Value;

            if (IsCardinalAdjacent(currentCell, targetCell))
            {
                nextCell = currentCell;
                return true;
            }

            int bestScore = int.MaxValue;
            bool found = false;

            for (int i = 0; i < 4; i++)
            {
                var attackCell = targetCell + GetCardinalDir(i);

                if (!IsoGridUtility.InBounds(Cfg, attackCell))
                    continue;

                if (!TryPickStepToward(entity, currentCell, attackCell, lastMoveDir, out var candidate))
                    continue;

                var remain = attackCell - candidate;
                int score = math.abs(remain.x) + math.abs(remain.y);

                if (score < bestScore)
                {
                    bestScore = score;
                    nextCell = candidate;
                    found = true;
                }
            }

            return found;
        }

        bool TryPickFlowCell(Entity entity, int2 currentCell, float2 lastMoveDir, out int2 nextCell)
        {
            nextCell = currentCell;

            var idx = currentCell.y * Width + currentCell.x;
            var flow = FlowCells[idx];

            var preferredDir = ToCardinalDir(flow.DirX, flow.DirY);
            var preferred = currentCell + preferredDir;

            if ((preferredDir.x != 0 || preferredDir.y != 0) &&
                CanStep(preferred) &&
                !IsLikelyClaimed(preferred))
            {
                nextCell = preferred;
                return true;
            }

            int bestScore = int.MaxValue;
            bool found = false;

            for (int i = 0; i < 4; i++)
            {
                var dir = GetCardinalDir(i);
                var candidate = currentCell + dir;

                if (!CanStep(candidate))
                    continue;

                var cIdx = candidate.y * Width + candidate.x;
                var cFlow = FlowCells[cIdx];

                if (cFlow.Integration == ushort.MaxValue)
                    continue;

                int score = cFlow.Integration * 10;
                score += ComputeDirectionPenalty(dir, lastMoveDir);
                score += ComputeLaneChoicePenalty(entity, dir);

                if (IsLikelyClaimed(candidate))
                    score += 8;

                if (score < bestScore)
                {
                    bestScore = score;
                    nextCell = candidate;
                    found = true;
                }
            }

            return found;
        }

        bool TryPickStepToward(Entity entity, int2 currentCell, int2 goalCell, float2 lastMoveDir, out int2 nextCell)
        {
            nextCell = currentCell;

            int bestScore = int.MaxValue;
            bool found = false;

            for (int i = 0; i < 4; i++)
            {
                var dir = GetCardinalDir(i);
                var candidate = currentCell + dir;

                if (!CanStep(candidate))
                    continue;

                var delta = goalCell - candidate;
                int score = math.abs(delta.x) + math.abs(delta.y);
                score += ComputeDirectionPenalty(dir, lastMoveDir);
                score += ComputeLaneChoicePenalty(entity, dir);

                if (IsLikelyClaimed(candidate))
                    score += 8;

                if (score < bestScore)
                {
                    bestScore = score;
                    nextCell = candidate;
                    found = true;
                }
            }

            return found;
        }

        void MoveTowardCell(
            Entity entity,
            ref LocalTransform transform,
            ref ZombieMove move,
            float2 worldPos,
            int2 currentCell,
            int2 nextCell,
            in ZombieSeparation separation)
        {
            if (!CanStep(nextCell))
                return;

            var targetPos = IsoGridUtility.GridToWorld(Cfg, nextCell).xy;
            var toTarget = targetPos - worldPos;
            var dist = math.length(toTarget);

            if (dist < 0.02f)
                return;

            var forward = toTarget / math.max(dist, 0.0001f);
            var tangent = new float2(-forward.y, forward.x);

            var nearBlocked = HasAdjacentBlocked(currentCell);

            var sepScale = nearBlocked ? 0.03f : 1f;
            var laneScale = nearBlocked ? 0.01f : 1f;

            if (move.StuckFrames >= 8)
            {
                sepScale *= 0.25f;
                laneScale = 0f;
            }

            var laneBias = ComputeLaneBias(entity);
            var laneOffset = tangent * laneBias * move.LaneBiasStrength * laneScale;
            var sep = separation.Force * move.SeparationWeight * sepScale;

            var desiredDir = forward * math.max(0.85f, move.FlowWeight) + sep + laneOffset;

            var lenSq = math.lengthsq(desiredDir);
            if (lenSq < 0.0001f)
                desiredDir = forward;
            else
                desiredDir *= math.rsqrt(lenSq);

            var moveStep = math.min(move.Speed * Dt, dist);
            var movedPos = worldPos + desiredDir * moveStep;
            var movedCell = IsoGridUtility.WorldToGrid(Cfg, movedPos);

            if (!IsoGridUtility.InBounds(Cfg, movedCell))
                return;

            if ((movedCell.x != currentCell.x || movedCell.y != currentCell.y) && !CanStep(movedCell))
            {
                var tangentAmount = math.dot(desiredDir, tangent);
                var slideDir = tangent * tangentAmount;

                var slideLenSq = math.lengthsq(slideDir);
                if (slideLenSq > 0.0001f)
                {
                    slideDir *= math.rsqrt(slideLenSq);

                    var slidePos = worldPos + slideDir * (moveStep * 0.15f);
                    var slideCell = IsoGridUtility.WorldToGrid(Cfg, slidePos);

                    if (IsoGridUtility.InBounds(Cfg, slideCell) &&
                        slideCell.x == currentCell.x &&
                        slideCell.y == currentCell.y)
                    {
                        transform.Position = new float3(slidePos.x, slidePos.y, transform.Position.z);
                        move.LastMoveDir = slideDir;
                    }
                }

                return;
            }

            transform.Position = new float3(movedPos.x, movedPos.y, transform.Position.z);
            move.LastMoveDir = desiredDir;
        }

        int2 ToCardinalDir(int dx, int dy)
        {
            if (math.abs(dx) >= math.abs(dy))
            {
                if (dx > 0) return new int2(1, 0);
                if (dx < 0) return new int2(-1, 0);
            }

            if (dy > 0) return new int2(0, 1);
            if (dy < 0) return new int2(0, -1);

            return int2.zero;
        }

        int2 GetCardinalDir(int index)
        {
            switch (index)
            {
                case 0: return new int2(1, 0);
                case 1: return new int2(-1, 0);
                case 2: return new int2(0, 1);
                default: return new int2(0, -1);
            }
        }

        int ComputeDirectionPenalty(int2 stepDir, float2 lastMoveDir)
        {
            var lastLenSq = math.lengthsq(lastMoveDir);
            if (lastLenSq < 0.0001f)
                return 0;

            var step = math.normalizesafe(new float2(stepDir.x, stepDir.y));
            var last = math.normalizesafe(lastMoveDir);

            var dot = math.dot(step, last);

            if (dot > 0.85f) return 0;
            if (dot > 0.25f) return 1;
            if (dot > -0.25f) return 3;
            return 6;
        }

        int ComputeLaneChoicePenalty(Entity entity, int2 stepDir)
        {
            uint h = (uint)entity.Index;
            h ^= 0x9E3779B9u;
            h *= 2654435769u;
            h ^= h >> 16;

            bool preferHorizontal = (h & 1u) == 0u;
            bool preferPositive = ((h >> 1) & 1u) == 0u;

            int penalty = 0;

            if (preferHorizontal)
            {
                if (stepDir.y != 0)
                    penalty += 1;
            }
            else
            {
                if (stepDir.x != 0)
                    penalty += 1;
            }

            if (preferPositive)
            {
                if (stepDir.x < 0 || stepDir.y < 0)
                    penalty += 1;
            }
            else
            {
                if (stepDir.x > 0 || stepDir.y > 0)
                    penalty += 1;
            }

            return penalty;
        }

        bool CanStep(int2 toCell)
        {
            if (!IsoGridUtility.InBounds(Cfg, toCell))
                return false;

            var idx = toCell.y * Width + toCell.x;
            return DynamicOcc[idx].Value == 0 && StaticOcc[idx].Value == 0;
        }

        bool HasAdjacentBlocked(int2 currentCell)
        {
            for (int i = 0; i < 4; i++)
            {
                var cell = currentCell + GetCardinalDir(i);
                if (!IsoGridUtility.InBounds(Cfg, cell))
                    continue;

                var idx = cell.y * Width + cell.x;
                if (DynamicOcc[idx].Value != 0 || StaticOcc[idx].Value != 0)
                    return true;
            }

            return false;
        }

        bool IsCardinalAdjacent(int2 a, int2 b)
        {
            var d = a - b;
            return (math.abs(d.x) == 1 && d.y == 0) ||
                   (math.abs(d.y) == 1 && d.x == 0);
        }

        bool IsLikelyClaimed(int2 cell)
        {
            // ParallelWriter는 읽을 수 없어서, 선점 실패 시 막는 방식이 핵심.
            // 여기선 점수 보정용 더미 훅만 남겨둠.
            return false;
        }

        float ComputeLaneBias(Entity entity)
        {
            var h = (uint)entity.Index;
            h ^= 2747636419u;
            h *= 2654435769u;
            h ^= h >> 16;
            h *= 2654435769u;
            h ^= h >> 16;

            var t = (h & 1023u) / 1023f;
            return t * 2f - 1f;
        }
    }
}