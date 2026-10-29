using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjects.Dialogue
{
    [CreateAssetMenu(fileName = "Dialogue", menuName = "War/Dialogue")]
    public class DialogueSequence : ScriptableObject
    {
        public string sequenceName;
        public List<DialogueLine> lines;
    }

    [System.Serializable]
    public class DialogueLine
    {
        public string speaker;
        [TextArea(2, 5)]
        public string text;
        public Sprite portrait;
    }
}