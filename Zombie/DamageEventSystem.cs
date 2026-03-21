using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ZombieAttackSystem))]
[UpdateAfter(typeof(CoreDamageSystem))]
[UpdateAfter(typeof(ProjectileHitSystem))]
public partial struct DamageEventSystem : ISystem
{
    ComponentLookup<Health> healthLookup;

    public void OnCreate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<DamageEventQueueTag>())
        {
            var e = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<DamageEventQueueTag>(e);
            state.EntityManager.AddBuffer<DamageEvent>(e);
        }

        healthLookup = state.GetComponentLookup<Health>(false);
    }

    public void OnUpdate(ref SystemState state)
    {
        healthLookup.Update(ref state);

        var queueEntity = SystemAPI.GetSingletonEntity<DamageEventQueueTag>();
        var damageBuffer = SystemAPI.GetBuffer<DamageEvent>(queueEntity);
        UnityEngine.Debug.Log("DamageEvent Count: " + damageBuffer.Length);

        if (damageBuffer.Length == 0)
            return;

        for (int i = 0; i < damageBuffer.Length; i++)
        {
            var ev = damageBuffer[i];

            if (ev.Target == Entity.Null)
                continue;

            if (!healthLookup.HasComponent(ev.Target))
                continue;

            var hp = healthLookup[ev.Target];
            hp.Value -= ev.Value;
            healthLookup[ev.Target] = hp;
        }

        damageBuffer.Clear();
    }
}