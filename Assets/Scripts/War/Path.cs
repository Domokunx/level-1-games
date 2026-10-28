using System;
using System.Collections.Generic;

public readonly struct Path : IEquatable<Path>
{
    public Node From { get; }
    public Node To { get; }

    public Path(Node from, Node to)
    {
        From = from;
        To = to;
    }

    // Undirected equality: Path(A, B) == Path(B, A)
    public bool Equals(Path other)
    {
        var comparer = EqualityComparer<Node>.Default;
        
        return (comparer.Equals(From, other.From) && comparer.Equals(To, other.To))
               || (comparer.Equals(From, other.To) && comparer.Equals(To, other.From));
    }

    public override bool Equals(object obj)
    {
        return obj is Path other && Equals(other);
    }

    // GetHashCode(A, B) == GetHashCode(B, A), commutative
    public override int GetHashCode()
    {
        int h1 = EqualityComparer<Node>.Default.GetHashCode(From);
        int h2 = EqualityComparer<Node>.Default.GetHashCode(To);
        
        return h1 + h2; 
    }

    public static bool operator ==(Path left, Path right) => left.Equals(right);
    public static bool operator !=(Path left, Path right) => !left.Equals(right);
}