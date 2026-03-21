using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

public class IsoInputBridge : MonoBehaviour
{
    public Camera cam;
    public Transform highlightVisual;
    public int buildingType = 0;

    EntityManager entityManager;
    EntityQuery gridQuery;
    bool initialized;

    void OnEnable()
    {
        if (!cam)
            cam = Camera.main;

        initialized = false;
    }

    void OnDisable()
    {
        if (initialized)
        {
            gridQuery.Dispose();
            initialized = false;
        }
    }

    bool TryInit()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
            return false;

        entityManager = world.EntityManager;

        gridQuery = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<GridConfig>(),
            ComponentType.ReadOnly<GridOccupancy>(),
            ComponentType.ReadOnly<OccCell>());

        initialized = true;
        return true;
    }

    void Update()
    {
        if (!initialized && !TryInit())
            return;

        if (cam == null || Mouse.current == null)
            return;

        if (gridQuery.IsEmptyIgnoreFilter)
            return;

        var gridEntity = gridQuery.GetSingletonEntity();
        var cfg = entityManager.GetComponentData<GridConfig>(gridEntity);

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame)
                buildingType = 0;

            if (keyboard.digit2Key.wasPressedThisFrame)
                buildingType = 1;
        }

        var mousePos = Mouse.current.position.ReadValue();
        var world = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, cam.nearClipPlane));
        world.z = 0f;

        var cell = IsoGridUtility.WorldToGrid(cfg, new float2(world.x, world.y));

        if (!IsoGridUtility.InBounds(cfg, cell))
        {
            if (highlightVisual && highlightVisual.gameObject.activeSelf)
                highlightVisual.gameObject.SetActive(false);

            return;
        }

        var highlightPos = IsoGridUtility.GridToWorld(cfg, cell);

        if (highlightVisual)
        {
            highlightVisual.position = new Vector3(highlightPos.x, highlightPos.y, highlightPos.z);

            if (!highlightVisual.gameObject.activeSelf)
                highlightVisual.gameObject.SetActive(true);
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            var cmdEntity = entityManager.CreateEntity(typeof(CmdBuild));
            entityManager.SetComponentData(cmdEntity, new CmdBuild
            {
                BuildingType = buildingType,
                Cell = cell
            });
        }
    }
}