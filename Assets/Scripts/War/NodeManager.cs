using System.Collections.Generic;
using UnityEngine;

public class NodeManager : MonoBehaviour
{
    public static NodeManager Instance;

    private Node[] _nodes;
    private HashSet<Path> _paths = new HashSet<Path>();
    private Dictionary<Path, LineRenderer> _pathLineRenderers = new Dictionary<Path, LineRenderer>();
    
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

        // Init Map
        FindALlNodes();
        GenerateAllPaths();
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

        UpdateAllPathColors();
    }

    public void UpdateAllPathColors()
    {
        foreach (var kvp in _pathLineRenderers)
        {
            Path p = kvp.Key;
            LineRenderer line = kvp.Value;

            if (!line || !p.From || !p.To) continue;

            Color targetColor = neutralLineColor;

            // Rule 1: Both connected nodes are Ally
            if (p.From.supplyFaction == SupplyFaction.Ally && p.To.supplyFaction == SupplyFaction.Ally)
            {
                targetColor = allyLineColor;
            }
            // Rule 2: Both connected nodes are Enemy
            else if (p.From.supplyFaction == SupplyFaction.Enemy && p.To.supplyFaction == SupplyFaction.Enemy)
            {
                targetColor = enemyLineColor;
            }
            // Rule 3: Neutral or mixed factions -> Gray (neutralLineColor) (DEFAULT)
            {
            }

            // Apply color to the line renderer (supporting both start/end color settings)
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
        if (node == null || unit == null) return;
        
        node.OccupyNode(unit);
    }

    public void UnoccupyNode(Node node)
    {
        if (node == null) return;   
        
        node.UnoccupyNode();
    }

    // BFS
    // Updated GetNodesInRange in NodeManager.cs
    public List<Node> GetNodesInRange(Node startNode, int range)
    {
        List<Node> res = new List<Node>();
        Queue<(Node node, int distance)> queue = new();
        HashSet<Node> visited = new HashSet<Node>();

        queue.Enqueue((startNode, 0));
        visited.Add(startNode);

        while (queue.Count > 0)
        {
            var (current, dist) = queue.Dequeue();

            if (dist > 0) res.Add(current); 
            if (dist >= range) continue;
        
            foreach (Node neighbour in current.neighbours)
            {
                if (!neighbour || !visited.Add(neighbour)) continue;
            
                // If the neighbor is occupied by an enemy, do NOT enqueue it for further path expansion.
                // We still added it to 'res' if dist > 0 for attack search
                if (neighbour.isOccupied && neighbour.currentUnit && !neighbour.currentUnit.isPlayerUnit)
                {
                    continue; 
                }

                queue.Enqueue((neighbour, dist + 1));
            }
        }

        return res;
    }
    
    #region Visual Highlights
    public void HighlightNodes(List<Node> nodes, bool shouldHighlight)
    {
        foreach (Node n in nodes)
        {
            // Example: toggle a visual mesh renderer or ring child object
            var renderer = n.GetComponent<SpriteRenderer>();
            if (renderer)
            {
                renderer.material.color = shouldHighlight 
                    ? n.isOccupied 
                        ? (n.isOccupiedByAlly ? Color.blue : Color.red) 
                        : Color.white 
                    : Color.white;
                        
            }
        }
    }
    
    public void HighlightRangesForUnit(Unit unit)
    {
        if (!unit || !unit.currentNode) return;

        unit.currentNode.SetHighlightState(NodeHighlightState.Selected);

        var rangeNodes = GetNodesInRange(unit.currentNode, unit.MoveRange);

        // 1. Highlight Valid Movement Nodes (Unoccupied OR occupied by friendly Allies for stacking)
        foreach (Node node in rangeNodes)
        {
            if (node == unit.currentNode) continue;

            bool isEnemy = node.isOccupied && node.currentUnit != null && !node.currentUnit.isPlayerUnit;
        
            if (!isEnemy)
            {
                node.SetHighlightState(NodeHighlightState.ReachableMove);
            }
        }

        // 2. Highlight Range 1 Attack Targets (Strictly adjacent neighbors to the unit's CURRENT position)
        foreach (Node neighbor in unit.currentNode.neighbours)
        {
            if (neighbor != null && neighbor.isOccupied && neighbor.currentUnit != null && !neighbor.currentUnit.isPlayerUnit)
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

            if (current.isOccupied && current.currentUnit && current.currentUnit.isPlayerUnit)
            {
                return current.currentUnit;
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

    public Node FindVulnerableSupplyNode(Node startNode)
    {
        if (!startNode) return null;

        Queue<Node> queue = new Queue<Node>();
        HashSet<Node> visited = new HashSet<Node>();

        queue.Enqueue(startNode);
        visited.Add(startNode);

        while (queue.Count > 0)
        {
            Node current = queue.Dequeue();

            // Check if this node is an unoccupied, player-controlled supply point
            if (current.supplyFaction == SupplyFaction.Ally && !current.isOccupied)
            {
                return current;
            }

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
}
