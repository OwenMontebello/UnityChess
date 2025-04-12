using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SkinNotificationPanel : MonoBehaviour
{
    [SerializeField] private Text notificationText;
    [SerializeField] private Image backgroundPanel;
    [SerializeField] private float fadeInTime = 0.5f;
    [SerializeField] private float displayTime = 3.0f;
    [SerializeField] private float fadeOutTime = 0.5f;
    
    private Coroutine animationCoroutine;
    
    private void Awake()
    {
        // Initially hide the panel
        gameObject.SetActive(false);
        
        // Log component references for debugging
        Debug.Log($"SkinNotificationPanel Awake - notificationText: {(notificationText != null ? "Set" : "NULL")}");
        Debug.Log($"SkinNotificationPanel Awake - backgroundPanel: {(backgroundPanel != null ? "Set" : "NULL")}");
    }
    
    public void ShowNotification(string message)
    {
        Debug.Log($"SkinNotificationPanel.ShowNotification called with message: {message}");
        
        // Note: This method won't be used by the DLCStoreManager anymore,
        // but we'll keep it in case you want to call it directly in other contexts
        
        // Ensure the GameObject is active before starting the coroutine
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning("Attempting to show notification on inactive panel. Activating it first.");
            gameObject.SetActive(true);
        }
        
        // Set the notification text
        if (notificationText != null)
        {
            notificationText.text = message;
            Debug.Log("Notification text set");
        }
        else
        {
            Debug.LogError("Notification text component is NULL!");
            return;
        }
            
        // Stop any existing animation
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            Debug.Log("Stopped existing notification coroutine");
        }
            
        // Start the animation - this now runs on an active GameObject
        animationCoroutine = StartCoroutine(AnimateNotification());
        Debug.Log("Started notification animation coroutine");
    }
    
    private IEnumerator AnimateNotification()
    {
        Debug.Log("AnimateNotification coroutine started");
        
        // Force layout update
        Canvas.ForceUpdateCanvases();
        
        // Check if components exist before proceeding
        if (notificationText == null || backgroundPanel == null)
        {
            Debug.LogError("Missing UI components. Text: " + (notificationText != null) + ", Panel: " + (backgroundPanel != null));
            yield break;
        }
        
        Color textColor = notificationText.color;
        Color bgColor = backgroundPanel.color;
        
        // Set initial alpha to 0
        textColor.a = 0f;
        bgColor.a = 0f;
        notificationText.color = textColor;
        backgroundPanel.color = bgColor;
        
        Debug.Log("Starting fade in animation");
        
        // Fade in
        float startTime = Time.time;
        while (Time.time < startTime + fadeInTime)
        {
            float t = (Time.time - startTime) / fadeInTime;
            textColor.a = t;
            bgColor.a = t * 0.8f; // Making background slightly transparent
            
            notificationText.color = textColor;
            backgroundPanel.color = bgColor;
            
            yield return null;
        }
        
        Debug.Log("Fade in complete");
        
        // Ensure fully visible
        textColor.a = 1f;
        bgColor.a = 0.8f;
        notificationText.color = textColor;
        backgroundPanel.color = bgColor;
        
        // Display for set time
        Debug.Log($"Displaying notification for {displayTime} seconds");
        yield return new WaitForSeconds(displayTime);
        
        Debug.Log("Display time complete, starting fade out");
        
        // Fade out
        startTime = Time.time;
        while (Time.time < startTime + fadeOutTime)
        {
            float t = 1f - ((Time.time - startTime) / fadeOutTime);
            textColor.a = t;
            bgColor.a = t * 0.8f;
            
            notificationText.color = textColor;
            backgroundPanel.color = bgColor;
            
            yield return null;
        }
        
        // Hide panel
        gameObject.SetActive(false);
        animationCoroutine = null;
        Debug.Log("Notification panel hidden");
    }
}