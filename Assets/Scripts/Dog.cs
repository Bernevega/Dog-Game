using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class Dog : MonoBehaviour
{
    private enum DogState
    {
        Wandering,
        Waiting,
        WatchingPlayer,
        ReturningToPlayer
    }

    [Header("References")]
    [SerializeField] private Transform player;

    [Header("Distance Settings")]
    [Min(0.1f)]
    [SerializeField] private float wanderRadius = 8f;

    [Min(0f)]
    [SerializeField] private float proximityRadius = 2f;

    [Header("Wander Settings")]
    [Min(1)]
    [SerializeField] private int maxDestinationAttempts = 25;

    [Min(0.1f)]
    [SerializeField] private float navMeshSampleDistance = 3f;

    [Min(0.05f)]
    [SerializeField] private float pointReachDistance = 0.8f;

    [Min(0f)]
    [SerializeField] private float minWaitTime = 1f;

    [Min(0f)]
    [SerializeField] private float maxWaitTime = 3f;

    [Header("Movement Speeds")]
    [Min(0f)]
    [SerializeField] private float wanderSpeed = 2.5f;

    [Min(0f)]
    [SerializeField] private float returnSpeed = 4.5f;

    [Header("Rotation")]
    [Min(0f)]
    [SerializeField] private float lookRotationSpeed = 8f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color wanderRadiusColor = Color.green;
    [SerializeField] private Color proximityRadiusColor = Color.yellow;
    [SerializeField] private Color destinationColor = Color.cyan;

    private NavMeshAgent agent;
    private Coroutine waitCoroutine;

    private DogState currentState = DogState.Wandering;
    private Vector3 currentDestination;
    private bool hasDestination;

    private float wanderRadiusSqr;
    private float proximityRadiusSqr;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        CacheDerivedValues();
    }

    private void OnValidate()
    {
        if (maxWaitTime < minWaitTime)
            maxWaitTime = minWaitTime;

        CacheDerivedValues();
    }

    private void Start()
    {
        if (player == null)
        {
            Debug.LogError("Dog: Player reference is missing.", this);
            enabled = false;
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("Dog: NavMeshAgent is not placed on a valid NavMesh.", this);
            enabled = false;
            return;
        }

        agent.speed = wanderSpeed;
        agent.isStopped = false;

        EnterWanderingState();
    }

    private void Update()
    {
        if (!CanRun())
            return;

        float sqrDistanceToPlayer = GetFlatSqrDistance(transform.position, player.position);

        switch (currentState)
        {
            case DogState.Wandering:
                UpdateWandering(sqrDistanceToPlayer);
                break;

            case DogState.Waiting:
                UpdateWaiting(sqrDistanceToPlayer);
                break;

            case DogState.WatchingPlayer:
                UpdateWatchingPlayer(sqrDistanceToPlayer);
                break;

            case DogState.ReturningToPlayer:
                UpdateReturningToPlayer(sqrDistanceToPlayer);
                break;
        }
    }

    private bool CanRun()
    {
        return player != null && agent != null && agent.isOnNavMesh;
    }

    private void UpdateWandering(float sqrDistanceToPlayer)
    {
        if (sqrDistanceToPlayer <= proximityRadiusSqr)
        {
            EnterWatchingPlayerState();
            return;
        }

        if (sqrDistanceToPlayer > wanderRadiusSqr)
        {
            EnterReturningToPlayerState();
            return;
        }

        if (!hasDestination)
        {
            TryPickNewWanderDestination();
            return;
        }

        if (agent.pathPending)
            return;

        if (HasReachedDestination())
            EnterWaitingState();
    }

    private void UpdateWaiting(float sqrDistanceToPlayer)
    {
        if (sqrDistanceToPlayer <= proximityRadiusSqr)
        {
            EnterWatchingPlayerState();
            return;
        }

        if (sqrDistanceToPlayer > wanderRadiusSqr)
        {
            EnterReturningToPlayerState();
        }
    }

    private void UpdateWatchingPlayer(float sqrDistanceToPlayer)
    {
        if (sqrDistanceToPlayer > wanderRadiusSqr)
        {
            EnterReturningToPlayerState();
            return;
        }

        if (sqrDistanceToPlayer > proximityRadiusSqr)
        {
            EnterWanderingState();
            return;
        }

        RotateTowardsPlayer();
    }

    private void UpdateReturningToPlayer(float sqrDistanceToPlayer)
    {
        if (sqrDistanceToPlayer <= proximityRadiusSqr)
        {
            EnterWatchingPlayerState();
            return;
        }

        if (sqrDistanceToPlayer <= wanderRadiusSqr)
        {
            EnterWanderingState();
            return;
        }

        UpdateReturnDestination();
    }

    private void EnterWanderingState()
    {
        StopWaiting();

        currentState = DogState.Wandering;
        agent.isStopped = false;
        agent.speed = wanderSpeed;

        TryPickNewWanderDestination();
    }

    private void EnterWaitingState()
    {
        StopWaiting();

        currentState = DogState.Waiting;
        hasDestination = false;

        agent.isStopped = true;
        agent.ResetPath();

        waitCoroutine = StartCoroutine(WaitAndResumeWandering());
    }

    private void EnterWatchingPlayerState()
    {
        StopWaiting();

        currentState = DogState.WatchingPlayer;
        hasDestination = false;

        agent.isStopped = true;
        agent.ResetPath();
    }

    private void EnterReturningToPlayerState()
    {
        StopWaiting();

        currentState = DogState.ReturningToPlayer;
        agent.isStopped = false;
        agent.speed = returnSpeed;

        UpdateReturnDestination();
    }

    private IEnumerator WaitAndResumeWandering()
    {
        float waitTime = Random.Range(minWaitTime, maxWaitTime);
        yield return new WaitForSeconds(waitTime);

        waitCoroutine = null;

        if (currentState == DogState.Waiting)
            EnterWanderingState();
    }

    private void StopWaiting()
    {
        if (waitCoroutine != null)
        {
            StopCoroutine(waitCoroutine);
            waitCoroutine = null;
        }
    }

    private void TryPickNewWanderDestination()
    {
        hasDestination = false;

        for (int i = 0; i < maxDestinationAttempts; i++)
        {
            Vector2 randomOffset2D = Random.insideUnitCircle * wanderRadius;

            Vector3 candidatePoint = new Vector3(
                player.position.x + randomOffset2D.x,
                player.position.y,
                player.position.z + randomOffset2D.y
            );

            float sqrDistanceFromPlayer = GetFlatSqrDistance(candidatePoint, player.position);

            if (sqrDistanceFromPlayer < proximityRadiusSqr || sqrDistanceFromPlayer > wanderRadiusSqr)
                continue;

            if (!NavMesh.SamplePosition(candidatePoint, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                continue;

            float sampledSqrDistance = GetFlatSqrDistance(hit.position, player.position);

            if (sampledSqrDistance < proximityRadiusSqr || sampledSqrDistance > wanderRadiusSqr)
                continue;

            currentDestination = hit.position;
            hasDestination = agent.SetDestination(currentDestination);

            if (hasDestination)
                return;
        }
    }

    private void UpdateReturnDestination()
    {
        if (!NavMesh.SamplePosition(player.position, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            hasDestination = false;
            return;
        }

        currentDestination = hit.position;
        hasDestination = agent.SetDestination(currentDestination);
    }

    private bool HasReachedDestination()
    {
        if (agent.pathPending)
            return false;

        if (agent.remainingDistance > pointReachDistance)
            return false;

        if (agent.hasPath && agent.velocity.sqrMagnitude > 0.01f)
            return false;

        return true;
    }

    private void RotateTowardsPlayer()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            lookRotationSpeed * Time.deltaTime
        );
    }

    private float GetFlatSqrDistance(Vector3 a, Vector3 b)
    {
        float x = a.x - b.x;
        float z = a.z - b.z;
        return (x * x) + (z * z);
    }

    private void CacheDerivedValues()
    {
        wanderRadius = Mathf.Max(0.1f, wanderRadius);
        proximityRadius = Mathf.Clamp(proximityRadius, 0f, wanderRadius);
        maxDestinationAttempts = Mathf.Max(1, maxDestinationAttempts);
        navMeshSampleDistance = Mathf.Max(0.1f, navMeshSampleDistance);
        pointReachDistance = Mathf.Max(0.05f, pointReachDistance);
        minWaitTime = Mathf.Max(0f, minWaitTime);
        maxWaitTime = Mathf.Max(minWaitTime, maxWaitTime);
        wanderSpeed = Mathf.Max(0f, wanderSpeed);
        returnSpeed = Mathf.Max(wanderSpeed, returnSpeed);
        lookRotationSpeed = Mathf.Max(0f, lookRotationSpeed);

        proximityRadiusSqr = proximityRadius * proximityRadius;
        wanderRadiusSqr = wanderRadius * wanderRadius;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || player == null)
            return;

        Gizmos.color = wanderRadiusColor;
        Gizmos.DrawWireSphere(player.position, wanderRadius);

        Gizmos.color = proximityRadiusColor;
        Gizmos.DrawWireSphere(player.position, proximityRadius);

        if (Application.isPlaying && hasDestination)
        {
            Gizmos.color = destinationColor;
            Gizmos.DrawSphere(currentDestination, 0.2f);
            Gizmos.DrawLine(transform.position, currentDestination);
        }
    }
}