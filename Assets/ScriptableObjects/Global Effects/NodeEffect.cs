using UnityEngine;

namespace ScriptableObjects.Global_Effects
{
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
    
    public enum EffectScope
    {
        Global,
        Local
    }

    [CreateAssetMenu(fileName = "NodeEffect", menuName = "War/Effects/Node Effect")]
    public class NodeEffect : ScriptableObject
    {
        public string effectName;
        [TextArea] public string description;

        public EffectScope scope;
        public EffectTarget target;
        public EffectStat stat;

        [Range(0f, 2f)]
        public float multiplier = 1f;

        public bool AppliesTo(Unit unit, Node sourceNode)
        {
            if (!unit)
                return false;

            if (scope == EffectScope.Local && unit.currentNode != sourceNode)
            {
                return false;
            }

            return target switch
            {
                EffectTarget.Everyone => true,
                EffectTarget.Allies => unit.isPlayerUnit,
                EffectTarget.Enemies => !unit.isPlayerUnit,

                EffectTarget.DeathBeforeDishonor =>
                    unit.isPlayerUnit &&
                    unit.currentNode &&
                    unit.currentNode.isBukitChandu &&
                    unit.Regiment == RegimentType.MalayRegiment,

                _ => false
            };
        }
    }
}