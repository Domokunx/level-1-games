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

public enum GlobalNodeEffect
{
    None,
    MacRitchieLost
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
    
    [Header("Global Effects")]
    [SerializeField] private GlobalNodeEffect globalEffect;
    public GlobalNodeEffect GlobalEffect => globalEffect;
    private bool _hasBeenLost;

    public bool IsControlledByEnemy
    {
        get
        {
            if (ContainsEnemyUnits)
                _hasBeenLost = true;

            return _hasBeenLost;
        }
    }
    
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
    
    [Header("Stack Portrait UI")]
    [SerializeField] private Transform portraitsContainer;
    [SerializeField] private Image portraitPrefab;
    [SerializeField] private TMPro.TextMeshProUGUI stackCountText;
    
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
    public int StackMaxHealth => units.Sum(unit => unit.MaxHealth);
    public int StackAttack => units.Sum(unit => unit.AttackPower);
    public int StackEffectiveAttack => units.Sum(unit => unit.GetEffectiveAttack);
    public int StackMoveRange => units.Count == 0 ? 0 : units.Min(unit => unit.MoveRange);
    public int StackSpeed => units.Count == 0 ? 0 : units.Min(unit => unit.Speed);
    
    private void Start()
    {
        InitializeNodeState();
    }

    private void InitializeNodeState()
    {
        if (units == null) units = new List<Unit>();
        
        RemoveInvalidUnits();

        foreach (Unit unit in units)
        {
            if (unit) unit.currentNode = this;
        }

        RefreshUnitVisual();
        RefreshNodeColor();
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

        RefreshNodeColor();
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

    public void RemoveInvalidUnits()
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
        RemoveInvalidUnits();

        if (portraitsContainer)
        {
            for (int i = portraitsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(portraitsContainer.GetChild(i).gameObject);
            }

            int maxVisiblePortraits = 3;
            int visibleCount = Mathf.Min(units.Count, maxVisiblePortraits);

            for (int i = 0; i < visibleCount; i++)
            {
                Unit unit = units[i];

                if (!unit || !unit.Portrait)
                    continue;

                Image portrait = Instantiate(portraitPrefab, portraitsContainer);
                portrait.sprite = unit.Portrait;
            }

            int hiddenCount = units.Count - visibleCount;
            stackCountText.gameObject.SetActive(hiddenCount > 0);

            if (hiddenCount > 0)
            {
                stackCountText.text = $"+{hiddenCount}";
            }
        }

        RefreshNodeColor();
    }
    
    private void RefreshNodeColor()
    {
        if (!nodeColor)
            return;

        if (units.Count > 0 && units[0])
        {
            nodeColor.color = units[0].isPlayerUnit
                ? occupiedAllyColor
                : occupiedEnemyColor;

            return;
        }

        nodeColor.color = supplyFaction switch
        {
            SupplyFaction.Ally => unoccupiedAllyColor,
            SupplyFaction.Enemy => unoccupiedEnemyColor,
            _ => neutralColor
        };
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
        if (!nodeColor) yield break;

        Color originalColor = nodeColor.color;
        nodeColor.color = Color.white;
        yield return new WaitForSeconds(0.12f);
        nodeColor.color = originalColor;
    }

    public IEnumerator PulsePortrait()
    {
        if (!portraitsContainer || portraitsContainer.childCount == 0)
            yield break;

        Transform transform = portraitsContainer;
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * 1.15f;

        float duration = 0.1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale =
                Vector3.Lerp(originalScale, targetScale, elapsed / duration);
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale =
                Vector3.Lerp(targetScale, originalScale, elapsed / duration);
            yield return null;
        }

        transform.localScale = originalScale;
    }
    #endregion
}