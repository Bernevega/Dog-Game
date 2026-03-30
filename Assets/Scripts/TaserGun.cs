using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class TaserGun : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Image cooldownFillImage;

    [Header("Input")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string shootActionName = "Shoot";

    [Header("Gun Settings")]
    [SerializeField] private float shootDistance = 100f;
    [SerializeField] private float cooldownDuration = 0.25f;
    [SerializeField] private float bulletSpeed = 45f;
    [SerializeField] private float bulletLifetime = 3f;
    [SerializeField] private float defaultSpawnDistanceFromCamera = 0.5f;
    [SerializeField] private LayerMask aimLayers = ~0;

    [Header("Alien Tag")]
    [SerializeField] private string requiredAlienTag = "Alien";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool drawDebugRay = true;

    private InputSystem_Actions inputActions;
    private InputAction shootAction;

    private bool canShoot = true;
    private float cooldownTimer = 0f;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
        shootAction = inputActions.asset.FindAction(actionMapName + "/" + shootActionName, false);

        if (shootAction == null)
            DebugLogWarning("Could not find input action: " + actionMapName + "/" + shootActionName);

        if (playerCamera == null)
            DebugLogWarning("Player Camera is not assigned.");

        if (bulletPrefab == null)
            DebugLogWarning("Bullet Prefab is not assigned.");

        if (cooldownFillImage == null)
            DebugLogWarning("Cooldown Fill Image is not assigned.");
    }

    private void OnEnable()
    {
        inputActions.Enable();

        if (shootAction != null)
            shootAction.performed += OnShootPerformed;
    }

    private void OnDisable()
    {
        if (shootAction != null)
            shootAction.performed -= OnShootPerformed;

        inputActions.Disable();
    }

    private void Start()
    {
        if (cooldownFillImage != null)
            cooldownFillImage.fillAmount = 1f;
    }

    private void Update()
    {
        UpdateCooldown();
    }

    private void OnShootPerformed(InputAction.CallbackContext context)
    {
        if (!canShoot)
            return;

        Shoot();
    }

    private void Shoot()
    {
        if (playerCamera == null || bulletPrefab == null)
            return;

        canShoot = false;
        cooldownTimer = cooldownDuration;

        Vector3 targetPoint = playerCamera.transform.position + playerCamera.transform.forward * shootDistance;
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (drawDebugRay)
            Debug.DrawRay(ray.origin, ray.direction * shootDistance, Color.yellow, 1.5f);

        if (Physics.Raycast(ray, out RaycastHit hit, shootDistance, aimLayers, QueryTriggerInteraction.Ignore))
        {
            targetPoint = hit.point;
            DebugLog("Aiming at: " + hit.collider.name);
        }

        Vector3 spawnPosition;

        if (firePoint != null)
            spawnPosition = firePoint.position;
        else
            spawnPosition = playerCamera.transform.position + playerCamera.transform.forward * defaultSpawnDistanceFromCamera;

        Vector3 direction = (targetPoint - spawnPosition).normalized;

        if (direction == Vector3.zero)
            direction = playerCamera.transform.forward;

        Quaternion bulletRotation = Quaternion.LookRotation(direction);
        GameObject bulletObject = Instantiate(bulletPrefab, spawnPosition, bulletRotation);

        Bullet bullet = bulletObject.GetComponent<Bullet>();

        if (bullet != null)
        {
            bullet.Initialize(direction, bulletSpeed, bulletLifetime, requiredAlienTag);
        }
        else
        {
            DebugLogWarning("The spawned bullet prefab has no Bullet script attached.");
        }

        UpdateCooldownFillInstant();
    }

    private void UpdateCooldown()
    {
        if (!canShoot)
        {
            cooldownTimer -= Time.deltaTime;

            if (cooldownFillImage != null)
            {
                float progress = 1f - (cooldownTimer / cooldownDuration);
                cooldownFillImage.fillAmount = Mathf.Clamp01(progress);
            }

            if (cooldownTimer <= 0f)
            {
                cooldownTimer = 0f;
                canShoot = true;

                if (cooldownFillImage != null)
                    cooldownFillImage.fillAmount = 1f;
            }
        }
    }

    private void UpdateCooldownFillInstant()
    {
        if (cooldownFillImage != null)
            cooldownFillImage.fillAmount = 0f;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log("[Gun] " + message, this);
    }

    private void DebugLogWarning(string message)
    {
        if (enableDebugLogs)
            Debug.LogWarning("[Gun] " + message, this);
    }
}