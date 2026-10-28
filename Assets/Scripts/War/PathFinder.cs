using System.Collections.Generic;
using UnityEngine;

public class PathFinder : MonoBehaviour
{
    public static PathFinder Instance;
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

    public List<Node> FindPath(Node start, Node end)
    {
        if (start == null || end == null) return null;
        
        // Init
        NodeManager.Instance.ResetNodeCosts();
        start.gCost = 0f;
        start.hCost = GetDistance(start, end);
        
        List<Node> frontier = new List<Node>();
        HashSet<Node> visited = new HashSet<Node>();
        
        frontier.Add(start);

        // A* search
        while (frontier.Count > 0)
        {
            Node current = frontier[0];
            for (int i = 1; i < frontier.Count; i++)
            {
                // if cost is lower, save
                // if heuristic is correct, should get closer to end 
                if (frontier[i].FinalCost < current.FinalCost)
                {
                    current = frontier[i];
                }
            }
            
            frontier.Remove(current);
            visited.Add(current);

            if (current == end)
            {
                return RetracePath(start, end);
            }
            
            // Check neighbours
            foreach (Node neighbour in current.neighbours)
            {
                if (neighbour == null || visited.Contains(neighbour)) continue;
                
                float newMovementCostToNeighbour = current.gCost + GetDistance(current, neighbour);

                if (newMovementCostToNeighbour < neighbour.gCost || !frontier.Contains(neighbour))
                {
                    neighbour.gCost = newMovementCostToNeighbour;
                    neighbour.hCost = GetDistance(neighbour, end);
                    neighbour.parent = current;

                    // Check neighbour
                    if (!frontier.Contains(neighbour))
                    {
                        frontier.Add(neighbour);
                    }
                }
            }
        }

        return null;
    }

    private List<Node> RetracePath(Node start, Node end)
    {
        List<Node> path = new List<Node>();
        Node current = end;

        while (current != start)
        {
            path.Add(current);
            current = current.parent;
        }

        path.Add(start);
        path.Reverse();
        return path;
    }

    private float GetDistance(Node start, Node end)
    {
        return Vector3.Distance(start.transform.position, end.transform.position);
    }
}
