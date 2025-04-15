using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;

public class NetworkPingManager : NetworkBehaviour
{
    // UI references
    [SerializeField] private TextMeshProUGUI pingText;
    [SerializeField] private float pingInterval = 1.0f;
    
    // Ping tracking
    private ulong clientId;
    private float lastPingTime;
    private float currentPing;
    
    // Server time sync
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
    
    // Regular ping measurement
    private IEnumerator PingCoroutine()
    {
        Debug.Log("Starting ping coroutine");
        
        // Short delay for initialization
        yield return new WaitForSeconds(0.5f);
        
        while (IsClient && NetworkManager.Singleton.IsConnectedClient)
        {
            // Send ping to server
            lastPingTime = Time.realtimeSinceStartup;
            PingServerRpc();
            
            yield return new WaitForSeconds(pingInterval);
        }
    }
    
    // Ask server for ping response
    [ServerRpc(RequireOwnership = false)]
    private void PingServerRpc(ServerRpcParams serverRpcParams = default)
    {
        // Get sender ID
        ulong senderId = serverRpcParams.Receive.SenderClientId;
        
        // Target only the sender
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { senderId }
            }
        };
        
        // Reply to ping
        PingResponseClientRpc(clientRpcParams);
    }
    
    // Server response to ping
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
    
    // Manual ping test
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