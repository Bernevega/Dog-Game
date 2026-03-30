using System.Collections.Generic;
using UnityEngine;

public class AlienManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private GameObject alienPrefab;

    [Header("Alien Limits")]
    [SerializeField, Min(1)] private int maxNearbyAliens = 3;
    [SerializeField, Min(1)] private int maxTotalAliensAlive = 3;
    [SerializeField, Min(0.1f)] private float playerNearbyRadius = 20f;

    [Header("Alien Movement")]
    [SerializeField, Min(0f)] private float alienMoveSpeed = 2f;
    [SerializeField, Min(0f)] private float alienStoppingDistance = 1.5f;
    [SerializeField] private bool rotateAliensTowardsPlayer = true;

    [Header("Spawn Settings")]
    [SerializeField, Min(0f)] private float minSpawnDistanceFromPlayer = 6f;
    [SerializeField, Min(0f)] private float maxSpawnDistanceFromPlayer = 15f;
    [SerializeField] private float spawnHeightOffset = 0f;
    [SerializeField, Min(0.01f)] private float spawnCheckInterval = 0.5f;
    [SerializeField, Min(1)] private int maxSpawnAttemptsPerAlien = 20;
    [SerializeField, Min(0f)] private float minDistanceBetweenAliens = 3f;

    [Header("Despawn Settings")]
    [SerializeField, Min(0.1f)] private float alienRange = 25f;
    [SerializeField, Min(0f)] private float despawnDelay = 8f;

    [Header("Gizmo Settings")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color playerNearbyRadiusColor = Color.cyan;
    [SerializeField] private Color playerSpawnMinColor = Color.yellow;
    [SerializeField] private Color playerSpawnMaxColor = Color.green;
    [SerializeField] private Color alienRangeColor = Color.red;
    [SerializeField] private Color alienStoppingDistanceColor = Color.magenta;

    private readonly List<GameObject> activeAliens = new List<GameObject>();
    private readonly Dictionary<GameObject, float> despawnTimers = new Dictionary<GameObject, float>();

    private float spawnTimer;

    private void Update()
    {
        if (player == null || alienPrefab == null)
            return;

        CleanupDestroyedAliens();
        MoveAliensTowardsPlayer();
        UpdateDespawnTimers();

        spawnTimer -= Time.deltaTime;

        if (spawnTimer <= 0f)
        {
            spawnTimer = spawnCheckInterval;
            FillNearbyAliens();
        }
    }

    private void CleanupDestroyedAliens()
    {
        for (int i = activeAliens.Count - 1; i >= 0; i--)
        {
            if (activeAliens[i] == null)
                activeAliens.RemoveAt(i);
        }

        List<GameObject> keysToRemove = null;

        foreach (var pair in despawnTimers)
        {
            if (pair.Key == null)
            {
                if (keysToRemove == null)
                    keysToRemove = new List<GameObject>();

                keysToRemove.Add(pair.Key);
            }
        }

        if (keysToRemove != null)
        {
            foreach (GameObject key in keysToRemove)
                despawnTimers.Remove(key);
        }
    }

    private void MoveAliensTowardsPlayer()
    {
        for (int i = 0; i < activeAliens.Count; i++)
        {
            GameObject alien = activeAliens[i];

            if (alien == null)
                continue;

            Vector3 alienPosition = alien.transform.position;
            Vector3 playerPosition = player.position;

            float distanceToPlayer = Vector3.Distance(alienPosition, playerPosition);

            if (distanceToPlayer <= playerNearbyRadius && distanceToPlayer > alienStoppingDistance)
            {
                Vector3 direction = (playerPosition - alienPosition).normalized;
                Vector3 moveDirection = new Vector3(direction.x, 0f, direction.z);

                alien.transform.position += moveDirection * alienMoveSpeed * Time.deltaTime;

                if (rotateAliensTowardsPlayer && moveDirection != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                    alien.transform.rotation = targetRotation;
                }
            }
        }
    }

    private void UpdateDespawnTimers()
    {
        for (int i = activeAliens.Count - 1; i >= 0; i--)
        {
            GameObject alien = activeAliens[i];

            if (alien == null)
                continue;

            float distanceToPlayer = Vector3.Distance(player.position, alien.transform.position);

            if (distanceToPlayer > alienRange)
            {
                if (!despawnTimers.ContainsKey(alien))
                    despawnTimers.Add(alien, despawnDelay);

                despawnTimers[alien] -= Time.deltaTime;

                if (despawnTimers[alien] <= 0f)
                {
                    despawnTimers.Remove(alien);
                    activeAliens.RemoveAt(i);
                    Destroy(alien);
                }
            }
            else
            {
                if (despawnTimers.ContainsKey(alien))
                    despawnTimers.Remove(alien);
            }
        }
    }

    private void FillNearbyAliens()
    {
        int nearbyCount = CountNearbyAliens();
        int totalAliveCount = CountAliveAliens();

        int nearbySlotsLeft = maxNearbyAliens - nearbyCount;
        int totalSlotsLeft = maxTotalAliensAlive - totalAliveCount;

        int aliensNeeded = Mathf.Min(nearbySlotsLeft, totalSlotsLeft);

        for (int i = 0; i < aliensNeeded; i++)
        {
            if (TryGetSpawnPosition(out Vector3 spawnPosition))
            {
                SpawnAlien(spawnPosition);
            }
            else
            {
                break;
            }
        }
    }

    private int CountNearbyAliens()
    {
        int count = 0;

        for (int i = 0; i < activeAliens.Count; i++)
        {
            GameObject alien = activeAliens[i];

            if (alien == null)
                continue;

            float distanceToPlayer = Vector3.Distance(player.position, alien.transform.position);

            if (distanceToPlayer <= playerNearbyRadius)
                count++;
        }

        return count;
    }

    private int CountAliveAliens()
    {
        int count = 0;

        for (int i = 0; i < activeAliens.Count; i++)
        {
            if (activeAliens[i] != null)
                count++;
        }

        return count;
    }

    private bool TryGetSpawnPosition(out Vector3 spawnPosition)
    {
        for (int attempt = 0; attempt < maxSpawnAttemptsPerAlien; attempt++)
        {
            float randomAngle = Random.Range(0f, 360f);
            float randomDistance = Random.Range(minSpawnDistanceFromPlayer, maxSpawnDistanceFromPlayer);

            float x = Mathf.Cos(randomAngle * Mathf.Deg2Rad) * randomDistance;
            float z = Mathf.Sin(randomAngle * Mathf.Deg2Rad) * randomDistance;

            Vector3 candidatePosition = player.position + new Vector3(x, spawnHeightOffset, z);

            if (IsPositionValid(candidatePosition))
            {
                spawnPosition = candidatePosition;
                return true;
            }
        }

        spawnPosition = Vector3.zero;
        return false;
    }

    private bool IsPositionValid(Vector3 candidatePosition)
    {
        for (int i = 0; i < activeAliens.Count; i++)
        {
            GameObject alien = activeAliens[i];

            if (alien == null)
                continue;

            float distance = Vector3.Distance(candidatePosition, alien.transform.position);

            if (distance < minDistanceBetweenAliens)
                return false;
        }

        return true;
    }

    private void SpawnAlien(Vector3 position)
    {
        if (CountAliveAliens() >= maxTotalAliensAlive)
            return;

        GameObject newAlien = Instantiate(alienPrefab, position, Quaternion.identity);
        activeAliens.Add(newAlien);
    }

    private void OnValidate()
    {
        if (maxSpawnDistanceFromPlayer < minSpawnDistanceFromPlayer)
            maxSpawnDistanceFromPlayer = minSpawnDistanceFromPlayer;

        if (alienRange < playerNearbyRadius)
            alienRange = playerNearbyRadius;

        if (alienStoppingDistance < 0f)
            alienStoppingDistance = 0f;

        if (maxTotalAliensAlive < 1)
            maxTotalAliensAlive = 1;

        if (maxNearbyAliens > maxTotalAliensAlive)
            maxNearbyAliens = maxTotalAliensAlive;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || player == null)
            return;

        Gizmos.color = playerNearbyRadiusColor;
        Gizmos.DrawWireSphere(player.position, playerNearbyRadius);

        Gizmos.color = playerSpawnMinColor;
        Gizmos.DrawWireSphere(player.position, minSpawnDistanceFromPlayer);

        Gizmos.color = playerSpawnMaxColor;
        Gizmos.DrawWireSphere(player.position, maxSpawnDistanceFromPlayer);

        for (int i = 0; i < activeAliens.Count; i++)
        {
            if (activeAliens[i] == null)
                continue;

            Gizmos.color = alienRangeColor;
            Gizmos.DrawWireSphere(activeAliens[i].transform.position, alienRange);

            Gizmos.color = alienStoppingDistanceColor;
            Gizmos.DrawWireSphere(activeAliens[i].transform.position, alienStoppingDistance);
        }
    }
}