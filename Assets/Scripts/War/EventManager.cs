using System.Collections.Generic;
using ScriptableObjects.Events;
using TMPro;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance;

    [SerializeField] private List<GlobalEvents> events;
    private readonly HashSet<GlobalEvents> _hasTriggered = new();
    
    [Header("UI References)]")]
    [SerializeField] private GameObject notificationContainer;
    [SerializeField] private TextMeshProUGUI notificationText;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        } else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    public void TriggerEvent(GlobalEvents globalEvent)
    {
        if (!globalEvent) return;
        if (globalEvent.triggerOnce && _hasTriggered.Contains(globalEvent)) return;
        
        events.Add(globalEvent);

        if (globalEvent.dialogue)
        {
            // DialogueManager.Instance.PlayDialogue(globalEvent.dialogue);
        }
    }

    public void ShowNotification(string text)
    {
        notificationText.text = text;
        notificationContainer.SetActive(true);
    }

    public void HideNotification()
    {
        notificationContainer.SetActive(false);
        notificationText.text = string.Empty;        
    }
}
