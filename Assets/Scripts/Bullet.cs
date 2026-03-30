using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private float speed = 45f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private string requiredAlienTag = "Alien";
    [SerializeField] private bool destroyOnAnyHit = true;

    private Vector3 moveDirection;
    private bool isInitialized;

    public void Initialize(Vector3 direction, float bulletSpeed, float bulletLifetime, string alienTag)
    {
        moveDirection = direction.normalized;
        speed = bulletSpeed;
        lifetime = bulletLifetime;
        requiredAlienTag = alienTag;
        isInitialized = true;

        Destroy(gameObject, lifetime);
    }

    private void Start()
    {
        if (!isInitialized)
        {
            moveDirection = transform.forward;
            Destroy(gameObject, lifetime);
        }
    }

    private void Update()
    {
        float moveDistance = speed * Time.deltaTime;

        if (Physics.Raycast(transform.position, moveDirection, out RaycastHit hit, moveDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            Alien alien = hit.collider.GetComponentInParent<Alien>();

            if (alien != null && alien.CompareTag(requiredAlienTag))
            {
                alien.Die();
            }

            if (destroyOnAnyHit || alien != null)
            {
                transform.position = hit.point;
                Destroy(gameObject);
                return;
            }
        }

        transform.position += moveDirection * moveDistance;

        if (moveDirection != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(moveDirection);
    }
}