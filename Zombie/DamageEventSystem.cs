using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ZombieAttackSystem))]
[UpdateAfter(typeof(ProjectileHitSystem))]
public partial struct DamageEventSystem : ISystem
{
    ComponentLookup<Health> healthLookup;

    public void OnCreate(ref SystemState state)
    {
        healthLookup = state.GetComponentLookup<Health>(false);

        var queueEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponent<DamageEventQueueTag>(queueEntity);
        state.EntityManager.AddBuffer<DamageEvent>(queueEntity);
    }

    public void OnUpdate(ref SystemState state)
    {
        state.CompleteDependency();

        healthLookup.Update(ref state);

        var queueEntity = SystemAPI.GetSingletonEntity<DamageEventQueueTag>();
        var damageBuffer = SystemAPI.GetBuffer<DamageEvent>(queueEntity);

        if (damageBuffer.Length == 0)
            return;

        for (int i = 0; i < damageBuffer.Length; i++)
        {
            var evt = damageBuffer[i];

            if (evt.Target == Entity.Null)
                continue;

            if (!healthLookup.HasComponent(evt.Target))
                continue;

            var hp = healthLookup[evt.Target];
            hp.Value -= evt.Value;
            healthLookup[evt.Target] = hp;
        }

        damageBuffer.Clear();
    }
}