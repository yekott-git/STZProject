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
    ComponentLookup<GridCell> targetCellLookup;
    ComponentLookup<AttackSlotConfig> slotConfigLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GridConfig>();
        state.RequireForUpdate<AttackSlotState>();
        state.RequireForUpdate<DamageEventQueueTag>();

        healthLookup = state.GetComponentLookup<Health>(true);
        targetCellLookup = state.GetComponentLookup<GridCell>(true);
        slotConfigLookup = state.GetComponentLookup<AttackSlotConfig>(true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        healthLookup.Update(ref state);
        targetCellLookup.Update(ref state);
        slotConfigLookup.Update(ref state);

        var cfg = SystemAPI.GetSingleton<GridConfig>();
        var slotMap = SystemAPI.GetSingleton<AttackSlotState>().SlotOwnerMap;

        var damageQueueEntity = SystemAPI.GetSingletonEntity<DamageEventQueueTag>();
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        var job = new ZombieAttackJob
        {
            Cfg = cfg,
            SlotMap = slotMap,
            HealthLookup = healthLookup,
            TargetCellLookup = targetCellLookup,
            SlotConfigLookup = slotConfigLookup,
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
        [ReadOnly] public NativeParallelHashMap<int, Entity> SlotMap;
        [ReadOnly] public ComponentLookup<Health> HealthLookup;
        [ReadOnly] public ComponentLookup<GridCell> TargetCellLookup;
        [ReadOnly] public ComponentLookup<AttackSlotConfig> SlotConfigLookup;

        public Entity DamageQueueEntity;
        public EntityCommandBuffer.ParallelWriter Ecb;
        public float Dt;

        void Execute(
            [EntityIndexInQuery] int sortKey,
            Entity entity,
            ref ZombieAttack attack,
            in LocalTransform transform,
            in AttackSlotAssignment slotAssignment,
            in ZombieTag zombieTag)
        {
            attack.Timer -= Dt;
            if (attack.Timer > 0f)
                return;

            if (slotAssignment.HasSlot == 0)
                return;

            if (slotAssignment.Target == Entity.Null)
                return;

            if (!HealthLookup.HasComponent(slotAssignment.Target))
                return;

            if (!TargetCellLookup.HasComponent(slotAssignment.Target))
                return;

            if (!SlotConfigLookup.HasComponent(slotAssignment.Target))
                return;

            var slotKey = AttackSlotUtility.MakeSlotKey(slotAssignment.Target, slotAssignment.SlotIndex);
            if (!SlotMap.TryGetValue(slotKey, out var owner) || owner != entity)
                return;

            var targetCell = TargetCellLookup[slotAssignment.Target].Value;
            var slotConfig = SlotConfigLookup[slotAssignment.Target];

            if (!AttackSlotUtility.IsAdjacentForPattern(slotConfig.Pattern, targetCell, slotAssignment.SlotCell))
                return;

            var myPos = transform.Position.xy;
            var slotWorld = IsoGridUtility.GridToWorld(Cfg, slotAssignment.SlotCell).xy;
            var distSq = math.lengthsq(myPos - slotWorld);

            const float attackSnapRange = 0.30f;
            if (distSq > attackSnapRange * attackSnapRange)
                return;

            Ecb.AppendToBuffer(sortKey, DamageQueueEntity, new DamageEvent
            {
                Target = slotAssignment.Target,
                Value = attack.Damage
            });

            attack.Timer = attack.Cooldown;
        }
    }
}