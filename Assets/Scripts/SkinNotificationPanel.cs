using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SkinNotificationPanel : MonoBehaviour
{
    // UI elements
    [SerializeField] private Text notificationText;
    [SerializeField] private Image backgroundPanel;
    
    // Animation timing
    [SerializeField] private float fadeInTime = 0.5f;
    [SerializeField] private float displayTime = 3.0f;
    [SerializeField] private float fadeOutTime = 0.5f;
    
    // Animation reference
    private Coroutine animationCoroutine;
    
    private void Awake()
    {
        // Hide by default
        gameObject.SetActive(false);
        
        // Log component status
        Debug.Log($"SkinNotificationPanel Awake - notificationText: {(notificationText != null ? "Set" : "NULL")}");
        Debug.Log($"SkinNotificationPanel Awake - backgroundPanel: {(backgroundPanel != null ? "Set" : "NULL")}");
    }
    
    // Show a notification message
    public void ShowNotification(string message)
    {
        Debug.Log($"SkinNotificationPanel.ShowNotification called with message: {message}");
        
        // Note: DLCStoreManager handles notifications now
        
        // Ensure panel is active
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning("Attempting to show notification on inactive panel. Activating it first.");
            gameObject.SetActive(true);
        }
        
        // Set message text
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
            
        // Cancel existing animation
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            Debug.Log("Stopped existing notification coroutine");
        }
            
        // Start animation
        animationCoroutine = StartCoroutine(AnimateNotification());
        Debug.Log("Started notification animation coroutine");
    }
    
    // Fade in/out animation
    private IEnumerator AnimateNotification()
    {
        Debug.Log("AnimateNotification coroutine started");
        
        // Force layout update
        Canvas.ForceUpdateCanvases();
        
        // Check components
        if (notificationText == null || backgroundPanel == null)
        {
            Debug.LogError("Missing UI components. Text: " + (notificationText != null) + ", Panel: " + (backgroundPanel != null));
            yield break;
        }
        
        Color textColor = notificationText.color;
        Color bgColor = backgroundPanel.color;
        
        // Start transparent
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
            bgColor.a = t * 0.8f; // Semi-transparent background
            
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
        
        // Hold for display time
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
        
        // Hide when done
        gameObject.SetActive(false);
        animationCoroutine = null;
        Debug.Log("Notification panel hidden");
    }
}