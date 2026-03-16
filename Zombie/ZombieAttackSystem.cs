using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
namespace STZProject.Zombie
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ZombieAttackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GridConfig>();
            state.RequireForUpdate<FlowFieldState>();

            // 이건 네 원래 코드에 있던 맵 singleton 타입으로 바꿔
            state.RequireForUpdate<WallIndexState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            GridConfig cfg = SystemAPI.GetSingleton<GridConfig>();
            float dt = SystemAPI.Time.DeltaTime;
            int width = cfg.Size.x;

            Entity flowEntity = SystemAPI.GetSingletonEntity<FlowFieldState>();
            DynamicBuffer<FlowFieldCell> flowCells = SystemAPI.GetBuffer<FlowFieldCell>(flowEntity);

            // 이 줄도 네 원래 코드 그대로
            var map = SystemAPI.GetSingleton<WallIndexState>().Map;

            ComponentLookup<Health> healthLookup = state.GetComponentLookup<Health>(false);

            foreach (var (atk, tr) in SystemAPI
                         .Query<RefRW<ZombieAttack>, RefRO<LocalTransform>>()
                         .WithAll<ZombieTag>())
            {
                atk.ValueRW.Timer -= dt;
                if (atk.ValueRO.Timer > 0f)
                    continue;

                int2 myCell = IsoGridUtility.WorldToGrid(cfg, tr.ValueRO.Position.xy);
                if (!IsoGridUtility.InBounds(cfg, myCell))
                    continue;

                int idx = myCell.y * width + myCell.x;
                FlowFieldCell flow = flowCells[idx];

                int2 dir = new int2(flow.DirX, flow.DirY);
                if (dir.x == 0 && dir.y == 0)
                    continue;

                int2 frontCell = myCell + dir;
                if (!IsoGridUtility.InBounds(cfg, frontCell))
                    continue;

                int key = GridKeyUtility.CellKey(frontCell, width);

                if (!map.TryGetValue(key, out Entity targetEntity))
                    continue;

                if (targetEntity == Entity.Null)
                    continue;

                if (!healthLookup.HasComponent(targetEntity))
                    continue;

                Health hp = healthLookup[targetEntity];
                hp.Value -= atk.ValueRO.Damage;
                healthLookup[targetEntity] = hp;

                atk.ValueRW.Timer = atk.ValueRO.Cooldown;
            }
        }
    }
}