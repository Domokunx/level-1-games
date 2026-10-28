using System.Collections;
using System.Collections.Generic;
using ScriptableObjects;
using UnityEngine;

public class Unit : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private UnitDataSO data;

    [Header("Runtime State (Read-Only in Inspector)")]
    public RectTransform portraitUIRect;
    public bool isPlayerUnit;
    public int currentHealth;
    public Node currentNode;
    public bool hasActedThisTurn;

    // Expose stats as public properties so other scripts can read them
    // Lambda functions so that they can only be read, not written
    public string UnitName => data  ? data.unitName : "Unknown";
    public int MaxHealth => data  ? data.maxHealth : 100;
    public int MoveRange => data  ? data.moveRange : 3;
    public int AttackPower => data  ? data.attackPower : 10;
    public int Speed => data  ? data.speed : 5;
    public Sprite Portrait => data  ? data.portrait : null;
    public bool IsSupplied =>
        currentNode &&
        NodeManager.Instance.IsNodeSupplied(
            currentNode,
            isPlayerUnit ? SupplyFaction.Ally : SupplyFaction.Enemy
        );
    
    public int GetEffectiveAttack => IsSupplied ? Mathf.RoundToInt(AttackPower * 1.5f) : AttackPower;

    [Header("Animation Parameters")]
    [SerializeField] private float moveSpeed = 1.5f;

    private void Awake()
    {
        InitializeUnit();
    }

    private void InitializeUnit()
    {
        if (data )
        {
            currentHealth = data.maxHealth;
        }
        else
        {
            Debug.LogWarning($"Unit {gameObject.name} is missing a UnitDataSO reference!", this);
            currentHealth = 100; // Fallback default
        }
    }
    
    public IEnumerator MoveAlongPath(List<Node> path, float stepDelay = 0.15f)
    {
        for (int i = 0; i < path.Count; i++)
        {
            Node nextNode = path[i];
            if (nextNode == currentNode) continue;

            if (currentNode)
            {
                currentNode.RemoveUnit(this);
            }

            nextNode.AddUnit(this);

            yield return new WaitForSeconds(stepDelay);
        }
    }
    
    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Max(0, currentHealth);
    }
    
    private Node GetHqNode()
    {
        return isPlayerUnit ? BattleManager.Instance.playerHeadquartersNode : BattleManager.Instance.enemyHeadquartersNode;
    }
    public void ResetTurnState()
    {
        hasActedThisTurn = false;
        // TODO: Reset unit visual tint/gray out if implemented
    }
}