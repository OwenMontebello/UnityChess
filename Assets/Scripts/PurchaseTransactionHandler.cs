using System.Collections.Generic;
using UnityEngine;

public class PurchaseTransactionHandler
{
    // Player currency
    private int playerCredits;
    // Player owned items
    private List<string> ownedSkins;

    // Initialize and load data
    public PurchaseTransactionHandler()
    {
        LoadPlayerData();
        Debug.Log("PurchaseTransactionHandler created. Credits: " + playerCredits);
    }

    // Check if player can afford item
    public bool CanAfford(int cost)
    {
        return playerCredits >= cost;
    }

    // Try to buy a skin
    public bool PurchaseSkin(string skinId, int cost)
    {
        if (ownedSkins.Contains(skinId))
        {
            Debug.Log($"Purchase skipped: Skin '{skinId}' is already owned.");
            return false;
        }
        if (!CanAfford(cost))
        {
            Debug.Log($"Purchase failed: Not enough credits for '{skinId}'. Cost: {cost}, Available: {playerCredits}");
            return false;
        }
        Debug.Log("Before purchase, credits: " + playerCredits);
        playerCredits -= cost;
        ownedSkins.Add(skinId);
        SavePlayerData();
        Debug.Log("After purchase, credits: " + playerCredits);
        return true;
    }

    // Pay for equipping a skin
    public bool DeductCredits(int cost)
    {
        if (!CanAfford(cost))
        {
            Debug.Log($"Deduction failed: Not enough credits. Cost: {cost}, Available: {playerCredits}");
            return false;
        }
        Debug.Log("Deducting equip cost: " + cost + ", before: " + playerCredits);
        playerCredits -= cost;
        SavePlayerData();
        Debug.Log("After equip deduction, credits: " + playerCredits);
        return true;
    }

    // Add currency to player
    public void AddCurrency(int amount)
    {
        playerCredits += amount;
        SavePlayerData();
        Debug.Log("Added " + amount + " currency, new total: " + playerCredits);
    }

    // Get player's currency
    public int GetPlayerCredits() => playerCredits;
    
    // Get player's owned skins
    public List<string> GetOwnedSkins() => ownedSkins;

    // Load player data from storage
    private void LoadPlayerData()
    {
        // Load player currency, default 200
        playerCredits = PlayerPrefs.HasKey("PlayerCredits") ? PlayerPrefs.GetInt("PlayerCredits") : 200;
        
        // Load owned skins
        if (PlayerPrefs.HasKey("OwnedSkins"))
        {
            string savedSkins = PlayerPrefs.GetString("OwnedSkins");
            ownedSkins = !string.IsNullOrEmpty(savedSkins)
                ? new List<string>(savedSkins.Split(','))
                : new List<string>();
        }
        else
        {
            ownedSkins = new List<string>();
        }
    }

    // Save player data to storage
    private void SavePlayerData()
    {
        PlayerPrefs.SetInt("PlayerCredits", playerCredits);
        string owned = string.Join(",", ownedSkins.ToArray());
        PlayerPrefs.SetString("OwnedSkins", owned);
        PlayerPrefs.Save();
        Debug.Log("Player data saved. Credits now: " + playerCredits);
    }
}