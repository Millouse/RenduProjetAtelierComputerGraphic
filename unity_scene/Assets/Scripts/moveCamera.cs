// CameraController.cs
// Contrôleur de caméra utilisant le nouveau Input System de Unity 6.
// Dépendances : package "Input System" installé via Package Manager.
// Sur la Camera, ajouter un PlayerInput avec les actions ci-dessous
// (ou laisser le script créer ses propres bindings en runtime).
//
// Actions attendues dans l'Input Action Asset :
//   Move        – Value, Vector2  (WASD / stick gauche)
//   UpDown      – Value, float    (E=+1, Q=-1 / gâchettes)
//   Look        – Value, Vector2  (delta souris / stick droit)
//   Sprint      – Button          (LeftShift / bouton B)
//   RotateHold  – Button          (bouton droit souris)
//   Zoom        – Value, float    (molette)

using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    // ── Déplacement ───────────────────────────────────────────────────────────
    [Header("Déplacement")]
    [Tooltip("Vitesse de déplacement de base")]
    public float moveSpeed = 10f;

    [Tooltip("Multiplicateur sprint")]
    public float sprintMultiplier = 2.5f;

    [Tooltip("Lissage du déplacement (0 = aucun, 0.99 = très lisse)")]
    [Range(0f, 0.99f)]
    public float moveSmoothness = 0.1f;

    // ── Rotation ──────────────────────────────────────────────────────────────
    [Header("Rotation")]
    public float mouseSensitivityX = 0.15f;
    public float mouseSensitivityY = 0.15f;

    [Tooltip("Angle vertical minimum (degrés)")]
    public float minVerticalAngle = -80f;

    [Tooltip("Angle vertical maximum (degrés)")]
    public float maxVerticalAngle = 80f;

    // ── Zoom ──────────────────────────────────────────────────────────────────
    [Header("Zoom")]
    public bool  enableZoom = true;
    public float zoomSpeed  = 5f;
    public float minZoom    = 2f;
    public float maxZoom    = 50f;

    // ── Suivi de cible ────────────────────────────────────────────────────────
    [Header("Suivi de cible")]
    [Tooltip("Laisser vide pour le mode free-fly")]
    public Transform followTarget = null;
    public Vector3   followOffset = new Vector3(0f, 5f, -10f);
    public float     followSpeed  = 5f;

    // ── Privé ─────────────────────────────────────────────────────────────────
    float    _yaw;
    float    _pitch;
    Vector3  _smoothVelocity;
    Camera   _cam;

    // Valeurs lues depuis l'Input System chaque frame
    Vector2 _moveInput;
    float   _upDownInput;
    Vector2 _lookDelta;
    bool    _rotateHeld;
    bool    _sprintHeld;
    float   _zoomInput;

    // Références aux actions (auto-créées si aucun PlayerInput n'est présent)
    InputAction _moveAction;
    InputAction _upDownAction;
    InputAction _lookAction;
    InputAction _rotateHoldAction;
    InputAction _sprintAction;
    InputAction _zoomAction;

    // ── Init ──────────────────────────────────────────────────────────────────
    void Awake()
    {
        _cam = GetComponent<Camera>();

        // Tente de récupérer les actions depuis un PlayerInput sur l'objet.
        // Sinon, crée des bindings par défaut en code.
        PlayerInput playerInput = GetComponent<PlayerInput>();

        if (playerInput != null)
        {
            _moveAction       = playerInput.actions["Move"];
            _upDownAction     = playerInput.actions["UpDown"];
            _lookAction       = playerInput.actions["Look"];
            _rotateHoldAction = playerInput.actions["RotateHold"];
            _sprintAction     = playerInput.actions["Sprint"];
            _zoomAction       = playerInput.actions["Zoom"];
        }
        else
        {
            CreateDefaultActions();
        }
    }

    void Start()
    {
        _yaw   = transform.eulerAngles.y;
        _pitch = transform.eulerAngles.x;
    }

    void OnEnable()
    {
        _moveAction?.Enable();
        _upDownAction?.Enable();
        _lookAction?.Enable();
        _rotateHoldAction?.Enable();
        _sprintAction?.Enable();
        _zoomAction?.Enable();
    }

    void OnDisable()
    {
        _moveAction?.Disable();
        _upDownAction?.Disable();
        _lookAction?.Disable();
        _rotateHoldAction?.Disable();
        _sprintAction?.Disable();
        _zoomAction?.Disable();
    }

    // ── Update ────────────────────────────────────────────────────────────────
    void Update()
    {
        ReadInputs();

        if (followTarget != null)
            HandleFollowTarget();
        else
        {
            HandleMouseRotation();
            HandleMovement();
        }

        if (enableZoom)
            HandleZoom();
    }

    // ── Lecture des actions ───────────────────────────────────────────────────
    void ReadInputs()
    {
        _moveInput    = _moveAction?.ReadValue<Vector2>()   ?? Vector2.zero;
        _upDownInput  = _upDownAction?.ReadValue<float>()   ?? 0f;
        _lookDelta    = _lookAction?.ReadValue<Vector2>()   ?? Vector2.zero;
        _rotateHeld   = _rotateHoldAction?.IsPressed()      ?? false;
        _sprintHeld   = _sprintAction?.IsPressed()          ?? false;
        _zoomInput    = _zoomAction?.ReadValue<float>()     ?? 0f;
    }

    // ── Rotation ──────────────────────────────────────────────────────────────
    void HandleMouseRotation()
    {
        if (!_rotateHeld) return;

        _yaw   += _lookDelta.x * mouseSensitivityX;
        _pitch -= _lookDelta.y * mouseSensitivityY;
        _pitch  = Mathf.Clamp(_pitch, minVerticalAngle, maxVerticalAngle);

        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    // ── Déplacement ───────────────────────────────────────────────────────────
    void HandleMovement()
    {
        float speed = moveSpeed * (_sprintHeld ? sprintMultiplier : 1f);

        Vector3 direction = transform.right   * _moveInput.x
                          + transform.forward * _moveInput.y
                          + transform.up      * _upDownInput;

        Vector3 target = transform.position + direction * speed * Time.deltaTime;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            target,
            ref _smoothVelocity,
            Mathf.Max(moveSmoothness, 0.001f)
        );
    }

    // ── Zoom ──────────────────────────────────────────────────────────────────
    void HandleZoom()
    {
        if (Mathf.Abs(_zoomInput) < 0.01f) return;

        if (_cam != null && !_cam.orthographic)
        {
            transform.position += transform.forward * _zoomInput * zoomSpeed;
        }
        else if (_cam != null && _cam.orthographic)
        {
            _cam.orthographicSize -= _zoomInput * zoomSpeed;
            _cam.orthographicSize  = Mathf.Clamp(_cam.orthographicSize, minZoom, maxZoom);
        }
    }

    // ── Suivi de cible ────────────────────────────────────────────────────────
    void HandleFollowTarget()
    {
        if (_rotateHeld)
        {
            _yaw   += _lookDelta.x * mouseSensitivityX;
            _pitch -= _lookDelta.y * mouseSensitivityY;
            _pitch  = Mathf.Clamp(_pitch, minVerticalAngle, maxVerticalAngle);
        }

        Quaternion rotation       = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3    desiredPosition = followTarget.position + rotation * followOffset;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSpeed * Time.deltaTime
        );

        transform.LookAt(followTarget.position + Vector3.up * (followOffset.y * 0.5f));
    }

    // ── Bindings par défaut (sans PlayerInput) ────────────────────────────────
    void CreateDefaultActions()
    {
        // Move – WASD / flèches
        _moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        _moveAction.AddCompositeBinding("2DVector")
            .With("Up",    "<Keyboard>/w")
            .With("Up",    "<Keyboard>/upArrow")
            .With("Down",  "<Keyboard>/s")
            .With("Down",  "<Keyboard>/downArrow")
            .With("Left",  "<Keyboard>/a")
            .With("Left",  "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/d")
            .With("Right", "<Keyboard>/rightArrow");

        // UpDown – E (monter) / Q (descendre)
        _upDownAction = new InputAction("UpDown", InputActionType.Value, expectedControlType: "Axis");
        _upDownAction.AddCompositeBinding("1DAxis")
            .With("Positive", "<Keyboard>/e")
            .With("Negative", "<Keyboard>/q");

        // Look – delta de la souris (divisé par 10 pour rester cohérent
        //        avec le mode "Delta" qui renvoie des pixels bruts)
        _lookAction = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
        _lookAction.AddBinding("<Mouse>/delta");

        // RotateHold – bouton droit
        _rotateHoldAction = new InputAction("RotateHold", InputActionType.Button);
        _rotateHoldAction.AddBinding("<Mouse>/rightButton");

        // Sprint – LeftShift
        _sprintAction = new InputAction("Sprint", InputActionType.Button);
        _sprintAction.AddBinding("<Keyboard>/leftShift");

        // Zoom – molette Y
        _zoomAction = new InputAction("Zoom", InputActionType.Value, expectedControlType: "Axis");
        _zoomAction.AddBinding("<Mouse>/scroll/y");
    }
}