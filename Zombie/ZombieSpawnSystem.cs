using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct ZombieSpawnSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ZombiePrefabRef>();
        state.RequireForUpdate<GridConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null || !keyboard.spaceKey.wasPressedThisFrame)
            return;

        var prefab = SystemAPI.GetSingleton<ZombiePrefabRef>().Prefab;
        var cfg = SystemAPI.GetSingleton<GridConfig>();

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        for (int i = 0; i < 10; i++)
        {
            var zombie = ecb.Instantiate(prefab);

            var cell = new int2(5 + i, 5);
            var pos = IsoGridUtility.GridToWorld(cfg, cell);

            ecb.SetComponent(zombie, LocalTransform.FromPosition(pos));
            ecb.SetComponent(zombie, new ZombieMove
            {
                Speed = 2f,
                TargetCell = new int2(60, 60),
                CurrentStepCell = int2.zero,
                HasStepCell = 0,
                SeparationRadius = 0.75f,
                SeparationWeight = 1.25f
            });
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}