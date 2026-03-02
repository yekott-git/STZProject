using UnityEngine;
using UnityEngine.InputSystem;

public class IsoCameraController : MonoBehaviour
{
    [Header("Pan")]
    public float panSpeed = 12f;          // WASD 이동 속도
    public float dragPanSpeed = 1.0f;     // 마우스 중클/우클 드래그 속도
    public float edgePanSpeed = 12f;      // 화면 가장자리 이동 속도
    public float edgeSize = 12f;          // 가장자리 감지 픽셀

    [Header("Zoom")]
    public float zoomSpeed = 6f;          // 휠 줌 속도
    public float minOrtho = 3f;
    public float maxOrtho = 20f;

    [Header("Bounds (optional)")]
    public bool useBounds = false;
    public Vector2 minXY = new Vector2(-50, -50);
    public Vector2 maxXY = new Vector2( 50,  50);

    Camera _cam;
    bool _dragging;
    Vector3 _dragStartWorld;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = Camera.main;
    }

    void Update()
    {
        if (_cam == null) return;

        float dt = Time.deltaTime;
        Vector3 move = Vector3.zero;

        // 1) WASD/화살표 팬
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
        }
        move = move.normalized * panSpeed * dt;

        // 2) 화면 가장자리 팬(선택)
        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 mp = mouse.position.ReadValue();
            if (mp.x <= edgeSize) move.x -= edgePanSpeed * dt;
            if (mp.x >= Screen.width - edgeSize) move.x += edgePanSpeed * dt;
            if (mp.y <= edgeSize) move.y -= edgePanSpeed * dt;
            if (mp.y >= Screen.height - edgeSize) move.y += edgePanSpeed * dt;
        }

        // 3) 마우스 드래그 팬 (중클 또는 우클)
        if (mouse != null)
        {
            bool dragDown = mouse.middleButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame;
            bool dragUp   = mouse.middleButton.wasReleasedThisFrame || mouse.rightButton.wasReleasedThisFrame;

            if (dragDown)
            {
                _dragging = true;
                _dragStartWorld = _cam.ScreenToWorldPoint(new Vector3(mouse.position.ReadValue().x, mouse.position.ReadValue().y, _cam.nearClipPlane));
            }
            if (dragUp) _dragging = false;

            if (_dragging)
            {
                Vector3 curWorld = _cam.ScreenToWorldPoint(new Vector3(mouse.position.ReadValue().x, mouse.position.ReadValue().y, _cam.nearClipPlane));
                Vector3 delta = _dragStartWorld - curWorld;
                move += new Vector3(delta.x, delta.y, 0f) * dragPanSpeed;
            }
        }

        // 4) 줌(휠) - Orthographic 기준
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // scroll 양수=위로(보통 줌인), 음수=줌아웃. 취향대로 부호 바꿔도 됨.
                _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize - scroll * 0.01f * zoomSpeed, minOrtho, maxOrtho);
            }
        }

        // 이동 적용
        Vector3 p = _cam.transform.position;
        p += new Vector3(move.x, move.y, 0f);

        // 경계 제한(원하면)
        if (useBounds)
        {
            p.x = Mathf.Clamp(p.x, minXY.x, maxXY.x);
            p.y = Mathf.Clamp(p.y, minXY.y, maxXY.y);
        }

        _cam.transform.position = p;
    }
}