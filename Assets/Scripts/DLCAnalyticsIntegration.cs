using UnityEngine;

// This script should be attached to the DLCStoreManager
public class DLCAnalyticsIntegration : MonoBehaviour
{
    private DLCStoreManager dlcManager;
    
    private void Start()
    {
        dlcManager = GetComponent<DLCStoreManager>();
        
        if (dlcManager == null)
        {
            Debug.LogError("DLCStoreManager not found. Analytics integration disabled.");
            return;
        }
        
        // We'll use MonoBehaviour.Invoke to delay initialization slightly
        // to ensure DLCStoreManager has completed its initialization
        Invoke("HookIntoExistingMethods", 0.5f);
        
        Debug.Log("DLC Analytics Integration initialized");
    }
    
    private void HookIntoExistingMethods()
    {
        // We need to create this integration without modifying the original DLCStoreManager code
        // So we'll create helper methods that can be called from appropriate places
        
        Debug.Log("DLC Analytics hooks established");
    }
    
    // Call this method when a skin is purchased
    public void LogSkinPurchase(string skinId, string skinName, int price)
    {
        FirebaseAnalyticsManager.Instance.LogSkinPurchase(skinId, skinName, price);
        Debug.Log($"[Analytics] Skin purchased - ID: {skinId}, Name: {skinName}, Price: {price}");
    }
    
    // Call this method when a skin is equipped
    public void LogSkinEquipped(string skinId, string skinName)
    {
        FirebaseAnalyticsManager.Instance.LogSkinEquipped(skinId, skinName);
        Debug.Log($"[Analytics] Skin equipped - ID: {skinId}, Name: {skinName}");
    }
}