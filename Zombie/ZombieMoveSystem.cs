using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct ZombieMoveSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<FlowFieldState>();
        state.RequireForUpdate<GridOccupancy>();
        state.RequireForUpdate<WallIndexState>();
    }

    public void OnUpdate(ref SystemState state)
    {
        GridConfig cfg = SystemAPI.GetSingleton<GridConfig>();
        Entity flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
        DynamicBuffer<FlowFieldCell> flowCells = SystemAPI.GetBuffer<FlowFieldCell>(flowEntity);

        Entity occEntity = SystemAPI.GetSingletonEntity<GridOccupancy>();
        DynamicBuffer<OccCell> occ = SystemAPI.GetBuffer<OccCell>(occEntity);

        var map = SystemAPI.GetSingleton<WallIndexState>().Map;

        int width = cfg.Size.x;
        float dt = SystemAPI.Time.DeltaTime;

        // 1) 현재 프레임 좀비 위치 스냅샷
        NativeList<float2> zombiePositions = new NativeList<float2>(Allocator.Temp);

        foreach (var tr in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<ZombieTag>())
        {
            zombiePositions.Add(tr.ValueRO.Position.xy);
        }

        // 2) 이동
        int selfIndex = 0;

        foreach (var (transform, move) in SystemAPI
                     .Query<RefRW<LocalTransform>, RefRW<ZombieMove>>()
                     .WithAll<ZombieTag>())
        {
            float2 worldPos = transform.ValueRO.Position.xy;
            int2 currentCell = IsoGridUtility.WorldToGrid(cfg, worldPos);

            if (!IsoGridUtility.InBounds(cfg, currentCell))
            {
                selfIndex++;
                continue;
            }

            // 인접 공격 대상 있으면 이동 중지
            bool hasAdjacentTarget = false;

            int2 a0 = currentCell + new int2(1, 0);
            int2 a1 = currentCell + new int2(-1, 0);
            int2 a2 = currentCell + new int2(0, 1);
            int2 a3 = currentCell + new int2(0, -1);

            if (IsoGridUtility.InBounds(cfg, a0) &&
                map.TryGetValue(GridKeyUtility.CellKey(a0, width), out Entity e0) &&
                e0 != Entity.Null)
                hasAdjacentTarget = true;

            if (!hasAdjacentTarget &&
                IsoGridUtility.InBounds(cfg, a1) &&
                map.TryGetValue(GridKeyUtility.CellKey(a1, width), out Entity e1) &&
                e1 != Entity.Null)
                hasAdjacentTarget = true;

            if (!hasAdjacentTarget &&
                IsoGridUtility.InBounds(cfg, a2) &&
                map.TryGetValue(GridKeyUtility.CellKey(a2, width), out Entity e2) &&
                e2 != Entity.Null)
                hasAdjacentTarget = true;

            if (!hasAdjacentTarget &&
                IsoGridUtility.InBounds(cfg, a3) &&
                map.TryGetValue(GridKeyUtility.CellKey(a3, width), out Entity e3) &&
                e3 != Entity.Null)
                hasAdjacentTarget = true;

            if (hasAdjacentTarget)
            {
                move.ValueRW.HasStepCell = 0;
                selfIndex++;
                continue;
            }

            // step cell 없으면 새로 선택
            if (move.ValueRO.HasStepCell == 0)
            {
                int index = currentCell.y * width + currentCell.x;
                FlowFieldCell flow = flowCells[index];

                int2 dir = new int2(flow.DirX, flow.DirY);
                if (dir.x == 0 && dir.y == 0)
                {
                    selfIndex++;
                    continue;
                }

                int2 nextCell = currentCell + dir;
                if (!IsoGridUtility.InBounds(cfg, nextCell))
                {
                    selfIndex++;
                    continue;
                }

                int nextIdx = nextCell.y * width + nextCell.x;
                if (occ[nextIdx].Value != 0)
                {
                    selfIndex++;
                    continue;
                }

                move.ValueRW.CurrentStepCell = nextCell;
                move.ValueRW.HasStepCell = 1;
            }

            int2 stepCell = move.ValueRO.CurrentStepCell;
            if (!IsoGridUtility.InBounds(cfg, stepCell))
            {
                move.ValueRW.HasStepCell = 0;
                selfIndex++;
                continue;
            }

            int stepIdx = stepCell.y * width + stepCell.x;
            if (occ[stepIdx].Value != 0)
            {
                move.ValueRW.HasStepCell = 0;
                selfIndex++;
                continue;
            }

            float3 stepWorld3 = IsoGridUtility.GridToWorld(cfg, stepCell);
            float2 stepWorld = stepWorld3.xy;
            float2 toStep = stepWorld - worldPos;
            float dist = math.length(toStep);

            if (dist < 0.05f)
            {
                LocalTransform snap = transform.ValueRO;
                snap.Position = stepWorld3;
                transform.ValueRW = snap;

                move.ValueRW.HasStepCell = 0;
                selfIndex++;
                continue;
            }

            float2 baseMoveDir = toStep / dist;

            // separation 계산
            float2 separation = float2.zero;
            float radius = move.ValueRO.SeparationRadius;
            float radiusSq = radius * radius;

            for (int i = 0; i < zombiePositions.Length; i++)
            {
                if (i == selfIndex)
                    continue;

                float2 other = zombiePositions[i];
                float2 away = worldPos - other;
                float dSq = math.lengthsq(away);

                if (dSq <= 0.0001f || dSq > radiusSq)
                    continue;

                float d = math.sqrt(dSq);
                float strength = 1f - (d / radius);
                separation += (away / d) * strength;
            }

            float2 finalDir = baseMoveDir;

            if (math.lengthsq(separation) > 0.0001f)
            {
                float2 side = separation - baseMoveDir * math.dot(separation, baseMoveDir);

                if (math.lengthsq(side) > 0.0001f)
                {
                    float2 blended = baseMoveDir + side * move.ValueRO.SeparationWeight;
                    finalDir = math.normalize(blended);

                    if (math.dot(finalDir, baseMoveDir) < 0.35f)
                        finalDir = baseMoveDir;
                }
            }

            float step = math.min(move.ValueRO.Speed * dt, dist);
            float2 nextPos = worldPos + finalDir * step;

            LocalTransform tr = transform.ValueRO;
            tr.Position.x = nextPos.x;
            tr.Position.y = nextPos.y;
            transform.ValueRW = tr;

            selfIndex++;
        }

        zombiePositions.Dispose();
    }
}