using UnityEngine;
using Fusion;

[RequireComponent(typeof(Collider2D))]
public class KillZone : MonoBehaviour
{
    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponentInParent<PlayerController>();
        if (pc == null)
            return;

        // gameplay robi TYLKO state authority
        if (!pc.Object.HasStateAuthority)
            return;

        pc.OnKilled();
    }
}
