using Unity.Entities;

public partial class WaveSpawnerBootstrapSystem : SystemBase
{
    protected override void OnCreate()
    {
        if (!SystemAPI.HasSingleton<WaveSpawner>())
        {
            var e = EntityManager.CreateEntity(typeof(WaveSpawner));
            EntityManager.SetComponentData(e, new WaveSpawner { Timer = 5f, Wave = 0 });
        }
    }

    protected override void OnUpdate() { }
}