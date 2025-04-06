using UnityEngine;
using System;
using System.Collections;
using System.IO;
using Firebase;
using Firebase.Storage;
using Firebase.Extensions;

public class DynamicSkinLoader : MonoBehaviour
{
    // Firebase Storage reference
    private FirebaseStorage storage;

    // Path to store downloaded skins
    private string skinDownloadPath;

    // Firebase Storage bucket URL
    private const string FIREBASE_STORAGE_BUCKET = "gs://chess-f8d15.firebasestorage.app";

    private void Awake()
    {
        // Initialize download path
        skinDownloadPath = Path.Combine(Application.persistentDataPath, "DownloadedSkins");
        
        // Create directory if it doesn't exist
        if (!Directory.Exists(skinDownloadPath))
        {
            Directory.CreateDirectory(skinDownloadPath);
        }

        // Initialize Firebase
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            if (task.Result == DependencyStatus.Available)
            {
                // Initialize Firebase Storage
                storage = FirebaseStorage.GetInstance(FIREBASE_STORAGE_BUCKET);
                Debug.Log("Firebase Storage initialized successfully");
            }
            else
            {
                Debug.LogError("Could not resolve Firebase dependencies: " + task.Result);
            }
        });
    }

    /// <summary>
    /// Download a specific skin material from Firebase Storage
    /// </summary>
    /// <param name="skinName">Name of the skin (Blue, Gold, Red)</param>
    /// <param name="pieceType">Type of piece (Pawn, Rook, Knight, etc.)</param>
    public void DownloadSkinMaterial(string skinName, string pieceType)
    {
        if (storage == null)
        {
            Debug.LogError("Firebase Storage not initialized");
            return;
        }

        // Construct the file path in Firebase Storage
        string storagePath = $"{skinName} {pieceType}.mat";
        StorageReference skinRef = storage.GetReference(storagePath);

        // Local path where the material will be saved
        string localPath = Path.Combine(skinDownloadPath, $"{skinName}_{pieceType}.mat");

        // Download the file
        skinRef.GetFileAsync(localPath).ContinueWithOnMainThread(task => 
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"Error downloading skin material: {task.Exception}");
                return false;
            }
            
            if (task.IsCompleted)
            {
                Debug.Log($"Successfully downloaded {storagePath} material");
                return true;
            }

            return false;
        });
    }

    /// <summary>
    /// Load a previously downloaded skin material
    /// </summary>
    /// <param name="skinName">Name of the skin (Blue, Gold, Red)</param>
    /// <param name="pieceType">Type of piece (Pawn, Rook, Knight, etc.)</param>
    /// <returns>Loaded Material or null if not found</returns>
    public Material LoadDownloadedSkinMaterial(string skinName, string pieceType)
    {
        string localPath = Path.Combine(skinDownloadPath, $"{skinName}_{pieceType}.mat");

        if (File.Exists(localPath))
        {
            // Load the material from the file
            Material loadedMaterial = new Material(Shader.Find("Standard")); // Default shader
            
            // Load texture if applicable
            Texture2D texture = LoadTextureFromFile(localPath);
            if (texture != null)
            {
                loadedMaterial.mainTexture = texture;
            }
            
            return loadedMaterial;
        }

        Debug.LogWarning($"Material {pieceType} for skin {skinName} not found.");
        return null;
    }

    /// <summary>
    /// Load texture from a file path
    /// </summary>
    private Texture2D LoadTextureFromFile(string filePath)
    {
        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(fileData);
        return texture;
    }

    /// <summary>
    /// Check if a specific skin material is downloaded
    /// </summary>
    public bool IsSkinMaterialDownloaded(string skinName, string pieceType)
    {
        string localPath = Path.Combine(skinDownloadPath, $"{skinName}_{pieceType}.mat");
        return File.Exists(localPath);
    }

    /// <summary>
    /// Download all materials for a specific skin
    /// </summary>
    /// <param name="skinName">Name of the skin (Blue, Gold, Red)</param>
    public void DownloadFullSkin(string skinName)
    {
        string[] pieceTypes = new string[] 
        { 
            "Pawn", "Rook", "Knight", "Bishop", "Queen", "King" 
        };

        foreach (string pieceType in pieceTypes)
        {
            DownloadSkinMaterial(skinName, pieceType);
        }
    }

    /// <summary>
    /// Apply downloaded skin materials to pieces
    /// </summary>
    public void ApplyDownloadedSkin(string skinName)
    {
        // Find all pieces on the board
        VisualPiece[] pieces = FindObjectsOfType<VisualPiece>();
        
        foreach (VisualPiece piece in pieces)
        {
            Renderer renderer = piece.GetComponent<Renderer>();
            if (renderer == null) continue;

            // Determine piece type and color
            string pieceType = GetPieceType(piece);
            
            // Load the downloaded material
            Material downloadedMaterial = LoadDownloadedSkinMaterial(skinName, pieceType);
            
            if (downloadedMaterial != null)
            {
                renderer.material = downloadedMaterial;
                Debug.Log($"Applied {skinName} material to {pieceType}");
            }
        }
    }

    private string GetPieceType(VisualPiece piece)
    {
        string pieceName = piece.name.ToLower();
        
        if (pieceName.Contains("pawn")) return "Pawn";
        if (pieceName.Contains("rook")) return "Rook";
        if (pieceName.Contains("knight")) return "Knight";
        if (pieceName.Contains("bishop")) return "Bishop";
        if (pieceName.Contains("queen")) return "Queen";
        if (pieceName.Contains("king")) return "King";
        
        return string.Empty;
    }
}