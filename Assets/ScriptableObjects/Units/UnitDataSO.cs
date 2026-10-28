using UnityEngine;

namespace ScriptableObjects
{
    public enum RegimentType
    {
        None,
        MalayRegiment,
        Australian,
        Indian,
        British
    }
    
    [CreateAssetMenu(fileName = "NewUnitData", menuName = "Wargame/UnitData")]
    public class UnitDataSO : ScriptableObject
    {
        [Header("General Info")]
        public string unitName;
        [TextArea] public string description;
        public Sprite portrait;
        public RegimentType regimentType;
        public bool isAdnanBinSaidi;
        
        [Header("Base Stats")]
        public int maxHealth = 100;
        public int moveRange = 3;
        public int attackPower = 20;
        public int attackRange = 1;
        public int speed = 1;
        
    }
}
