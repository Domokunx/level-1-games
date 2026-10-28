using UnityEngine;

namespace ScriptableObjects.Global_Effects
{
    public enum GlobalEffectType
    {
        None,
        MacRitchieLost,
        BukitTimahLost,
    
    }
    
    public enum EffectTarget
    {
        Everyone,
        Allies,
        Enemies,
        DeathBeforeDishonor
    }

    public enum EffectStat
    {
        Attack,
        Health,
        MoveRange,
        Speed
    }

    [CreateAssetMenu(fileName = "NodeEffect", menuName = "War/Effects/Node Effect")]
    public class NodeEffect : ScriptableObject
    {
        public string effectName;
        [TextArea] public string description;

        public EffectTarget target;
        public EffectStat stat;

        [Range(0f, 2f)]
        public float multiplier = 1f;

        public bool AppliesTo(Unit unit)
        {
            return target switch
            {
                EffectTarget.Everyone => true,
                EffectTarget.Allies => unit.isPlayerUnit,
                EffectTarget.Enemies => !unit.isPlayerUnit,
                EffectTarget.DeathBeforeDishonor => 
                    unit.isPlayerUnit && unit.currentNode.name == "Bukit Chandu" &&
                    unit.Regiment == RegimentType.MalayRegiment,
                _ => false
            };
        }
    }
}