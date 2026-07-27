using UnityEngine;

public class DisableOnDedicatedServer : MonoBehaviour
{
#if UNITY_SERVER
    private void Awake()
    {
        gameObject.SetActive(false);
    }
#endif
}