using UnityEngine;
using System;
using System.Collections;
using System.IO;
using Firebase;
using Firebase.Storage;
using Firebase.Extensions;

public class DynamicSkinLoader : MonoBehaviour
{
    // Firebase connection
    private FirebaseStorage storage;

    // Local storage path
    private string skinDownloadPath;

    // Firebase bucket URL
    private const string FIREBASE_STORAGE_BUCKET = "gs://chess-f8d15.firebasestorage.app";

    private void Awake()
    {
        // Set download folder
        skinDownloadPath = Path.Combine(Application.persistentDataPath, "DownloadedSkins");
        
        // Create folder if needed
        if (!Directory.Exists(skinDownloadPath))
        {
            Directory.CreateDirectory(skinDownloadPath);
        }

        // Initialize Firebase
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            if (task.Result == DependencyStatus.Available)
            {
                // Connect to storage
                storage = FirebaseStorage.GetInstance(FIREBASE_STORAGE_BUCKET);
                Debug.Log("Firebase Storage initialized successfully");
            }
            else
            {
                Debug.LogError("Could not resolve Firebase dependencies: " + task.Result);
            }
        });
    }

    // Download a skin for a piece
    public void DownloadSkinMaterial(string skinName, string pieceType)
    {
        if (storage == null)
        {
            Debug.LogError("Firebase Storage not initialized");
            return;
        }

        // Firebase path
        string storagePath = $"{skinName} {pieceType}.mat";
        StorageReference skinRef = storage.GetReference(storagePath);

        // Local path
        string localPath = Path.Combine(skinDownloadPath, $"{skinName}_{pieceType}.mat");

        // Start download
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

    // Load a downloaded skin material
    public Material LoadDownloadedSkinMaterial(string skinName, string pieceType)
    {
        string localPath = Path.Combine(skinDownloadPath, $"{skinName}_{pieceType}.mat");

        if (File.Exists(localPath))
        {
            // Create material
            Material loadedMaterial = new Material(Shader.Find("Standard")); // Default shader
            
            // Add texture
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

    // Load image from file
    private Texture2D LoadTextureFromFile(string filePath)
    {
        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(fileData);
        return texture;
    }

    // Check if skin exists locally
    public bool IsSkinMaterialDownloaded(string skinName, string pieceType)
    {
        string localPath = Path.Combine(skinDownloadPath, $"{skinName}_{pieceType}.mat");
        return File.Exists(localPath);
    }

    // Download all materials for a skin
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

    // Apply skins to all pieces
    public void ApplyDownloadedSkin(string skinName)
    {
        // Find all pieces
        VisualPiece[] pieces = FindObjectsOfType<VisualPiece>();
        
        foreach (VisualPiece piece in pieces)
        {
            Renderer renderer = piece.GetComponent<Renderer>();
            if (renderer == null) continue;

            // Get piece type
            string pieceType = GetPieceType(piece);
            
            // Load material
            Material downloadedMaterial = LoadDownloadedSkinMaterial(skinName, pieceType);
            
            if (downloadedMaterial != null)
            {
                renderer.material = downloadedMaterial;
                Debug.Log($"Applied {skinName} material to {pieceType}");
            }
        }
    }

    // Identify piece type
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