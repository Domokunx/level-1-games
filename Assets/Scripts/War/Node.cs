using System;
using System.Collections;
using System.Collections.Generic;
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
    public Unit currentUnit; // Reverted back to single unit reference
    public bool isOccupied => currentUnit != null;
    public bool isOccupiedByAlly => isOccupied && currentUnit.isPlayerUnit;
    
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
    
    private void Awake()
    {
        if (currentUnit != null) currentUnit.currentNode = this;
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

    public void OccupyNode(Unit unit)
    {
        if (!unit) return;
        currentUnit = unit;
        unit.currentNode = this;

        if (unitImage)
        {
            unitImage.enabled = true;
            unitImage.sprite = unit.Portrait;
        }

        if (nodeColor)
        {
            nodeColor.color = unit.isPlayerUnit ? occupiedAllyColor : occupiedEnemyColor;
        }
    }

    public void UnoccupyNode()
    {
        if (currentUnit)
        {
            currentUnit.currentNode = null;
            currentUnit = null;
        }

        if (unitImage)
        {
            unitImage.enabled = false;
            unitImage.sprite = null;
        }

        if (nodeColor)
        {
            nodeColor.color = supplyFaction switch
            {
                SupplyFaction.Ally => unoccupiedAllyColor,
                SupplyFaction.Enemy => unoccupiedEnemyColor,
                _ => neutralColor
            };
        }
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