using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleUIManager : MonoBehaviour
{
    public static BattleUIManager Instance;

    [Header("Ally Panel (Left)")]
    [SerializeField] private GameObject selectedUnitPanel;
    [SerializeField] private Image selectedPortrait;
    [SerializeField] private TextMeshProUGUI selectedNameText;
    [SerializeField] private TextMeshProUGUI selectedStatsText;
    [SerializeField] private TextMeshProUGUI selectedHealthText;

    [Header("Enemy Panel (Right)")]
    [SerializeField] private GameObject enemyNodePanel;
    [SerializeField] private Image enemyPortrait;
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private TextMeshProUGUI enemyStatsText;
    [SerializeField] private TextMeshProUGUI enemyHealthText;

    [Header("Deployment UI Overlay")]
    [SerializeField] private GameObject deploymentCounterPanel;
    [SerializeField] private TextMeshProUGUI deploymentCounterText;

    [Header("Smart Contextual Action Button")]
    [SerializeField] private GameObject contextualActionButtons;
    [SerializeField] private Button primaryActionButton;
    [SerializeField] private TextMeshProUGUI actionButtonText;
    [SerializeField] private Button endTurnButton;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        HideAllPanels();
    }

    private void Start()
    {
        if (BattleManager.Instance == null) return;
        BattleManager.Instance.OnUnitDeployed += HandleUnitDeployed;
        BattleManager.Instance.OnDeploymentCountChanged += UpdateDeploymentCounter;
        BattleManager.Instance.OnPhaseChanged += HandlePhaseChanged;

        HandlePhaseChanged(BattleManager.Instance.currentState);
    }

    private void OnDestroy()
    {
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnUnitDeployed -= HandleUnitDeployed;
            BattleManager.Instance.OnDeploymentCountChanged -= UpdateDeploymentCounter;
            BattleManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
        }
    }

    public void ShowInspectedNodeUI(Node node, Unit selectedUnit)
    {
        if (!node)
        {
            HideAllPanels();
            return;
        }

        node.RemoveInvalidUnits();

        // Always update the action button, including for empty deployment nodes
        UpdatePrimaryActionButton(node, selectedUnit);

        if (node.ContainsPlayerUnits)
            ShowSelectedStackUI(node);
        else
            HideSelectedUnitUI();

        if (node.ContainsEnemyUnits)
            ShowEnemyStackUI(node);
        else
            HideEnemyUnitUI();
    }

    private void UpdatePrimaryActionButton(Node node, Unit selectedUnit)
    {
        ToggleButtonsAccordingToState(true);
        primaryActionButton.onClick.RemoveAllListeners();

        bool isDeployment = BattleManager.Instance.currentState == BattleState.Deployment;
        bool isPlayerTurn = BattleManager.Instance.currentState == BattleState.PlayerTurn;

        if (isDeployment)
        {
            if (BattleManager.Instance.CanDeployAt(node))
            {
                actionButtonText.text = "Deploy Unit";
                primaryActionButton.interactable = true;
                primaryActionButton.onClick.AddListener(() => BattleManager.Instance.OnClickDeploy());
            }
            else if (BattleManager.Instance.CanUndeployAt(node))
            {
                actionButtonText.text = "Undeploy";
                primaryActionButton.interactable = true;
                primaryActionButton.onClick.AddListener(() => BattleManager.Instance.OnClickUndeploy());
            }
            else
            {
                actionButtonText.text = "Cannot Deploy";
                primaryActionButton.interactable = false;
            }
        }
        else if (isPlayerTurn)
        {
            if (selectedUnit && selectedUnit.hasActedThisTurn)
            {
                actionButtonText.text = "Already Acted";
                primaryActionButton.interactable = false;
                return;
            }
            
            if (BattleManager.Instance.CanMoveTo(node))
            {
                actionButtonText.text = "Move Here";
                primaryActionButton.interactable = true;
                primaryActionButton.onClick.AddListener(() => BattleManager.Instance.OnClickMove());
            }
            else if (BattleManager.Instance.CanAttack(node))
            {
                actionButtonText.text = "Attack";
                primaryActionButton.interactable = true;
                primaryActionButton.onClick.AddListener(() => BattleManager.Instance.OnClickAttack());
            }
            else
            {
                actionButtonText.text = "Action Unavailable";
                primaryActionButton.interactable = false;
            }
        }
    }

    private void UpdateDeploymentCounter(int remaining, int max)
    {
        if (deploymentCounterText != null)
        {
            deploymentCounterText.text = $"Deployments Remaining: {remaining} / {max}";
        }
    }

    private void ShowSelectedStackUI(Node unitStack)
    {
        if (!unitStack || !unitStack.ContainsPlayerUnits || unitStack.units.Count == 0)
        {
            HideSelectedUnitUI();
            return;
        }

        Unit unit = unitStack.units[0];

        if (!unit)
        {
            HideSelectedUnitUI();
            return;
        }
        selectedUnitPanel.SetActive(true);
        selectedNameText.text = unit.UnitName;

        string supplyStatus = unit.IsSupplied ? "<color=green>(+50%)</color>" : "<color=red>(Isolated)</color>";
        selectedStatsText.text = $"ATK: {unitStack.StackEffectiveAttack}{supplyStatus} | MOV: {unitStack.StackMoveRange}";
        selectedHealthText.text = $"HP: {unitStack.StackHealth}/{unitStack.StackMaxHealth}";
        if (!selectedPortrait) return;
        selectedPortrait.gameObject.SetActive(unit.Portrait != null);
        selectedPortrait.sprite = unit.Portrait;
    }

    private void ShowEnemyStackUI(Node enemyNode)
    {
        if (!enemyNode || !enemyNode.ContainsEnemyUnits || enemyNode.units.Count == 0)
        {
            HideEnemyUnitUI();
            return;
        }

        Unit enemy = enemyNode.units[0];

        if (!enemy)
        {
            HideEnemyUnitUI();
            return;
        }
        enemyNodePanel.SetActive(true);
        enemyNameText.text = enemy.UnitName;
        
        string supplyStatus = enemy.IsSupplied ? "<color=green>(+50%)</color>" : "<color=red>(Isolated)</color>";
        enemyStatsText.text = $" | ATK: {enemyNode.StackEffectiveAttack}{supplyStatus} | MOV: {enemyNode.StackSpeed}";
        selectedHealthText.text = $"HP: {enemyNode.StackHealth}/{enemyNode.StackMaxHealth}";

        if (!enemyPortrait) return;
        enemyPortrait.gameObject.SetActive(enemy.Portrait != null);
        enemyPortrait.sprite = enemy.Portrait;
    }

    private void HideSelectedUnitUI() => selectedUnitPanel.SetActive(false);
    private void HideEnemyUnitUI() => enemyNodePanel.SetActive(false);
    private void ToggleButtonsAccordingToState(bool isOn) => contextualActionButtons.SetActive(isOn);

    public void HideAllPanels()
    {
        HideSelectedUnitUI();
        HideEnemyUnitUI();
        ToggleButtonsAccordingToState(false);
    }

    private void HandleUnitDeployed(Node deployedNode)
    {
        ShowInspectedNodeUI(deployedNode, null);
    }

    private void HandlePhaseChanged(BattleState phase)
    {
        switch (phase)
        {
            case BattleState.Deployment:
                deploymentCounterPanel.SetActive(true);
                endTurnButton.gameObject.SetActive(false);
                break;
            
            case BattleState.PlayerTurn:
                endTurnButton.gameObject.SetActive(true);
                deploymentCounterPanel.SetActive(false);
                break;
            
            case BattleState.EnemyTurn:
                deploymentCounterPanel.SetActive(false);
                endTurnButton.gameObject.SetActive(false);
                break;
            
            default:
                if (deploymentCounterPanel) deploymentCounterPanel.SetActive(false);
                break;
        }

    }
}