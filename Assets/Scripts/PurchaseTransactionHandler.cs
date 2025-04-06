using System.Collections.Generic;
using UnityEngine;

public class PurchaseTransactionHandler
{
    // The player's current credits.
    private int playerCredits;
    // List of skin IDs that the player owns.
    private List<string> ownedSkins;

    // Constructor loads persistent data.
    public PurchaseTransactionHandler()
    {
        LoadPlayerData();
        Debug.Log("PurchaseTransactionHandler created. Credits: " + playerCredits);
    }

    /// <summary>
    /// Checks if the player can afford a given cost.
    /// </summary>
    public bool CanAfford(int cost)
    {
        return playerCredits >= cost;
    }

    /// <summary>
    /// Purchases a skin if not already owned.
    /// Returns true if successful.
    /// </summary>
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

    /// <summary>
    /// Deducts credits (used for equipping).
    /// Returns true if successful.
    /// </summary>
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

    /// <summary>
    /// Adds currency to the player's balance.
    /// </summary>
    public void AddCurrency(int amount)
    {
        playerCredits += amount;
        SavePlayerData();
        Debug.Log("Added " + amount + " currency, new total: " + playerCredits);
    }

    public int GetPlayerCredits() => playerCredits;
    public List<string> GetOwnedSkins() => ownedSkins;

    private void LoadPlayerData()
    {
        // Use "PlayerCredits" as the key.
        playerCredits = PlayerPrefs.HasKey("PlayerCredits") ? PlayerPrefs.GetInt("PlayerCredits") : 200;
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

    private void SavePlayerData()
    {
        PlayerPrefs.SetInt("PlayerCredits", playerCredits);
        string owned = string.Join(",", ownedSkins.ToArray());
        PlayerPrefs.SetString("OwnedSkins", owned);
        PlayerPrefs.Save();
        Debug.Log("Player data saved. Credits now: " + playerCredits);
    }
}