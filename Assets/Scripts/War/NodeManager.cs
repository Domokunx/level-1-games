using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NodeManager : MonoBehaviour
{
    public static NodeManager Instance;

    private Node[] _nodes;
    private readonly HashSet<Path> _paths = new HashSet<Path>();
    private readonly Dictionary<Path, LineRenderer> _pathLineRenderers = new Dictionary<Path, LineRenderer>();
    private readonly HashSet<Node> _allySuppliedNodes = new();
    private readonly HashSet<Node> _enemySuppliedNodes = new();
    
    [Header("Supply Line Edge Colors")]
    [SerializeField] private Color allyLineColor = Color.blue;
    [SerializeField] private Color enemyLineColor = Color.red;
    [SerializeField] private Color neutralLineColor = Color.gray;
    
    [SerializeField] private GameObject pathParent;
    [SerializeField] private LineRenderer lineStyle;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        FindALlNodes();
        // ValidateAndRepairNeighbours();
        GenerateAllPaths();
    }

    private void Start()
    {
        // Init Map
        RenderAllPaths();
    }

    private void FindALlNodes()
    {
        _nodes = GetComponentsInChildren<Node>(); 
    }
    
    private void GenerateAllPaths()
    {
        _paths.Clear();

        if (_nodes == null) return;

        foreach (Node n in _nodes)
        {
            if (n == null || n.neighbours == null) continue;

            foreach (var neighbour in n.neighbours)
            {
                if (neighbour == null) continue;

                _paths.Add(new Path(n, neighbour));
            }
        }
    }

    private void RenderAllPaths()
    {
        // Clear out old ones if re-rendering
        foreach (var kvp in _pathLineRenderers)
        {
            if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        }
        _pathLineRenderers.Clear();

        foreach (var p in _paths)
        {
            var lineObj = Instantiate(lineStyle, pathParent.transform);
            lineObj.positionCount = 2;
            lineObj.SetPosition(0, p.From.transform.position);
            lineObj.SetPosition(1, p.To.transform.position);

            _pathLineRenderers.Add(p, lineObj);
        }

        RefreshSupplyState();
    }

    public void RefreshSupplyState()
    {
        RecalculateSupplyConnections();
        UpdateAllPathColors();
    }
    public void UpdateAllPathColors()
    {
        foreach (var kvp in _pathLineRenderers)
        {
            Path path = kvp.Key;
            LineRenderer line = kvp.Value;

            if (!line || !path.From || !path.To)
                continue;

            Color targetColor = neutralLineColor;

            SupplyFaction fromFaction = path.From.supplyFaction;
            SupplyFaction toFaction = path.To.supplyFaction;

            // Neutral or mixed-faction edges are never active supply lines.
            if (fromFaction == SupplyFaction.Neutral || toFaction == SupplyFaction.Neutral ||
                fromFaction != toFaction)
            {
                line.startColor = targetColor;
                line.endColor = targetColor;
                continue;
            }

            bool fromConnected = IsNodeSupplied(path.From, fromFaction);
            bool toConnected = IsNodeSupplied(path.To, fromFaction);

            // An edge is active only when both endpoints are HQ-connected.
            if (!fromConnected || !toConnected)
            {
                line.startColor = targetColor;
                line.endColor = targetColor;
                continue;
            }

            targetColor = fromFaction == SupplyFaction.Ally
                ? allyLineColor
                : enemyLineColor;

            line.startColor = targetColor;
            line.endColor = targetColor;
        }
    }

    public void ResetNodeCosts()
    {
        foreach (Node n in _nodes)
        {
            n.gCost = float.PositiveInfinity;
            n.hCost = 0f;
            n.parent = null;
        }
    }

    public bool IsNodeSupplied(Node node, SupplyFaction faction)
    {
        if (!node) return false;

        return faction switch
        {
            SupplyFaction.Ally => _allySuppliedNodes.Contains(node),
            SupplyFaction.Enemy => _enemySuppliedNodes.Contains(node),
            _ => false
        };
    }

    private void RecalculateSupplyConnections()
    {
        _allySuppliedNodes.Clear();
        _enemySuppliedNodes.Clear();

        FloodSupply(BattleManager.Instance.playerHeadquartersNode, SupplyFaction.Ally, _allySuppliedNodes);
        FloodSupply(BattleManager.Instance.enemyHeadquartersNode, SupplyFaction.Enemy, _enemySuppliedNodes);
    }
    private static void FloodSupply(Node headquarters, SupplyFaction faction, HashSet<Node> suppliedNodes)
    {
        if (!headquarters || headquarters.supplyFaction != faction) return;

        Queue<Node> queue = new();
        queue.Enqueue(headquarters);
        suppliedNodes.Add(headquarters);

        while (queue.Count > 0)
        {
            Node current = queue.Dequeue();

            foreach (Node neighbour in current.neighbours)
            {
                if (!neighbour ||
                    neighbour.supplyFaction != faction ||
                    suppliedNodes.Contains(neighbour))
                {
                    continue;
                }

                suppliedNodes.Add(neighbour);
                queue.Enqueue(neighbour);
            }
        }
    }
    public Node GetClosestNodeByWorldPosition(Vector3 position)
    {
        Node closest = null;
        float minDist = float.PositiveInfinity;

        foreach (Node n in _nodes)
        {
            float dist = Vector3.Distance(n.transform.position, position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = n;
            }
        }

        return closest;
    }

    public static void OccupyNode(Node node, Unit unit)
    {
        if (!node || !unit) return;
        
        node.AddUnit(unit);
    }

    public void UnoccupyNode(Node node)
    {
        if (!node || !node.HasUnits())
            return;
        
        node.RemoveUnit(node.units[^1]);
    }
    
    // BFS
    private readonly Queue<(Node node, int distance)> _queue = new();
    private readonly HashSet<Node> _visited = new HashSet<Node>();
    public List<Node> GetNodesInRange(Node startNode, int range)
    {
        List<Node> res = new List<Node>();
        _queue.Clear();
        _visited.Clear();

        // Safety
        if (!startNode || range <= 0) return res;
        
        _queue.Enqueue((startNode, 0));
        _visited.Add(startNode);

        while (_queue.Count > 0)
        {
            var (current, dist) = _queue.Dequeue();

            if (dist > 0) res.Add(current); 
            if (dist >= range) continue;
        
            foreach (Node neighbour in current.neighbours)
            {
                if (!neighbour || !_visited.Add(neighbour)) continue;
            
                // If the neighbor is occupied by an enemy, do NOT enqueue it for further path expansion.
                // We still added it to 'res' if dist > 0 for attack search
                if (neighbour.isOccupied && neighbour.units[0] && !neighbour.units[0].isPlayerUnit)
                {
                    continue; 
                }

                _queue.Enqueue((neighbour, dist + 1));
            }
        }

        return res;
    }

    public void MoveStackToNode(Node from, Node to)
    {
        if (!from || !to || from == to || !from.HasUnits())
            return;

        List<Unit> movingUnits = from.units.ToList();

        SupplyFaction movingFaction =
            movingUnits[0].isPlayerUnit
                ? SupplyFaction.Ally
                : SupplyFaction.Enemy;

        foreach (Unit unit in movingUnits)
        {
            from.RemoveUnit(unit);
        }

        // Clear any remaining units defensively (O(1) probably lol)
        foreach (Unit unit in to.units.ToList())
        {
            to.RemoveUnit(unit);
        }

        to.SetSupplyFaction(movingFaction);

        foreach (Unit unit in movingUnits)
        {
            to.AddUnit(unit);
            // unit.transform.position = to.transform.position; // Once we have unit representation, currently UI based
        }

        RefreshSupplyState();
    }
    
    #region Visual Highlights
    public void HighlightRangesForUnit(Unit unit)
    {
        if (!unit || !unit.currentNode) return;

        unit.currentNode.SetHighlightState(NodeHighlightState.Selected);

        var rangeNodes = GetNodesInRange(unit.currentNode, unit.MoveRange);

        // 1. Highlight Valid Movement Nodes (Unoccupied OR occupied by friendly Allies for stacking)
        foreach (Node node in rangeNodes)
        {
            if (node == unit.currentNode) continue;

            bool isEnemy = node.isOccupied && node.units[0] != null && !node.units[0].isPlayerUnit;
        
            if (!isEnemy)
            {
                node.SetHighlightState(NodeHighlightState.ReachableMove);
            }
        }

        // 2. Highlight Range 1 Attack Targets (Strictly adjacent neighbors to the unit's CURRENT position)
        foreach (Node neighbor in unit.currentNode.neighbours)
        {
            if (neighbor != null && neighbor.isOccupied && neighbor.units[0] != null && !neighbor.units[0].isPlayerUnit)
            {
                neighbor.SetHighlightState(NodeHighlightState.ReachableAttack);
            }
        }
    }
    
    public void ClearAllHighlights()
    {
        foreach (Node node in _nodes)
        {
            if (node)
            {
                node.SetHighlightState(NodeHighlightState.None);
            }
        }
    }
    #endregion
    
    public static Unit FindClosestPlayerUnit(Node startNode)
    {
        if (!startNode) return null;

        Queue<Node> queue = new Queue<Node>();
        HashSet<Node> visited = new HashSet<Node>();

        queue.Enqueue(startNode);
        visited.Add(startNode);

        while (queue.Count > 0)
        {
            Node current = queue.Dequeue();

            if (current.isOccupied && current.units[0] && current.units[0].isPlayerUnit)
            {
                return current.units[0];
            }

            // Expand to neighbors
            foreach (Node neighbor in current.neighbours)
            {
                if (neighbor && visited.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        return null; 
    }

    public Dictionary<Node, int> GetGraphDistances(Node startNode)
    {
        Dictionary<Node, int> distances = new();

        if (!startNode)
            return distances;

        Queue<Node> queue = new();

        distances[startNode] = 0;
        queue.Enqueue(startNode);

        while (queue.Count > 0)
        {
            Node current = queue.Dequeue();
            int currentDistance = distances[current];

            foreach (Node neighbour in current.neighbours)
            {
                if (!neighbour || distances.ContainsKey(neighbour))
                    continue;

                distances[neighbour] = currentDistance + 1;
                queue.Enqueue(neighbour);
            }
        }

        return distances;
    }
    
    private void ValidateAndRepairNeighbours()
    {
        if (_nodes == null)
            return;

        foreach (Node node in _nodes)
        {
            if (!node)
                continue;

            if (node.neighbours == null)
            {
                node.neighbours = new List<Node>();
            }

            // Remove null references, self-references and duplicates.
            node.neighbours.RemoveAll(neighbour =>
                !neighbour ||
                neighbour == node
            );

            node.neighbours = node.neighbours
                .Distinct()
                .ToList();
        }

        // Ensure every connection works in both directions.
        foreach (Node node in _nodes)
        {
            if (!node)
                continue;

            foreach (Node neighbour in node.neighbours)
            {
                if (!neighbour.neighbours.Contains(node))
                {
                    neighbour.neighbours.Add(node);

                    Debug.LogWarning(
                        $"Added missing reciprocal connection: " +
                        $"{neighbour.name} <-> {node.name}",
                        neighbour
                    );
                }
            }
        }
    }
}
