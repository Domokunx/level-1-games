using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        playerHeadquartersNode.SetSupplyFaction(SupplyFaction.Ally);
        enemyHeadquartersNode.SetSupplyFaction(SupplyFaction.Enemy);
        
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
        currentState == BattleState.Deployment && node && 
        (!node.HasUnits() || node.units[0].isPlayerUnit) && 
        _currentDeploysLeft > 0;
    public bool CanUndeployAt(Node node) =>
        currentState == BattleState.Deployment && node && node.HasUnits()&& node.units[0] && node.units[0].isPlayerUnit;
    public bool CanMoveTo(Node node) => 
        currentState == BattleState.PlayerTurn && 
        selectedUnit && 
        node &&
        (!node.HasUnits() || selectedUnit.isPlayerUnit == node.units[0].isPlayerUnit) &&
        reachableNodes.Contains(node);

    public bool CanAttack(Node node) =>
        currentState == BattleState.PlayerTurn &&
        selectedUnit &&
        selectedUnit.currentNode &&
        node &&
        node.HasUnits()&&
        node.units[0] &&
        !node.units[0].isPlayerUnit &&
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
            targetNode.AddUnit(newEnemy);
            targetNode.SetSupplyFaction(SupplyFaction.Enemy, newEnemy);

            spawnedCount++;
        }

        // Refresh the edge lines to show the new enemy territory/supply trails
        NodeManager.Instance.RefreshSupplyState();
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
            case BattleState.PlayerTurn:
            {
                bool isReachableDestination =
                    selectedUnit &&
                    selectedUnit.currentNode != node &&
                    reachableNodes.Contains(node);

                if (isReachableDestination)
                {
                    inspectedNode = node;
                    BattleUIManager.Instance.ShowInspectedNodeUI(node, selectedUnit);
                    break;
                }
                
                bool hasPlayerUnit =
                    node.HasUnits() &&
                    node.units[0] &&
                    node.units[0].isPlayerUnit;

                if (hasPlayerUnit)
                {
                    TrySelectUnit(node.units[0]);
                }
                else if (node.CurrentHighlightState == NodeHighlightState.None &&
                         !node.HasUnits())
                {
                    // Clicking an empty, non-reachable node deselects.
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
            reachableNodes = NodeManager.Instance.GetNodesInRange(unit.currentNode, unit.currentNode.StackMoveRange);
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
            foreach (Unit stackUnit in unit.currentNode.units)
            {
                stackUnit.hasActedThisTurn = true;
            }
            
            // Determine faction based on who is moving
            SupplyFaction movingFaction = unit.isPlayerUnit ? SupplyFaction.Ally : SupplyFaction.Enemy;
            
            // Set supply trail
            foreach (Node node in path)
            {
                node.SetSupplyFaction(movingFaction, unit);
            }
            // Color the supply trail after traversal
            NodeManager.Instance.RefreshSupplyState();
            
            // Update node occupancy
            yield return StartCoroutine(MoveStackAlongPath(path, unit.currentNode));
        }

        DeselectUnit();
        isExecutingAction = false;
    }

    public IEnumerator MoveStackAlongPath(List<Node> path, Node currentNode, float stepDelay = 0.15f)
    {
        foreach (Node node in path)
        {
            NodeManager.Instance.MoveStackToNode(currentNode, node);
            currentNode = node;
            yield return new WaitForSeconds(stepDelay);
        }
    }
    
    private IEnumerator ExecuteAttack(Node attacker, Node defender)
    {
        isExecutingAction = true;
        foreach (var unit in attacker.units)
        {
            unit.hasActedThisTurn = true;
        }

        // Determine strike order based on Speed
        Node firstStriker = attacker;
        Node secondStriker = defender;

        if (defender.StackSpeed > attacker.StackSpeed)
        {
            firstStriker = defender;
            secondStriker = attacker;
        }

        yield return new WaitForSeconds(0.3f);

        // --- FIRST STRIKE ---
        if (secondStriker)
        {
            StartCoroutine(secondStriker.FlashDamageEffect());
            StartCoroutine(secondStriker.PulsePortrait());
        }
        secondStriker.TakeStackDamage(firstStriker.StackEffectiveAttack);
        yield return new WaitForSeconds(0.5f);

        // Check if the first strike defeated the target
        if (secondStriker.StackHealth <= 0)
        {
            NodeManager.Instance.MoveStackToNode(firstStriker, secondStriker);
            yield return new WaitForSeconds(0.5f);
            
            DeselectUnit();
            isExecutingAction = false;
            yield break;
        }

        // --- COUNTER-ATTACK (Second Striker survives) ---
        if (firstStriker)
        {
            StartCoroutine(firstStriker.FlashDamageEffect());
            StartCoroutine(firstStriker.PulsePortrait());
        }

        firstStriker.TakeStackDamage(secondStriker.StackEffectiveAttack);
        yield return new WaitForSeconds(0.5f);

        // Check if the counter-attack defeated the attacker
        if (firstStriker.StackHealth <= 0)
        {
            NodeManager.Instance.MoveStackToNode(secondStriker, firstStriker);
            yield return new WaitForSeconds(0.5f);
        }

        DeselectUnit();
        isExecutingAction = false;
    }
    
    public void OnClickDeploy()
    {
        if (!CanDeployAt(inspectedNode)) return;
        if (!unitPrefabToDeploy) return;
        
        GameObject unitObj = Instantiate(unitPrefabToDeploy.gameObject, inspectedNode.transform.position, Quaternion.identity, alliesParent.transform);
        Unit newUnit = unitObj.GetComponent<Unit>();
        
        newUnit.isPlayerUnit = true;
        NodeManager.OccupyNode(inspectedNode, newUnit);
        SupplyFaction deploymentFaction = newUnit.isPlayerUnit ? SupplyFaction.Ally : SupplyFaction.Enemy;
        inspectedNode.SetSupplyFaction(deploymentFaction, newUnit);
        NodeManager.Instance.RefreshSupplyState();

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

        Unit unitToRemove = inspectedNode.units[^1];
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
        Node defender = inspectedNode;

        if (attacker && defender)
        {
            StartCoroutine(ExecuteAttack(attacker.currentNode, defender));
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
        
        HashSet<Node> processedNodes = new();

        foreach (Unit enemy in enemyUnits)
        {
            if (!enemy || !enemy.currentNode || !processedNodes.Add(enemy.currentNode)) continue;
            
            Node enemyStack = enemy.currentNode;


            // Get nodes strictly within this enemy's movement range
            List<Node> enemyReachable = NodeManager.Instance.GetNodesInRange(enemyStack, enemyStack.StackMoveRange);
            if (enemyReachable.Count == 0) continue;

            Dictionary<Node, int> graphDistances =
                NodeManager.Instance.GetGraphDistances(enemyStack);
            
            // Check for Vulnerable Supply Nodes 
            Node targetSupplyNode = null;
            float highestSupplyThreat = float.MinValue;

            foreach (Node candidate in enemyReachable)
            {
                if (!candidate || candidate.isOccupied || candidate.supplyFaction != SupplyFaction.Ally)
                {
                    continue;
                }

                // Ignore territory that is already disconnected from the Allied HQ.
                if (!NodeManager.Instance.IsNodeSupplied(candidate, SupplyFaction.Ally))
                {
                    continue;
                }

                int alliedConnections = 0;

                foreach (Node neighbour in candidate.neighbours)
                {
                    if (neighbour && neighbour.supplyFaction == SupplyFaction.Ally && NodeManager.Instance.IsNodeSupplied(neighbour, SupplyFaction.Ally))
                    {
                        alliedConnections++;
                    }
                }

                // Fewer connections means capturing this node is more likely to sever graph
                float vulnerabilityScore = 100f - alliedConnections * 25f;

                // Prefer nodes closer to the enemy
                if (graphDistances.TryGetValue(candidate, out int graphDistance))
                {
                    vulnerabilityScore -= graphDistance * 10f;
                }

                if (!(vulnerabilityScore > highestSupplyThreat)) continue;
                
                highestSupplyThreat = vulnerabilityScore;
                targetSupplyNode = candidate;
            }

            if (targetSupplyNode)
            {
                yield return StartCoroutine(ExecuteMove(enemy, targetSupplyNode));
                yield return new WaitForSeconds(0.4f);
                continue;
            }
            
            // Check for immediate Attack targets (Range 1)
            Node targetNodeToAttack = null;
            int lowestHealth = int.MaxValue;
            bool foundLethalTarget = false;

            foreach (Node neighbour in enemyStack.neighbours)
            {
                if (!neighbour || !neighbour.HasUnits() || !neighbour.units[0] || !neighbour.units[0].isPlayerUnit) continue;

                Node candidate = neighbour;
                bool canKill = enemyStack.StackEffectiveAttack >= candidate.StackHealth;

                if (canKill && !foundLethalTarget)
                {
                    targetNodeToAttack = candidate;
                    lowestHealth = candidate.StackHealth;
                    foundLethalTarget = true;
                }
                else if (canKill == foundLethalTarget && candidate.StackHealth < lowestHealth)
                {
                    targetNodeToAttack = candidate;
                    lowestHealth = candidate.StackHealth;
                }
            }

            if (targetNodeToAttack)
            {
                yield return StartCoroutine(ExecuteAttack(enemy.currentNode, targetNodeToAttack));
                yield return new WaitForSeconds(0.4f);
                continue;
            }

            // Otherwise: Aggressively advance toward closest player unit, but pick the best destination WITHIN RANGE
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
    
    private int GetGraphDistance(Node start, Node target)
    {
        if (!start || !target)
            return int.MaxValue;

        Queue<(Node node, int distance)> queue = new();
        HashSet<Node> visited = new();

        queue.Enqueue((start, 0));
        visited.Add(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current.node == target)
                return current.distance;

            foreach (Node neighbour in current.node.neighbours)
            {
                if (!neighbour || !visited.Add(neighbour))
                    continue;

                queue.Enqueue((neighbour, current.distance + 1));
            }
        }

        return int.MaxValue;
    }
}
