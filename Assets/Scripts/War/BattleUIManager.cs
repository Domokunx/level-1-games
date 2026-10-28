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

    [Header("Enemy Panel (Right)")]
    [SerializeField] private GameObject enemyNodePanel;
    [SerializeField] private Image enemyPortrait;
    [SerializeField] private TextMeshProUGUI enemyNameText;
    [SerializeField] private TextMeshProUGUI enemyStatsText;

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
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnUnitDeployed += HandleUnitDeployed;
            BattleManager.Instance.OnDeploymentCountChanged += UpdateDeploymentCounter;
            BattleManager.Instance.OnPhaseChanged += HandlePhaseChanged;

            if (BattleManager.Instance.currentState == BattleState.Deployment && deploymentCounterPanel)
            {
                deploymentCounterPanel.SetActive(true);
            }
        }
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
        if (node == null)
        {
            HideAllPanels();
            return;
        }

        // Ally UI
        Unit allyToShow = null;
        if (BattleManager.Instance.currentState == BattleState.Deployment)
        {
            if (node.currentUnit != null && node.currentUnit.isPlayerUnit)
            {
                allyToShow = node.currentUnit;
            }
        }
        else
        {
            allyToShow = (selectedUnit != null) ? selectedUnit : (node.currentUnit != null && node.currentUnit.isPlayerUnit ? node.currentUnit : null);
        }

        if (allyToShow != null) ShowSelectedUnitUI(allyToShow);
        else HideSelectedUnitUI();

        // Enemy UI
        if (node.currentUnit != null && !node.currentUnit.isPlayerUnit)
        {
            ShowEnemyUnitUI(node.currentUnit);
        }
        else
        {
            HideEnemyUnitUI();
        }

        UpdatePrimaryActionButton(node, selectedUnit);
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

    private void ShowSelectedUnitUI(Unit unit)
    {
        selectedUnitPanel.SetActive(true);
        selectedNameText.text = unit.UnitName;
        selectedStatsText.text = $"HP: {unit.currentHealth} | ATK: {unit.AttackPower} | MOV: {unit.MoveRange}";

        string supplyStatus = unit.IsSupplied ? "<color=green>(+50%)</color>" : "<color=red>(Isolated)</color>";
        selectedStatsText.text = $"HP: {unit.currentHealth}/{unit.MaxHealth} | ATK: {unit.GetEffectiveAttack}{supplyStatus} | MOV: {unit.MoveRange}";

        if (!selectedPortrait) return;
        selectedPortrait.gameObject.SetActive(unit.Portrait != null);
        selectedPortrait.sprite = unit.Portrait;
    }

    private void ShowEnemyUnitUI(Unit enemyUnit)
    {
        enemyNodePanel.SetActive(true);
        enemyNameText.text = enemyUnit.UnitName;
        
        string supplyStatus = enemyUnit.IsSupplied ? "<color=green>(+50%)</color>" : "<color=red>(Isolated)</color>";
        enemyStatsText.text = $"HP: {enemyUnit.currentHealth}/{enemyUnit.MaxHealth} | ATK: {enemyUnit.GetEffectiveAttack}{supplyStatus} | SPD: {enemyUnit.Speed}";

        if (!enemyPortrait) return;
        enemyPortrait.gameObject.SetActive(enemyUnit.Portrait != null);
        enemyPortrait.sprite = enemyUnit.Portrait;
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