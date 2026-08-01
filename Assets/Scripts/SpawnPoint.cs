using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [SerializeField] private Vector2 minRandomOffset;
    [SerializeField] private Vector2 maxRandomOffset;
    [SerializeField] private Color gizmoColor = Color.green;
    [SerializeField] private float gizmoRadius = 0.5f;

    public Vector3 GetSpawnPosition(bool useRandomOffset = true)
    {
        var offset = Vector3.up;

        if (useRandomOffset)
        {
            offset += new Vector3(
                Random.Range(minRandomOffset.x, maxRandomOffset.x),
                0f,
                Random.Range(minRandomOffset.y, maxRandomOffset.y));
        }

        return transform.position + offset;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position + Vector3.up, Mathf.Max(0.05f, gizmoRadius));
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up);
    }
}
