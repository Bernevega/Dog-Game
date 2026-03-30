using UnityEngine;

public class Alien : MonoBehaviour
{
    [Header("Alien Settings")]
    public AlienManager alienManager;

    public void Die()
    {
        if (alienManager != null)
        {
            alienManager.RemoveAlien(gameObject);
        }

        Destroy(gameObject);
    }
}