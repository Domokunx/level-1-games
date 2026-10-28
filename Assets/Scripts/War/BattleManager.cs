using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BattleState
{
    Deployment,
    PlayerTurn,
    EnemyTurn,
    Victory,
    Defeat
}
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance;
    [Header("Supply Settings")]
    public Node playerHeadquartersNode;
    public Node enemyHeadquartersNode;
    
    [Header("Deployment Settings")] 
    [SerializeField] private GameObject alliesParent;
    [SerializeField] private Unit unitPrefabToDeploy;
    [SerializeField] private int maxUnitsToDeploy = 3;
    private int _currentDeploysLeft = 0;
    
    [Header("Enemy Deployment Settings")]
    [SerializeField] private Unit enemyPrefabToDeploy;
    [SerializeField] private int maxEnemiesToSpawn = 3;
    [SerializeField] private GameObject enemiesParent;
    [SerializeField] private List<Node> nodesToSpawnEnemies = new List<Node>();

    [Header("Turn State")] 
    public BattleState currentState;
    
    // UI Refresher Events
    public System.Action<Node> OnUnitDeployed;
    public System.Action<int, int> OnDeploymentCountChanged;
    public System.Action<Unit> OnUnitSelected;
    public System.Action<BattleState> OnPhaseChanged;

    // Player Turn info
    private Unit selectedUnit;
    private List<Node> reachableNodes = new List<Node>();
    private Node inspectedNode;
    private bool isExecutingAction = false;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        } else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    { 
        ResetDeploymentState();   
        SpawnInitialEnemies();
        OnPhaseChanged?.Invoke(currentState);
    }
    private void OnEnable() => Node.OnNodeClicked += HandleNodeClick;
    private void OnDisable() => Node.OnNodeClicked -= HandleNodeClick;
    private void ResetDeploymentState()
    {
        currentState = BattleState.Deployment;
        _currentDeploysLeft = maxUnitsToDeploy;
        OnDeploymentCountChanged?.Invoke(_currentDeploysLeft, maxUnitsToDeploy);
    }
    
    public bool CanDeployAt(Node node) => 
        currentState == BattleState.Deployment && !node.currentUnit && _currentDeploysLeft > 0;
    public bool CanUndeployAt(Node node) =>
        currentState == BattleState.Deployment && node && node.currentUnit && node.currentUnit.isPlayerUnit;
    public bool CanMoveTo(Node node) => 
        currentState == BattleState.PlayerTurn && 
        selectedUnit && 
        !node.currentUnit && 
        reachableNodes.Contains(node);

    public bool CanAttack(Node node) =>
        currentState == BattleState.PlayerTurn &&
        selectedUnit &&
        node.currentUnit &&
        !node.currentUnit.isPlayerUnit &&
        selectedUnit.currentNode.neighbours.Contains(node);
    
    private void SpawnInitialEnemies()
    {
        if (enemyPrefabToDeploy == null)
        {
            Debug.LogWarning("Enemy Prefab to deploy is missing in BattleManager!");
            return;
        }

        int spawnedCount = 0;
        for (int i = 0; i < maxEnemiesToSpawn; i++)
        {
            if (nodesToSpawnEnemies.Count == 0)
            {
                Debug.LogWarning("Not enough unoccupied nodes to spawn all enemies!");
                break;
            }

            // Pick a random available node
            int randomIndex = Random.Range(0, nodesToSpawnEnemies.Count);
            Node targetNode = nodesToSpawnEnemies[randomIndex];
            nodesToSpawnEnemies.RemoveAt(randomIndex);

            // Instantiate the enemy 
            Transform parentTransform = enemiesParent != null ? enemiesParent.transform : null;
            GameObject enemyObj = Instantiate(enemyPrefabToDeploy.gameObject, targetNode.transform.position, Quaternion.identity, parentTransform);
            Unit newEnemy = enemyObj.GetComponent<Unit>();

            // Enemy props
            newEnemy.isPlayerUnit = false;

            // Occupy the node and set initial enemy supply faction/territory
            targetNode.OccupyNode(newEnemy);
            targetNode.SetSupplyFaction(SupplyFaction.Enemy, newEnemy);

            spawnedCount++;
        }

        // Refresh the edge lines to show the new enemy territory/supply trails
        NodeManager.Instance.UpdateAllPathColors();
    }
    
    private void HandleNodeClick(Node node)
    {
        if (isExecutingAction || !node) return;

        // Only handle path-line highlight switches during PlayerTurn
        if (currentState == BattleState.PlayerTurn)
        {
            if (inspectedNode != null && inspectedNode != node && reachableNodes.Contains(inspectedNode) && selectedUnit != null && inspectedNode != selectedUnit.currentNode)
            {
                inspectedNode.SetHighlightState(NodeHighlightState.ReachableMove);
            }
        }

        inspectedNode = node;

        switch (currentState)
        {
            case BattleState.PlayerTurn when node.currentUnit != null && node.currentUnit.isPlayerUnit:
                TrySelectUnit(node.currentUnit);
                break;
            
            case BattleState.PlayerTurn:
            {
                if (node.CurrentHighlightState == NodeHighlightState.None && !node.currentUnit)
                {
                    DeselectUnit();
                }

                break;
            }
            
            case BattleState.Deployment:
                break;
        }

        BattleUIManager.Instance.ShowInspectedNodeUI(inspectedNode, selectedUnit);
    }
    public void HandleNodeHover(Node hoveredNode)
    {
        if (isExecutingAction || !hoveredNode) return;
        if (currentState != BattleState.PlayerTurn || selectedUnit == null || selectedUnit.hasActedThisTurn) return;
        
        if (reachableNodes.Contains(hoveredNode) && hoveredNode != selectedUnit.currentNode)
        {
            hoveredNode.SetHighlightState(NodeHighlightState.Preview);
        }
    }
    public void HandleNodeHoverExit(Node exitedNode)
    {
        if (isExecutingAction || exitedNode == null) return;
        if (currentState != BattleState.PlayerTurn || selectedUnit == null || selectedUnit.hasActedThisTurn) return;

        if (reachableNodes.Contains(exitedNode) && exitedNode != selectedUnit.currentNode && exitedNode != inspectedNode)
        {
            exitedNode.SetHighlightState(NodeHighlightState.ReachableMove);
        }
    }
    private void TrySelectUnit(Unit unit)
    {
        if (!unit || !unit.isPlayerUnit) return;
        
        NodeManager.Instance.ClearAllHighlights();
        reachableNodes.Clear();
        selectedUnit = unit;
        
        if (!unit.hasActedThisTurn)
        {
            // TODO: Add highlighting logic
            reachableNodes = NodeManager.Instance.GetNodesInRange(unit.currentNode, unit.MoveRange);
            NodeManager.Instance.HighlightRangesForUnit(unit);
        }
        else
        {
            if (unit.currentNode)
            {
                unit.currentNode.SetHighlightState(NodeHighlightState.Selected);
            }
        }

        if (inspectedNode)
        {
            BattleUIManager.Instance.ShowInspectedNodeUI(inspectedNode, selectedUnit);
        }
    }
    
    private void DeselectUnit()
    {
        NodeManager.Instance.ClearAllHighlights();
        selectedUnit = null;
        inspectedNode = null;
        reachableNodes.Clear();
        BattleUIManager.Instance.HideAllPanels();   
    }
    
    private IEnumerator ExecuteMove(Unit unit, Node destinationNode)
    {
        isExecutingAction = true;

        List<Node> path = PathFinder.Instance.FindPath(unit.currentNode, destinationNode);
        if (path != null)
        {
            // Determine faction based on who is moving
            SupplyFaction movingFaction = unit.isPlayerUnit ? SupplyFaction.Ally : SupplyFaction.Enemy;
            
            // Set supply trail
            foreach (Node node in path)
            {
                node.SetSupplyFaction(movingFaction, unit);
            }
            // Color the supply trail after traversal
            NodeManager.Instance.UpdateAllPathColors();
            
            // Update node occupancy
            yield return StartCoroutine(unit.MoveAlongPath(path));
        }

        unit.hasActedThisTurn = true;
        DeselectUnit();
        TrySelectUnit(unit); // Might be QoL if new unit state is shown
        isExecutingAction = false;
        
    }

    private IEnumerator ExecuteAttack(Unit attacker, Unit defender)
    {
        isExecutingAction = true;

        // Determine strike order based on Speed
        Unit firstStriker = attacker;
        Unit secondStriker = defender;

        if (defender.Speed > attacker.Speed)
        {
            firstStriker = defender;
            secondStriker = attacker;
        }

        yield return new WaitForSeconds(0.3f);

        // --- FIRST STRIKE ---
        if (secondStriker.currentNode)
        {
            StartCoroutine(secondStriker.currentNode.FlashDamageEffect());
            StartCoroutine(secondStriker.currentNode.PulsePortrait());
        }
        secondStriker.TakeDamage(firstStriker.GetEffectiveAttack);
        yield return new WaitForSeconds(0.5f);

        // Check if the first strike defeated the target
        if (secondStriker.currentHealth <= 0)
        {
            HandleUnitDefeat(secondStriker, firstStriker);
            yield return new WaitForSeconds(0.5f);
            DeselectUnit();
            isExecutingAction = false;
            yield break;
        }

        // --- COUNTER-ATTACK (Second Striker survives) ---
        if (firstStriker.currentNode)
        {
            StartCoroutine(firstStriker.currentNode.FlashDamageEffect());
            StartCoroutine(firstStriker.currentNode.PulsePortrait());
        }

        firstStriker.TakeDamage(secondStriker.GetEffectiveAttack);
        yield return new WaitForSeconds(0.5f);

        // Check if the counter-attack defeated the attacker
        if (firstStriker.currentHealth <= 0)
        {
            HandleUnitDefeat(firstStriker, secondStriker);
            yield return new WaitForSeconds(0.5f);
        }

        attacker.hasActedThisTurn = true;
        DeselectUnit();
        TrySelectUnit(attacker);
        isExecutingAction = false;
    }
    private void HandleUnitDefeat(Unit defeatedUnit, Unit winningUnit)
    {
        if (!defeatedUnit) return;
        Node targetNode = defeatedUnit.currentNode;

        // 1. Unoccupy and destroy the defeated unit
        if (targetNode)
        {
            targetNode.UnoccupyNode();
        }
    
        if (defeatedUnit.gameObject)
        {
            Destroy(defeatedUnit.gameObject);
        }

        // 2. If the winning unit was the attacker, they move into the conquered node
        if (winningUnit && winningUnit.currentNode != targetNode)
        {
            Node startNode = winningUnit.currentNode;
            if (startNode)
            {
                startNode.UnoccupyNode();
            }
        
            if (targetNode)
            {
                targetNode.OccupyNode(winningUnit);
                winningUnit.transform.position = targetNode.transform.position;
            }
        }

        // 3. Capture supply line and update path colors
        if (targetNode && winningUnit)
        {
            SupplyFaction conqueringFaction = winningUnit.isPlayerUnit ? SupplyFaction.Ally : SupplyFaction.Enemy;
            targetNode.SetSupplyFaction(conqueringFaction, winningUnit);
            NodeManager.Instance.UpdateAllPathColors();
        }
    }
    public void OnClickDeploy()
    {
        if (!CanDeployAt(inspectedNode)) return;
        if (!unitPrefabToDeploy) return;
        
        GameObject unitObj = Instantiate(unitPrefabToDeploy.gameObject, inspectedNode.transform.position, Quaternion.identity, alliesParent.transform);
        Unit newUnit = unitObj.GetComponent<Unit>();
        
        NodeManager.OccupyNode(inspectedNode, newUnit);
        newUnit.isPlayerUnit = true;
        SupplyFaction deploymentFaction = newUnit.isPlayerUnit ? SupplyFaction.Ally : SupplyFaction.Enemy;
        inspectedNode.SetSupplyFaction(deploymentFaction, newUnit);
        NodeManager.Instance.UpdateAllPathColors();

        _currentDeploysLeft--;

        OnDeploymentCountChanged?.Invoke(_currentDeploysLeft, maxUnitsToDeploy);
        OnUnitDeployed?.Invoke(inspectedNode);
        
        // change later
        if (_currentDeploysLeft <= 0)
        {
            StartPlayerTurn();
        }
    }

    public void OnClickUndeploy()
    {
        if (!CanUndeployAt(inspectedNode)) return;

        Unit unitToRemove = inspectedNode.currentUnit;
        NodeManager.Instance.UnoccupyNode(inspectedNode);
        Destroy(unitToRemove.gameObject);

        _currentDeploysLeft++;
        
        OnDeploymentCountChanged?.Invoke(_currentDeploysLeft, maxUnitsToDeploy);
        OnUnitDeployed?.Invoke(inspectedNode);
    }

    private void StartPlayerTurn()
    {
        currentState = BattleState.PlayerTurn;
        inspectedNode = null;
        selectedUnit = null;
        
        // OnPhaseChanged?.Invoke();
        BattleUIManager.Instance.HideAllPanels();
        Debug.Log("Deployment Complete. Player Turn Started!");
        OnPhaseChanged?.Invoke(currentState);
    }
    
    public void OnClickAttack()
    {
        if (!CanAttack(inspectedNode)) return;

        Unit attacker = selectedUnit;
        Unit defender = inspectedNode.currentUnit;

        if (attacker && defender)
        {
            StartCoroutine(ExecuteAttack(attacker, defender));
        }
    }
    

    public void OnClickMove()
    {
        if (!CanMoveTo(inspectedNode)) return;
        
        StartCoroutine(ExecuteMove(selectedUnit, inspectedNode));
    }
    
    public void OnClickEndPlayerTurn()
    {
        if (currentState != BattleState.PlayerTurn || isExecutingAction) return;

        DeselectUnit();
        currentState = BattleState.EnemyTurn;
        Debug.Log("Player Turn Ended. Enemy AI Turn Beginning...");
        OnPhaseChanged?.Invoke(currentState);
        
        // Start the enemy turn coroutine
        StartCoroutine(ExecuteEnemyTurn());
    }
    
    private IEnumerator ExecuteEnemyTurn()
    {
        isExecutingAction = true;
        yield return new WaitForSeconds(0.5f);

        Unit[] allUnits = FindObjectsByType<Unit>(FindObjectsSortMode.None);
        List<Unit> enemyUnits = new List<Unit>();
        foreach (var u in allUnits)
        {
            if (u && !u.isPlayerUnit)
            {
                enemyUnits.Add(u);
            }
        }

        foreach (var enemy in enemyUnits)
        {
            if (!enemy || !enemy.currentNode ||  enemy.currentHealth <= 0) continue;

            // 1. Check for immediate Attack targets (Range 1)
            Unit targetPlayerToAttack = null;
            foreach (var neighbor in enemy.currentNode.neighbours)
            {
                
                if (neighbor.isOccupied && neighbor.currentUnit && neighbor.currentUnit.isPlayerUnit)
                {
                    targetPlayerToAttack = neighbor.currentUnit;
                    break;
                }
            }

            if (targetPlayerToAttack)
            {
                yield return StartCoroutine(ExecuteAttack(enemy, targetPlayerToAttack));
                yield return new WaitForSeconds(0.4f);
                continue;
            }

            // Get nodes strictly within this enemy's movement range
            List<Node> enemyReachable = NodeManager.Instance.GetNodesInRange(enemy.currentNode, enemy.MoveRange);
            if (enemyReachable.Count == 0) continue;

            // 2. Check for Vulnerable Supply Nodes STRICTLY WITHIN Move Range
            Node targetSupplyNode = null;
            foreach (var reachableNode in enemyReachable)
            {
                if (!reachableNode.isOccupied && reachableNode.supplyFaction == SupplyFaction.Ally)
                {
                    targetSupplyNode = reachableNode;
                    break;
                }
            }

            if (targetSupplyNode)
            {
                yield return StartCoroutine(ExecuteMove(enemy, targetSupplyNode));
                yield return new WaitForSeconds(0.4f);
                continue;
            }

            // 3. Otherwise: Aggressively advance toward closest player unit, but pick the best destination WITHIN RANGE
            Unit closestPlayer = NodeManager.FindClosestPlayerUnit(enemy.currentNode);
            if (closestPlayer && closestPlayer.currentNode)
            {
                Node bestMoveNode = null;
                float minDistance = float.MaxValue;

                foreach (var node in enemyReachable)
                {
                    if (node.isOccupied) continue;

                    float dist = Vector3.Distance(node.transform.position, closestPlayer.currentNode.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestMoveNode = node;
                    }
                }

                if (bestMoveNode && bestMoveNode != enemy.currentNode)
                {
                    yield return StartCoroutine(ExecuteMove(enemy, bestMoveNode));
                    yield return new WaitForSeconds(0.3f);
                }
            }
        }

        // Reset player turn states
        foreach (var u in allUnits)
        {
            if (u && u.isPlayerUnit)
            {
                u.ResetTurnState();
            }
        }

        currentState = BattleState.PlayerTurn;
        isExecutingAction = false;
        Debug.Log("Enemy Turn Completed. Player Turn Started.");
        OnPhaseChanged?.Invoke(currentState);
    }
}
