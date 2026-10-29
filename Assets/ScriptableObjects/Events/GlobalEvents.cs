using ScriptableObjects.Dialogue;
using UnityEngine;

namespace ScriptableObjects.Events
{
    [CreateAssetMenu(fileName = "Events", menuName = "War/GlobalEvent")]
    public class GlobalEvents : ScriptableObject
    {
        public string eventName;
        public DialogueSequence dialogue;
        public Node targetNode;

        public bool triggerOnce = true;
        [HideInInspector] public bool hasTriggered;
    }
}
