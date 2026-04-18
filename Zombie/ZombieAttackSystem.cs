using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ZombieMoveSystem))]
public partial struct ZombieAttackSystem : ISystem
{
    ComponentLookup<Health> healthLookup;
    ComponentLookup<GridCell> gridCellLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<DamageEventQueueTag>();

        healthLookup = state.GetComponentLookup<Health>(true);
        gridCellLookup = state.GetComponentLookup<GridCell>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        healthLookup.Update(ref state);
        gridCellLookup.Update(ref state);

        var cfg = SystemAPI.GetSingleton<GridConfig>();

        var damageQueueEntity = SystemAPI.GetSingletonEntity<DamageEventQueueTag>();
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var job = new ZombieAttackJob
        {
            Cfg = cfg,
            HealthLookup = healthLookup,
            GridCellLookup = gridCellLookup,
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
        [ReadOnly] public ComponentLookup<Health> HealthLookup;
        [ReadOnly] public ComponentLookup<GridCell> GridCellLookup;

        public Entity DamageQueueEntity;
        public EntityCommandBuffer.ParallelWriter Ecb;
        public float Dt;

        void Execute(
            [EntityIndexInQuery] int sortKey,
            Entity entity,
            ref ZombieAttack attack,
            in LocalTransform transform,
            in ZombieCurrentTarget currentTarget,
            in ZombieTag zombieTag)
        {
            attack.Timer -= Dt;
            if (attack.Timer > 0f)
                return;

            var target = currentTarget.Value;
            if (target == Entity.Null)
                return;

            if (!GridCellLookup.HasComponent(target))
                return;

            if (HealthLookup.HasComponent(target) && HealthLookup[target].Value <= 0)
                return;

            var myCell = IsoGridUtility.WorldToGrid(Cfg, transform.Position.xy);
            var targetCell = GridCellLookup[target].Value;

            if (!IsCardinalAdjacent(myCell, targetCell))
                return;

            Ecb.AppendToBuffer(sortKey, DamageQueueEntity, new DamageEvent
            {
                Target = target,
                Value = attack.Damage
            });

            attack.Timer = attack.Cooldown;
        }

        bool IsCardinalAdjacent(int2 a, int2 b)
        {
            var d = a - b;
            return (math.abs(d.x) == 1 && d.y == 0) ||
                   (math.abs(d.y) == 1 && d.x == 0);
        }
    }
}