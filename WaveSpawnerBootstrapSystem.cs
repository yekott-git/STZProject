using Unity.Entities;

public partial class WaveSpawnerBootstrapSystem : SystemBase
{
    protected override void OnCreate()
    {
        if (!SystemAPI.HasSingleton<WaveSpawner>())
        {
            var e = EntityManager.CreateEntity(typeof(WaveSpawner));
            EntityManager.SetComponentData(e, new WaveSpawner
            {
                Timer = 0f,
                Wave = 0,

                SpawnInterval = 0.5f,
                ZombiesToSpawn = 0,
                ZombiesSpawned = 0,
                ZombiesAlive = 0,

                BreakTimer = 5f,
                BreakDuration = 5f,

                State = 0
            });
        }
    }

    protected override void OnUpdate()
    {
    }
}