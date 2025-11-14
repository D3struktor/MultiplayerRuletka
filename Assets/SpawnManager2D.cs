using UnityEngine;

public class SpawnManager2D : MonoBehaviour
{
    public Transform[] points;

    public Vector3 GetSpawnPoint()
    {
        if (points == null || points.Length == 0)
            return Vector3.zero;

        int i = Random.Range(0, points.Length);
        return points[i].position;
    }
}
