using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;

public class NetworkPingManager : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI pingText;
    [SerializeField] private float pingInterval = 1.0f;
    
    private ulong clientId;
    private float lastPingTime;
    private float currentPing;
    
    private NetworkVariable<long> serverTimestamp = new NetworkVariable<long>();

    public override void OnNetworkSpawn()
    {
        Debug.Log("NetworkPingManager: OnNetworkSpawn called");
        
        if (IsClient)
        {
            clientId = NetworkManager.Singleton.LocalClientId;
            Debug.Log($"Client {clientId} started ping measurement");
            StartCoroutine(PingCoroutine());
        }
    }
    
    private IEnumerator PingCoroutine()
    {
        Debug.Log("Starting ping coroutine");
        
        // Wait a moment to ensure everything is initialized
        yield return new WaitForSeconds(0.5f);
        
        while (IsClient && NetworkManager.Singleton.IsConnectedClient)
        {
            // Record the time and send ping request
            lastPingTime = Time.realtimeSinceStartup;
            PingServerRpc();
            
            yield return new WaitForSeconds(pingInterval);
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void PingServerRpc(ServerRpcParams serverRpcParams = default)
    {
        // Get client ID who sent the request
        ulong senderId = serverRpcParams.Receive.SenderClientId;
        
        // Send response back only to that client
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { senderId }
            }
        };
        
        // Send ping response
        PingResponseClientRpc(clientRpcParams);
    }
    
    [ClientRpc]
    private void PingResponseClientRpc(ClientRpcParams clientRpcParams = default)
    {
        // Calculate round-trip time
        float pingTime = (Time.realtimeSinceStartup - lastPingTime) * 1000f; // Convert to ms
        currentPing = pingTime;
        
        
        // Update UI
        if (pingText != null)
        {
            pingText.text = $"Ping: {currentPing:F1}ms";
        }
    }
    
    // Call this manually from UI buttons to test ping if needed
    public void TriggerManualPing()
    {
        if (IsClient && NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.Log("Manual ping triggered");
            lastPingTime = Time.realtimeSinceStartup;
            PingServerRpc();
        }
    }
}