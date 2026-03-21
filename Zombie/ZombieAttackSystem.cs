using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct ZombieAttackSystem : ISystem
{
    ComponentLookup<Health> healthLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<FlowFieldState>();
        state.RequireForUpdate<WallIndexState>();
        state.RequireForUpdate<DamageEventQueueTag>();

        healthLookup = state.GetComponentLookup<Health>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        healthLookup.Update(ref state);

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var width = cfg.Size.x;

        var flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
        var flowCells = SystemAPI.GetBuffer<FlowFieldCell>(flowEntity).AsNativeArray();

        var wallMap = SystemAPI.GetSingleton<WallIndexState>().Map;

        var damageQueueEntity = SystemAPI.GetSingletonEntity<DamageEventQueueTag>();
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var job = new ZombieAttackJob
        {
            Cfg = cfg,
            Width = width,
            FlowCells = flowCells,
            WallMap = wallMap,
            HealthLookup = healthLookup,
            DamageQueueEntity = damageQueueEntity,
            Ecb = ecb,
            Dt = SystemAPI.Time.DeltaTime
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct ZombieAttackJob : IJobEntity
    {
        [ReadOnly] public GridConfig Cfg;
        [ReadOnly] public int Width;
        [ReadOnly] public NativeArray<FlowFieldCell> FlowCells;
        [ReadOnly] public NativeParallelHashMap<int, Entity> WallMap;
        [ReadOnly] public ComponentLookup<Health> HealthLookup;

        public Entity DamageQueueEntity;
        public EntityCommandBuffer.ParallelWriter Ecb;
        public float Dt;

        void Execute(
            [EntityIndexInQuery] int sortKey,
            ref ZombieAttack attack,
            in LocalTransform transform,
            in ZombieTag zombieTag)
        {
            attack.Timer -= Dt;
            if (attack.Timer > 0f)
                return;

            var myCell = IsoGridUtility.WorldToGrid(Cfg, transform.Position.xy);
            if (!IsoGridUtility.InBounds(Cfg, myCell))
                return;

            var idx = myCell.y * Width + myCell.x;
            var flow = FlowCells[idx];
            var flowDir = new int2(flow.DirX, flow.DirY);

            var bestScore = int.MinValue;
            var targetEntity = Entity.Null;
            var targetCell = new int2(int.MinValue, int.MinValue);

            TrySelect(myCell, new int2(1, 0), flowDir, ref bestScore, ref targetEntity, ref targetCell);
            TrySelect(myCell, new int2(-1, 0), flowDir, ref bestScore, ref targetEntity, ref targetCell);
            TrySelect(myCell, new int2(0, 1), flowDir, ref bestScore, ref targetEntity, ref targetCell);
            TrySelect(myCell, new int2(0, -1), flowDir, ref bestScore, ref targetEntity, ref targetCell);

            if (targetEntity == Entity.Null)
                return;

            if (!HealthLookup.HasComponent(targetEntity))
                return;

            Ecb.AppendToBuffer(sortKey, DamageQueueEntity, new DamageEvent
            {
                Target = targetEntity,
                Value = attack.Damage
            });

            attack.Timer = attack.Cooldown;
        }

        void TrySelect(
            int2 myCell,
            int2 dir,
            int2 flowDir,
            ref int bestScore,
            ref Entity targetEntity,
            ref int2 targetCell)
        {
            var adj = myCell + dir;

            if (!IsoGridUtility.InBounds(Cfg, adj))
                return;

            var key = GridKeyUtility.CellKey(adj, Width);
            if (!WallMap.TryGetValue(key, out var entity))
                return;

            if (entity == Entity.Null)
                return;

            var score = 0;

            if (!(flowDir.x == 0 && flowDir.y == 0))
                score = dir.x * flowDir.x + dir.y * flowDir.y;

            if (score > bestScore)
            {
                bestScore = score;
                targetEntity = entity;
                targetCell = adj;
            }
        }
    }
}