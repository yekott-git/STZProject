using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

public class IsoInputBridge : MonoBehaviour
{
    public Camera cam;
    public Transform highlightVisual;
    public int buildingType = 0;

    EntityManager _em;
    EntityQuery _gridQuery;
    bool _initialized;

    void OnEnable()
    {
        // 카메라는 여기서 잡아도 OK
        if (!cam) cam = Camera.main;
        _initialized = false;
    }
    void OnDisable()
    {
        if (_initialized)
        {
            _gridQuery.Dispose();
            _initialized = false;
        }
    }
    bool TryInit()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
            return false;

        _em = world.EntityManager;

        // 한 번만 만들기
        _gridQuery = _em.CreateEntityQuery(
            ComponentType.ReadOnly<GridConfig>(),
            ComponentType.ReadOnly<GridOccupancy>(),
            ComponentType.ReadOnly<OccCell>()
        );

        _initialized = true;
        return true;
    }

    void Update()
    {
        if (!_initialized)
        {
            if (!TryInit()) return; // 월드 준비될 때까지 기다렸다가 다음 프레임에 init
        }

        if (_gridQuery.IsEmptyIgnoreFilter) return;

        var gridEntity = _gridQuery.GetSingletonEntity();
        var cfg = _em.GetComponentData<GridConfig>(gridEntity);

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) buildingType = 0; // Wall
            if (kb.digit2Key.wasPressedThisFrame) buildingType = 1; // Turret
        }

        // 마우스 -> 월드(2D 안전)
        Vector2 m = Mouse.current.position.ReadValue();
        Vector3 w = cam.ScreenToWorldPoint(new Vector3(m.x, m.y, cam.nearClipPlane));
        w.z = 0f;

        int2 cell = IsoGridUtility.WorldToGrid(cfg, new float2(w.x, w.y));

        if (IsoGridUtility.InBounds(cfg, cell))
        {
            float3 hw = IsoGridUtility.GridToWorld(cfg, cell);
            if (highlightVisual)
            {
                highlightVisual.position = new Vector3(hw.x, hw.y, hw.z);
                if (!highlightVisual.gameObject.activeSelf)
                    highlightVisual.gameObject.SetActive(true);
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                var cmdE = _em.CreateEntity(typeof(CmdBuild));
                _em.SetComponentData(cmdE, new CmdBuild { BuildingType = buildingType, Cell = cell });
            }
        }
        else
        {
            if (highlightVisual && highlightVisual.gameObject.activeSelf)
                highlightVisual.gameObject.SetActive(false);
        }
    }
}