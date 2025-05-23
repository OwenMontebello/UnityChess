using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    // Handle player spawn event
    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            Debug.Log("Local player spawned!");
        }
        else
        {
            Debug.Log($"Remote player spawned with ID {OwnerClientId}");
        }
    }
}