using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Firebase;
using Firebase.Storage;
using Firebase.Extensions;
using UnityChess;

public class FirebaseMaterialDownloader : MonoBehaviour
{
    // Firebase Storage reference.
    private FirebaseStorage storage;

    // Local folder where downloaded images are stored.
    private string downloadPath;

    // Delegates for download progress and completion.
    public delegate void DownloadProgressHandler(float progress);
    public delegate void DownloadCompleteHandler(bool success);

    // Dictionary to track which materials have been downloaded.
    private Dictionary<string, bool> downloadedMaterials = new Dictionary<string, bool>();

    private void Start()
    {
        // Set up the local download folder.
        downloadPath = Path.Combine(Application.persistentDataPath, "DownloadedMaterials");
        if (!Directory.Exists(downloadPath))
        {
            Directory.CreateDirectory(downloadPath);
        }

        // Initialize Firebase.
        InitializeFirebase();
    }

    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                storage = FirebaseStorage.DefaultInstance;
                Debug.Log("Firebase Storage initialized successfully");
            }
            else
            {
                Debug.LogError("Could not resolve Firebase dependencies: " + task.Result);
            }
        });
    }

    /// <summary>
    /// Downloads a skin image from Firebase Storage.
    /// </summary>
    public void DownloadMaterial(
        string skinName,
        string materialName,
        DownloadProgressHandler onProgress = null,
        DownloadCompleteHandler onComplete = null)
    {
        if (storage == null)
        {
            Debug.LogError("Firebase Storage not initialized. Retrying initialization.");
            InitializeFirebase();
            onComplete?.Invoke(false);
            return;
        }

        try
        {
            // Use PNG extension for the file stored in Firebase.
            string storagePath = $"{skinName} {materialName}.png";
            Debug.Log($"Attempting to access Firebase Storage path: {storagePath}");

            StorageReference storageRef = storage.GetReference(storagePath);
            if (storageRef == null)
            {
                Debug.LogError($"Failed to get reference for {storagePath}");
                onComplete?.Invoke(false);
                return;
            }

            // Local path where the image will be saved.
            string localPath = Path.Combine(downloadPath, $"{skinName}_{materialName}.png");
            Debug.Log($"Downloading from {storagePath} to {localPath}");

            storageRef.GetFileAsync(localPath).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    // Extract error details.
                    string errorMessage = "Unknown error";
                    if (task.Exception != null)
                    {
                        if (task.Exception.InnerExceptions != null && task.Exception.InnerExceptions.Count > 0)
                        {
                            errorMessage = string.Join("; ", task.Exception.InnerExceptions.Select(e => e.Message));
                        }
                        else
                        {
                            errorMessage = task.Exception.Message;
                        }
                    }
                    Debug.LogError($"Error downloading material {storagePath}: {errorMessage}");
                    onComplete?.Invoke(false);
                    return false;
                }
                if (task.IsCompleted)
                {
                    Debug.Log($"Successfully downloaded {storagePath} to {localPath}");
                    // Mark the file as downloaded.
                    string key = $"{skinName}_{materialName}";
                    downloadedMaterials[key] = true;
                    onComplete?.Invoke(true);
                    return true;
                }
                Debug.LogWarning($"Task neither faulted nor completed for {storagePath}");
                onComplete?.Invoke(false);
                return false;
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception in material download: {e.Message}");
            onComplete?.Invoke(false);
        }
    }

    /// <summary>
    /// Downloads all the images (for Pawn, Rook, Knight, Bishop, Queen, King) for a given skin.
    /// </summary>
    public void DownloadFullSkin(string skinName, DownloadCompleteHandler onAllDownloadsComplete = null)
    {
        string[] materialNames = new string[] { "Pawn", "Rook", "Knight", "Bishop", "Queen", "King" };

        int completedDownloads = 0;
        int failedDownloads = 0;

        Debug.Log($"Starting download for {skinName} skin. Total materials to download: {materialNames.Length}");

        foreach (string materialName in materialNames)
        {
            DownloadMaterial(
                skinName,
                materialName,
                onProgress: null,
                onComplete: (success) =>
                {
                    if (success)
                    {
                        completedDownloads++;
                        Debug.Log($"Successfully downloaded {skinName} {materialName} material");
                    }
                    else
                    {
                        failedDownloads++;
                        Debug.LogError($"Failed to download {skinName} {materialName} material");
                    }

                    if (completedDownloads + failedDownloads == materialNames.Length)
                    {
                        bool allDownloadsSuccessful = failedDownloads == 0;
                        Debug.Log($"Skin download complete. Success: {allDownloadsSuccessful}");
                        onAllDownloadsComplete?.Invoke(allDownloadsSuccessful);
                    }
                }
            );
        }
    }

    /// <summary>
    /// Checks whether the material image is downloaded.
    /// </summary>
    public bool IsMaterialDownloaded(string skinName, string materialName)
    {
        string key = $"{skinName}_{materialName}";

        // First, check the in-memory cache.
        if (downloadedMaterials.TryGetValue(key, out bool isDownloaded) && isDownloaded)
        {
            return true;
        }

        // Then, check the file system.
        string localPath = Path.Combine(downloadPath, $"{skinName}_{materialName}.png");
        isDownloaded = File.Exists(localPath);

        if (isDownloaded)
        {
            downloadedMaterials[key] = true;
        }

        Debug.Log($"Checking material download: {skinName} {materialName} - {isDownloaded}");
        return isDownloaded;
    }

    /// <summary>
    /// Loads the downloaded image from disk as a Texture2D and creates a Material.
    /// </summary>
    private Material LoadMaterialFromLocalFile(string skinName, string pieceType)
    {
        string localPath = Path.Combine(downloadPath, $"{skinName}_{pieceType}.png");
        if (!File.Exists(localPath))
        {
            Debug.LogWarning($"No local material file found at {localPath}");
            return null;
        }

        try
        {
            byte[] fileData = File.ReadAllBytes(localPath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(fileData))
            {
                Material material = new Material(Shader.Find("Standard"));
                material.mainTexture = texture;
                return material;
            }
            else
            {
                Debug.LogError($"Failed to load image from {localPath}");
                return null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception loading material from file {localPath}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Applies the skin to all VisualPiece objects of the specified side.
    /// </summary>
    public void ApplySkinToPieces(string skinName, Side pieceColor)
    {
        Debug.Log($"Applying {skinName} skin to {pieceColor} pieces");

        VisualPiece[] pieces = FindObjectsOfType<VisualPiece>();
        int appliedCount = 0;

        foreach (VisualPiece piece in pieces)
        {
            // Only apply the skin to pieces of the specified color.
            if (piece.PieceColor != pieceColor)
                continue;

            Renderer renderer = piece.GetComponent<Renderer>();
            if (renderer == null)
                continue;

            // Determine the type of piece (e.g., Pawn, Rook, Knight, etc.).
            string pieceType = GetPieceType(piece);
            if (string.IsNullOrEmpty(pieceType))
                continue;

            // If the material image is downloaded, try to load it.
            if (IsMaterialDownloaded(skinName, pieceType))
            {
                Material downloadedMat = LoadMaterialFromLocalFile(skinName, pieceType);
                if (downloadedMat != null)
                {
                    renderer.material = downloadedMat;
                }
                else
                {
                    // Fallback to a default color-based material.
                    renderer.material = CreateMaterialForSkin(skinName, pieceColor);
                }
                appliedCount++;
                Debug.Log($"Applied {skinName} material to {piece.name}");
            }
            else
            {
                Debug.LogWarning($"Material for {skinName} {pieceType} not downloaded yet");
            }
        }

        Debug.Log($"Applied {skinName} materials to {appliedCount} pieces");
    }

    /// <summary>
    /// Fallback method that creates a basic material if the downloaded file isn’t available.
    /// </summary>
    private Material CreateMaterialForSkin(string skinName, Side pieceColor)
    {
        Material material = new Material(Shader.Find("Standard"));

        if (skinName == "Gold")
        {
            material.color = new Color(1.0f, 0.84f, 0.0f);
            material.SetFloat("_Metallic", 0.8f);
            material.SetFloat("_Glossiness", 0.7f);
        }
        else if (skinName == "Silver")
        {
            material.color = new Color(0.75f, 0.75f, 0.75f);
            material.SetFloat("_Metallic", 0.8f);
            material.SetFloat("_Glossiness", 0.7f);
        }
        else if (skinName == "Red")
        {
            material.color = new Color(0.9f, 0.1f, 0.1f);
        }
        else if (skinName == "Blue")
        {
            material.color = new Color(0.1f, 0.1f, 0.9f);
        }
        else
        {
            // Default material based on piece color.
            material.color = pieceColor == Side.White ? Color.white : Color.black;
        }

        return material;
    }

    /// <summary>
    /// Determines the piece type based on the VisualPiece's name.
    /// </summary>
    private string GetPieceType(VisualPiece piece)
    {
        string pieceName = piece.name.ToLower();
        if (pieceName.Contains("pawn")) return "Pawn";
        if (pieceName.Contains("rook")) return "Rook";
        if (pieceName.Contains("knight")) return "Knight";
        if (pieceName.Contains("bishop")) return "Bishop";
        if (pieceName.Contains("queen")) return "Queen";
        if (pieceName.Contains("king")) return "King";

        Debug.LogWarning($"Could not determine piece type for: {piece.name}");
        return string.Empty;
    }
}