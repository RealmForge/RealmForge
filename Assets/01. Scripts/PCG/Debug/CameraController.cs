using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float fastMoveMultiplier = 3f;
    [SerializeField] private float smoothTime = 0.1f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 3f;
    [SerializeField] private float minVerticalAngle = -89f;
    [SerializeField] private float maxVerticalAngle = 89f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minFOV = 15f;
    [SerializeField] private float maxFOV = 90f;

    [Header("Controls")]
    [SerializeField] private KeyCode forwardKey = KeyCode.W;
    [SerializeField] private KeyCode backwardKey = KeyCode.S;
    [SerializeField] private KeyCode leftKey = KeyCode.A;
    [SerializeField] private KeyCode rightKey = KeyCode.D;
    [SerializeField] private KeyCode upKey = KeyCode.E;
    [SerializeField] private KeyCode downKey = KeyCode.Q;
    [SerializeField] private KeyCode fastMoveKey = KeyCode.LeftShift;
    [SerializeField] private int rotationMouseButton = 1; // 0: Left, 1: Right, 2: Middle

    private Camera _camera;
    private Vector3 _targetPosition;
    private Vector3 _currentVelocity;
    private float _rotationX;
    private float _rotationY;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            _camera = Camera.main;
        }
    }

    private void Start()
    {
        _targetPosition = transform.position;
        Vector3 euler = transform.eulerAngles;
        _rotationX = euler.y;
        _rotationY = euler.x;
    }

    private void Update()
    {
        HandleMovement();
        HandleRotation();
        HandleZoom();
    }

    private void HandleMovement()
    {
        Vector3 direction = Vector3.zero;

        if (Input.GetKey(forwardKey)) direction += transform.forward;
        if (Input.GetKey(backwardKey)) direction -= transform.forward;
        if (Input.GetKey(rightKey)) direction += transform.right;
        if (Input.GetKey(leftKey)) direction -= transform.right;
        if (Input.GetKey(upKey)) direction += Vector3.up;
        if (Input.GetKey(downKey)) direction -= Vector3.up;

        float currentSpeed = moveSpeed;
        if (Input.GetKey(fastMoveKey))
        {
            currentSpeed *= fastMoveMultiplier;
        }

        _targetPosition += direction.normalized * currentSpeed * Time.deltaTime;
        transform.position = Vector3.SmoothDamp(transform.position, _targetPosition, ref _currentVelocity, smoothTime);
    }

    private void HandleRotation()
    {
        if (Input.GetMouseButton(rotationMouseButton))
        {
            float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed;

            _rotationX += mouseX;
            _rotationY -= mouseY;
            _rotationY = Mathf.Clamp(_rotationY, minVerticalAngle, maxVerticalAngle);

            transform.rotation = Quaternion.Euler(_rotationY, _rotationX, 0f);
        }
    }

    private void HandleZoom()
    {
        if (_camera == null) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float newFOV = _camera.fieldOfView - scroll * zoomSpeed * 10f;
            _camera.fieldOfView = Mathf.Clamp(newFOV, minFOV, maxFOV);
        }
    }

    public void SetPosition(Vector3 position)
    {
        transform.position = position;
        _targetPosition = position;
    }

    public void SetRotation(float horizontal, float vertical)
    {
        _rotationX = horizontal;
        _rotationY = Mathf.Clamp(vertical, minVerticalAngle, maxVerticalAngle);
        transform.rotation = Quaternion.Euler(_rotationY, _rotationX, 0f);
    }

    public void LookAt(Vector3 target)
    {
        transform.LookAt(target);
        Vector3 euler = transform.eulerAngles;
        _rotationX = euler.y;
        _rotationY = euler.x;
        if (_rotationY > 180f) _rotationY -= 360f;
    }
}
