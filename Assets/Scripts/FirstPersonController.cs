using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraHolder;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;

    [Header("Look")]
    [Range(0.1f, 50f)] [SerializeField] private float mouseSensitivity = 15f;
    [Range(10f, 300f)] [SerializeField] private float gamepadSensitivity = 120f;
    [SerializeField] private float minPitch = -90f;
    [SerializeField] private float maxPitch = 90f;

    [Header("Camera Shake - General")]
    [Range(0f, 30f)] [SerializeField] private float walkShakeSpeed = 8f;
    [Range(0f, 30f)] [SerializeField] private float sprintShakeSpeed = 13f;
    [Range(0f, 10f)] [SerializeField] private float idleShakeSpeed = 1.5f;
    [Range(1f, 30f)] [SerializeField] private float shakeReturnSpeed = 10f;

    [Header("Camera Shake - Randomness")]
    [Range(0f, 2f)] [SerializeField] private float walkRandomness = 0.35f;
    [Range(0f, 2f)] [SerializeField] private float sprintRandomness = 0.5f;
    [Range(0f, 2f)] [SerializeField] private float idleRandomness = 0.2f;
    [Range(0.1f, 5f)] [SerializeField] private float randomRefreshSpeed = 1.2f;

    [Header("Camera Shake - Idle Position")]
    [Range(0f, 0.05f)] [SerializeField] private float idleShakeX = 0.003f;
    [Range(0f, 0.05f)] [SerializeField] private float idleShakeY = 0.004f;
    [Range(0f, 0.05f)] [SerializeField] private float idleShakeZ = 0.002f;

    [Header("Camera Shake - Idle Rotation")]
    [Range(0f, 5f)] [SerializeField] private float idleRotPitch = 0.15f;
    [Range(0f, 5f)] [SerializeField] private float idleRotYaw = 0.15f;
    [Range(0f, 5f)] [SerializeField] private float idleRotRoll = 0.1f;

    [Header("Camera Shake - Walk Position")]
    [Range(0f, 0.2f)] [SerializeField] private float walkShakeX = 0.015f;
    [Range(0f, 0.2f)] [SerializeField] private float walkShakeY = 0.04f;
    [Range(0f, 0.2f)] [SerializeField] private float walkShakeZ = 0.01f;

    [Header("Camera Shake - Sprint Position")]
    [Range(0f, 0.3f)] [SerializeField] private float sprintShakeX = 0.03f;
    [Range(0f, 0.3f)] [SerializeField] private float sprintShakeY = 0.08f;
    [Range(0f, 0.3f)] [SerializeField] private float sprintShakeZ = 0.02f;

    [Header("Camera Shake - Walk Rotation")]
    [Range(0f, 10f)] [SerializeField] private float walkRotPitch = 0.5f;
    [Range(0f, 10f)] [SerializeField] private float walkRotYaw = 0.3f;
    [Range(0f, 10f)] [SerializeField] private float walkRotRoll = 1f;

    [Header("Camera Shake - Sprint Rotation")]
    [Range(0f, 15f)] [SerializeField] private float sprintRotPitch = 1f;
    [Range(0f, 15f)] [SerializeField] private float sprintRotYaw = 0.6f;
    [Range(0f, 15f)] [SerializeField] private float sprintRotRoll = 2f;

    private CharacterController controller;
    private InputSystem_Actions inputActions;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 velocity;

    private float pitch;
    private bool jumpPressed;
    private bool sprintHeld;
    private bool usingGamepad;

    private Vector3 cameraHolderStartLocalPos;

    private float walkShakeTimer;
    private float sprintShakeTimer;
    private float idleShakeTimer;
    private float randomTimer;

    private Vector3 currentRandomPosMul = Vector3.one;
    private Vector3 currentRandomRotMul = Vector3.one;
    private Vector3 targetRandomPosMul = Vector3.one;
    private Vector3 targetRandomRotMul = Vector3.one;

    private float seedPosX;
    private float seedPosY;
    private float seedPosZ;
    private float seedRotX;
    private float seedRotY;
    private float seedRotZ;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputActions = new InputSystem_Actions();

        if (cameraHolder != null)
        {
            cameraHolderStartLocalPos = cameraHolder.localPosition;
        }

        seedPosX = Random.Range(0f, 1000f);
        seedPosY = Random.Range(0f, 1000f);
        seedPosZ = Random.Range(0f, 1000f);
        seedRotX = Random.Range(0f, 1000f);
        seedRotY = Random.Range(0f, 1000f);
        seedRotZ = Random.Range(0f, 1000f);

        SetNewRandomTargets(0.3f);
        currentRandomPosMul = targetRandomPosMul;
        currentRandomRotMul = targetRandomRotMul;
    }

    private void OnEnable()
    {
        inputActions.Enable();

        inputActions.Player.Move.performed += OnMovePerformed;
        inputActions.Player.Move.canceled += OnMoveCanceled;

        inputActions.Player.Look.performed += OnLookPerformed;
        inputActions.Player.Look.canceled += OnLookCanceled;

        inputActions.Player.Jump.performed += OnJumpPerformed;

        inputActions.Player.Sprint.performed += OnSprintPerformed;
        inputActions.Player.Sprint.canceled += OnSprintCanceled;
    }

    private void OnDisable()
    {
        inputActions.Player.Move.performed -= OnMovePerformed;
        inputActions.Player.Move.canceled -= OnMoveCanceled;

        inputActions.Player.Look.performed -= OnLookPerformed;
        inputActions.Player.Look.canceled -= OnLookCanceled;

        inputActions.Player.Jump.performed -= OnJumpPerformed;

        inputActions.Player.Sprint.performed -= OnSprintPerformed;
        inputActions.Player.Sprint.canceled -= OnSprintCanceled;

        inputActions.Disable();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();
        HandleCameraShake();
    }

    private void HandleMovement()
    {
        bool isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        float currentSpeed = sprintHeld ? sprintSpeed : walkSpeed;

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * currentSpeed * Time.deltaTime);

        if (jumpPressed && isGrounded && !sprintHeld)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        jumpPressed = false;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleLook()
    {
        if (cameraHolder == null)
            return;

        float yawInput;
        float pitchInput;

        if (usingGamepad)
        {
            yawInput = lookInput.x * gamepadSensitivity * Time.deltaTime;
            pitchInput = lookInput.y * gamepadSensitivity * Time.deltaTime;
        }
        else
        {
            yawInput = lookInput.x * mouseSensitivity * Time.deltaTime;
            pitchInput = lookInput.y * mouseSensitivity * Time.deltaTime;
        }

        pitch -= pitchInput;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.Rotate(Vector3.up * yawInput);
    }

    private void HandleCameraShake()
    {
        if (cameraHolder == null)
            return;

        bool isGrounded = controller.isGrounded;
        bool isMoving = moveInput.sqrMagnitude > 0.01f;
        bool isSprinting = isMoving && sprintHeld;

        UpdateRandomnessState(isMoving, isSprinting);

        Vector3 targetLocalPos = cameraHolderStartLocalPos;
        Vector3 targetLocalRotOffset = Vector3.zero;

        if (isGrounded && isMoving)
        {
            float speed = isSprinting ? sprintShakeSpeed : walkShakeSpeed;

            if (isSprinting)
            {
                sprintShakeTimer += Time.deltaTime * speed;
                Vector3 posOffset = GetShakePositionOffset(
                    sprintShakeTimer,
                    sprintShakeX,
                    sprintShakeY,
                    sprintShakeZ,
                    sprintRandomness
                );

                Vector3 rotOffset = GetShakeRotationOffset(
                    sprintShakeTimer,
                    sprintRotPitch,
                    sprintRotYaw,
                    sprintRotRoll,
                    sprintRandomness
                );

                targetLocalPos += posOffset;
                targetLocalRotOffset = rotOffset;
            }
            else
            {
                walkShakeTimer += Time.deltaTime * speed;
                Vector3 posOffset = GetShakePositionOffset(
                    walkShakeTimer,
                    walkShakeX,
                    walkShakeY,
                    walkShakeZ,
                    walkRandomness
                );

                Vector3 rotOffset = GetShakeRotationOffset(
                    walkShakeTimer,
                    walkRotPitch,
                    walkRotYaw,
                    walkRotRoll,
                    walkRandomness
                );

                targetLocalPos += posOffset;
                targetLocalRotOffset = rotOffset;
            }

            idleShakeTimer = 0f;
        }
        else
        {
            walkShakeTimer = 0f;
            sprintShakeTimer = 0f;

            idleShakeTimer += Time.deltaTime * idleShakeSpeed;

            Vector3 idlePosOffset = GetIdlePositionOffset();
            Vector3 idleRotOffset = GetIdleRotationOffset();

            targetLocalPos += idlePosOffset;
            targetLocalRotOffset = idleRotOffset;
        }

        cameraHolder.localPosition = Vector3.Lerp(
            cameraHolder.localPosition,
            targetLocalPos,
            shakeReturnSpeed * Time.deltaTime
        );

        Quaternion targetRotation = Quaternion.Euler(
            pitch + targetLocalRotOffset.x,
            targetLocalRotOffset.y,
            targetLocalRotOffset.z
        );

        cameraHolder.localRotation = Quaternion.Lerp(
            cameraHolder.localRotation,
            targetRotation,
            shakeReturnSpeed * Time.deltaTime
        );
    }

    private Vector3 GetShakePositionOffset(float timer, float xAmount, float yAmount, float zAmount, float randomnessAmount)
    {
        float x = GetLayeredWave(timer, 1.0f, seedPosX) * xAmount;
        float y = GetLayeredWave(timer, 1.35f, seedPosY) * yAmount;
        float z = GetLayeredWave(timer, 1.7f, seedPosZ) * zAmount;

        Vector3 baseOffset = new Vector3(x, y, z);
        Vector3 randomizedOffset = Vector3.Scale(baseOffset, currentRandomPosMul);

        return Vector3.Lerp(baseOffset, randomizedOffset, Mathf.Clamp01(randomnessAmount));
    }

    private Vector3 GetShakeRotationOffset(float timer, float pitchAmount, float yawAmount, float rollAmount, float randomnessAmount)
    {
        float x = GetLayeredWave(timer, 1.15f, seedRotX) * pitchAmount;
        float y = GetLayeredWave(timer, 1.45f, seedRotY) * yawAmount;
        float z = GetLayeredWave(timer, 1.8f, seedRotZ) * rollAmount;

        Vector3 baseOffset = new Vector3(x, y, z);
        Vector3 randomizedOffset = Vector3.Scale(baseOffset, currentRandomRotMul);

        return Vector3.Lerp(baseOffset, randomizedOffset, Mathf.Clamp01(randomnessAmount));
    }

    private Vector3 GetIdlePositionOffset()
    {
        float x = GetLayeredWave(idleShakeTimer, 0.45f, seedPosX + 100f) * idleShakeX;
        float y = GetLayeredWave(idleShakeTimer, 0.6f, seedPosY + 100f) * idleShakeY;
        float z = GetLayeredWave(idleShakeTimer, 0.4f, seedPosZ + 100f) * idleShakeZ;

        Vector3 baseOffset = new Vector3(x, y, z);
        Vector3 randomizedOffset = Vector3.Scale(baseOffset, currentRandomPosMul);

        return Vector3.Lerp(baseOffset, randomizedOffset, Mathf.Clamp01(idleRandomness));
    }

    private Vector3 GetIdleRotationOffset()
    {
        float x = GetLayeredWave(idleShakeTimer, 0.5f, seedRotX + 100f) * idleRotPitch;
        float y = GetLayeredWave(idleShakeTimer, 0.4f, seedRotY + 100f) * idleRotYaw;
        float z = GetLayeredWave(idleShakeTimer, 0.35f, seedRotZ + 100f) * idleRotRoll;

        Vector3 baseOffset = new Vector3(x, y, z);
        Vector3 randomizedOffset = Vector3.Scale(baseOffset, currentRandomRotMul);

        return Vector3.Lerp(baseOffset, randomizedOffset, Mathf.Clamp01(idleRandomness));
    }

    private float GetLayeredWave(float timer, float frequency, float seed)
    {
        float waveA = Mathf.Sin((timer + seed) * frequency);
        float waveB = Mathf.Sin((timer + seed * 0.37f) * (frequency * 1.83f)) * 0.5f;
        float waveC = Mathf.Cos((timer + seed * 0.19f) * (frequency * 0.71f)) * 0.3f;

        return (waveA + waveB + waveC) / 1.8f;
    }

    private void UpdateRandomnessState(bool isMoving, bool isSprinting)
    {
        randomTimer += Time.deltaTime * randomRefreshSpeed;

        if (randomTimer >= 1f)
        {
            randomTimer = 0f;

            float amount = idleRandomness;

            if (isMoving)
            {
                amount = isSprinting ? sprintRandomness : walkRandomness;
            }

            SetNewRandomTargets(amount);
        }

        currentRandomPosMul = Vector3.Lerp(currentRandomPosMul, targetRandomPosMul, Time.deltaTime * shakeReturnSpeed);
        currentRandomRotMul = Vector3.Lerp(currentRandomRotMul, targetRandomRotMul, Time.deltaTime * shakeReturnSpeed);
    }

    private void SetNewRandomTargets(float randomnessAmount)
    {
        float min = 1f - randomnessAmount;
        float max = 1f + randomnessAmount;

        targetRandomPosMul = new Vector3(
            Random.Range(min, max),
            Random.Range(min, max),
            Random.Range(min, max)
        );

        targetRandomRotMul = new Vector3(
            Random.Range(min, max),
            Random.Range(min, max),
            Random.Range(min, max)
        );
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
        usingGamepad = context.control.device is Gamepad;
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        moveInput = Vector2.zero;
    }

    private void OnLookPerformed(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
        usingGamepad = context.control.device is Gamepad;
    }

    private void OnLookCanceled(InputAction.CallbackContext context)
    {
        lookInput = Vector2.zero;
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        jumpPressed = true;
        usingGamepad = context.control.device is Gamepad;
    }

    private void OnSprintPerformed(InputAction.CallbackContext context)
    {
        sprintHeld = true;
        usingGamepad = context.control.device is Gamepad;
    }

    private void OnSprintCanceled(InputAction.CallbackContext context)
    {
        sprintHeld = false;
    }
}