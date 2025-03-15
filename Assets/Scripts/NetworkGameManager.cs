using UnityEngine;
using Unity.Netcode;

public class NetworkGameManager : MonoBehaviour
{
    private static NetworkGameManager instance;

    private void Awake()
    {
        // Ensure only one instance of NetworkGameManager exists
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StartHost()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
        {
            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("Host started successfully.");
            }
            else
            {
                Debug.LogError("Failed to start host.");
            }
        }
    }

    public void StartClient()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
        {
            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("Joined as client.");
            }
            else
            {
                Debug.LogError("Failed to join as client.");
            }
        }
    }
}
