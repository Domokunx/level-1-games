using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum SupplyFaction { Neutral, Ally, Enemy }
public enum NodeHighlightState
{
    None,
    ReachableMove, 
    ReachableAttack, 
    Selected, 
    Preview 
}

public class Node : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public List<Node> neighbours;

    [Header("Occupancy")]
    public List<Unit> units = new();
    public bool isOccupied => units.Count > 0;
    public bool isOccupiedByAlly => isOccupied && units[0].isPlayerUnit;
    public bool ContainsPlayerUnits => units.Exists(unit => unit && unit.isPlayerUnit);
    public bool ContainsEnemyUnits => units.Exists(unit => unit && !unit.isPlayerUnit);
    
    public static event Action<Node> OnNodeClicked;
    public Image unitImage;
    public SupplyFaction supplyFaction = SupplyFaction.Neutral;
    public Unit supplyOwner;
    
    [Header("A* Pathfinding Data")]
    [System.NonSerialized] public float gCost; 
    [System.NonSerialized] public float hCost; 
    [System.NonSerialized] public Node parent;
    public float FinalCost => gCost + hCost;

    [Header("Territory & Occupancy Colors")] 
    [SerializeField] private Color occupiedAllyColor = Color.blue;  
    [SerializeField] private Color unoccupiedAllyColor = new Color(0.4f, 0.7f, 1f, 1f);
    [SerializeField] private Color occupiedEnemyColor = Color.red; 
    [SerializeField] private Color unoccupiedEnemyColor = new Color(1f, 0.4f, 0.4f, 1f);
    [SerializeField] private Color neutralColor = Color.white;
    
    [Header("Highlight Visuals")] 
    [SerializeField] private SpriteRenderer nodeColor;
    [SerializeField] private SpriteRenderer highlightRenderer; 
    [SerializeField] private Color moveColor = new Color(0f, 0.8f, 1f, 0.5f); 
    [SerializeField] private Color attackColor = new Color(1f, 0.2f, 0.2f, 0.5f); 
    [SerializeField] private Color selectedColor = new Color(1f, 1f, 1f, 0.8f); 
    [SerializeField] private Color previewColor = new Color(0.2f, 1f, 0.4f, 0.9f);

    public NodeHighlightState CurrentHighlightState { get; private set; } = NodeHighlightState.None;
    
    [Header("Stack Stats")]
    public int StackHealth => units.Sum(unit => unit.currentHealth);
    public int StackAttack => units.Sum(unit => unit.AttackPower);
    public int StackEffectiveAttack => units.Sum(unit => unit.GetEffectiveAttack);
    public int StackMoveRange => units.Count == 0 ? 0 : units.Min(unit => unit.MoveRange);
    public int StackSpeed => units.Count == 0 ? 0 : units.Min(unit => unit.Speed);
    
    private void Awake()
    {
        if (units != null)
        {
            foreach (var unit in units)
            {
                unit.currentNode = this;
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnNodeClicked?.Invoke(this);
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        BattleManager.Instance.HandleNodeHover(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        BattleManager.Instance.HandleNodeHoverExit(this);
    }

    public void SetSupplyFaction(SupplyFaction faction, Unit owner = null)
    {
        supplyFaction = faction;
        supplyOwner = owner;

        if (isOccupied || !nodeColor) return;
        
        nodeColor.color = faction switch
        {
            SupplyFaction.Ally => unoccupiedAllyColor,
            SupplyFaction.Enemy => unoccupiedEnemyColor,
            _ => neutralColor
        };
    }
    
    public void SetHighlightState(NodeHighlightState state)
    {
        CurrentHighlightState = state;

        if (!highlightRenderer) return;

        switch (state)
        {
            case NodeHighlightState.ReachableMove:
                highlightRenderer.enabled = true;
                highlightRenderer.color = moveColor;
                break;
            case NodeHighlightState.ReachableAttack:
                highlightRenderer.enabled = true;
                highlightRenderer.color = attackColor;
                break;
            case NodeHighlightState.Selected:
                highlightRenderer.enabled = true;
                highlightRenderer.color = selectedColor;
                break;
            case NodeHighlightState.Preview:
                highlightRenderer.enabled = true;
                highlightRenderer.color = previewColor;
                break;
            case NodeHighlightState.None:
            default:
                highlightRenderer.enabled = false;
                break;
        }
    }
    
    #region Stack Operations

    private void RemoveInvalidUnits()
    {
        units.RemoveAll(unit => !unit);
    }
    public void AddUnit(Unit unit)
    {
        // Sanity check cuz of a stoopid bug
        if (!unit) return;
        RemoveInvalidUnits();
        if (units.Contains(unit)) return;
        
        if (isOccupied && units[0].isPlayerUnit != unit.isPlayerUnit) return;

        if (unit.currentNode && unit.currentNode != this)
        {
            unit.currentNode.RemoveUnit(unit);
        }
        
        units.Add(unit);
        unit.currentNode = this;
        RefreshUnitVisual();
    }

    public void RemoveUnit(Unit unit)
    {
        if (!unit)
            return;

        units.Remove(unit);

        if (unit.currentNode == this)
            unit.currentNode = null;

        RefreshUnitVisual();
    }

    private void RefreshUnitVisual()
    {
        if (!unitImage)
            return;

        if (!isOccupied)
        {
            unitImage.enabled = false;
            unitImage.sprite = null;
            return;
        }

        unitImage.enabled = true;
        unitImage.sprite = units[0].Portrait;

        if (nodeColor)
        {
            nodeColor.color = units[0].isPlayerUnit
                ? occupiedAllyColor
                : occupiedEnemyColor;
        }
    }
    
    public void TakeStackDamage(int damage)
    {
        RemoveInvalidUnits();
        
        int remainingDamage = damage;

        // Weakest units are removed first
        List<Unit> damageOrder = units
            .OrderBy(unit => unit.currentHealth)
            .ToList();

        foreach (Unit unit in damageOrder)
        {
            if (remainingDamage <= 0)
                break;

            int damageToUnit = Mathf.Min(
                unit.currentHealth,
                remainingDamage
            );

            unit.TakeDamage(damageToUnit);
            remainingDamage -= damageToUnit;

            if (unit.currentHealth <= 0)
            {
                RemoveUnit(unit);
                Destroy(unit.gameObject);
            }
        }

        RefreshUnitVisual();
    }

    public bool HasUnits()
    {
        return units.Count > 0;
    }
    #endregion
    
    #region Debug Visual Effects
    public IEnumerator FlashDamageEffect()
    {
        if (nodeColor == null) yield break;

        Color originalColor = nodeColor.color;
        nodeColor.color = Color.white;
        yield return new WaitForSeconds(0.12f);
        nodeColor.color = originalColor;
    }

    public IEnumerator PulsePortrait()
    {
        if (unitImage == null || !unitImage.enabled) yield break;

        RectTransform rectTransform = unitImage.rectTransform;
        Vector3 originalScale = rectTransform.localScale;
        Vector3 targetScale = originalScale * 1.35f;

        float elapsed = 0f;
        float duration = 0.1f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            rectTransform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / duration);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            rectTransform.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / duration);
            yield return null;
        }

        rectTransform.localScale = originalScale;
    }
    #endregion
}