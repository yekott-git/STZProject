using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial class ZombieSpawnSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<ZombiePrefabRef>();
        RequireForUpdate<GridConfig>();
    }

    protected override void OnUpdate()
    {
        if (!UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            return;

        var prefab = SystemAPI.GetSingleton<ZombiePrefabRef>().Prefab;
        var cfg = SystemAPI.GetSingleton<GridConfig>();

        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        for (int i = 0; i < 10; i++)
        {
            var z = ecb.Instantiate(prefab);

            int2 cell = new int2(5 + i, 5);
            float3 pos = IsoGridUtility.GridToWorld(cfg, cell);

            ecb.SetComponent(z, LocalTransform.FromPosition(pos));
            ecb.SetComponent(z, new ZombieMove
            {
                Speed = 2f,
                TargetCell = new int2(60, 60)
            });
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}