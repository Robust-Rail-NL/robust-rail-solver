// Covers Deque<T>.Replace, which puts several nodes in the place of one. It is
// what models splitting a train where it stands: the parts take over exactly the
// stretch the whole train held, so a train in the middle of a track does not have
// its parts moved to the end. Adding them would do that, and it is only right if
// the train drove out and came back.
namespace Tests.Deque;

using ServiceSiteScheduling;
using ServiceSiteScheduling.Parking;

public class DequeReplaceTests
{
    // The deque is generic over its own node type; this is the smallest thing
    // satisfying that constraint, so these tests need no parking model at all.
    private sealed class Node(string name) : DequeNode<Node>
    {
        public readonly string Name = name;

        public override string ToString() => this.Name;
    }

    private static Deque<Node> Of(params Node[] nodes)
    {
        var deque = new Deque<Node>();
        // Adding at side B appends, so the array reads A-to-B.
        foreach (var node in nodes)
            deque.Add(node, Side.B);
        return deque;
    }

    private static string A2B(Deque<Node> deque) => string.Join(",", deque.A2B);

    private static string B2A(Deque<Node> deque) => string.Join(",", deque.B2A);

    [Fact]
    public void ReplacingAnInteriorNode_KeepsBothNeighbours()
    {
        Node first = new("first"),
            middle = new("middle"),
            last = new("last");
        var deque = Of(first, middle, last);

        deque.Replace(middle, [new Node("x"), new Node("y")]);

        Assert.Equal("first,x,y,last", A2B(deque));
        // Walking back must agree, or a later departure would follow dangling links.
        Assert.Equal("last,y,x,first", B2A(deque));
        Assert.Equal(4, deque.Count);
    }

    [Fact]
    public void ReplacingTheHeadAtA_MovesTheAEndToTheFirstPart()
    {
        Node head = new("head"),
            other = new("other");
        var deque = Of(head, other);

        deque.Replace(head, [new Node("x"), new Node("y")]);

        Assert.Equal("x,y,other", A2B(deque));
        Assert.Equal("x", deque.Head(Side.A).Name);
        Assert.Equal("other", deque.Head(Side.B).Name);
    }

    [Fact]
    public void ReplacingTheHeadAtB_MovesTheBEndToTheLastPart()
    {
        Node other = new("other"),
            head = new("head");
        var deque = Of(other, head);

        deque.Replace(head, [new Node("x"), new Node("y")]);

        Assert.Equal("other,x,y", A2B(deque));
        Assert.Equal("other", deque.Head(Side.A).Name);
        Assert.Equal("y", deque.Head(Side.B).Name);
    }

    [Fact]
    public void ReplacingTheOnlyNode_LeavesBothEndsOnTheParts()
    {
        Node only = new("only");
        var deque = Of(only);

        deque.Replace(only, [new Node("x"), new Node("y")]);

        Assert.Equal("x,y", A2B(deque));
        Assert.Equal("x", deque.Head(Side.A).Name);
        Assert.Equal("y", deque.Head(Side.B).Name);
        Assert.Equal(2, deque.Count);
    }

    [Fact]
    public void ReplacingWithOneNode_IsASubstitutionAndKeepsTheCount()
    {
        Node first = new("first"),
            middle = new("middle"),
            last = new("last");
        var deque = Of(first, middle, last);

        deque.Replace(middle, [new Node("x")]);

        Assert.Equal("first,x,last", A2B(deque));
        Assert.Equal(3, deque.Count);
    }

    [Fact]
    public void TheReplacedNode_IsDetached()
    {
        Node first = new("first"),
            middle = new("middle"),
            last = new("last");
        var deque = Of(first, middle, last);

        deque.Replace(middle, [new Node("x")]);

        // Left linked, it would look like a member of this deque and a later
        // Remove would rewrite links inside it — the shape of #11.
        Assert.Null(middle.A);
        Assert.Null(middle.B);
    }

    [Fact]
    public void ReplacingWithNothing_IsRejected()
    {
        Node only = new("only");
        var deque = Of(only);

        // Emptying the deque is Remove's job, and silently accepting an empty
        // list here would drop a train off the track without saying so.
        Assert.Throws<ArgumentException>(() => deque.Replace(only, []));
    }

    [Fact]
    public void ReplacingANodeFromAnotherDeque_IsRejected()
    {
        var deque = Of(new Node("mine"));
        var other = Of(new Node("theirs"));

        Assert.Throws<ArgumentException>(() => deque.Replace(other.Head(Side.A), [new Node("x")]));
    }

    [Fact]
    public void ReplacingInAnEmptyDeque_IsRejected()
    {
        var deque = new Deque<Node>();

        Assert.Throws<ArgumentException>(() => deque.Replace(new Node("x"), [new Node("y")]));
    }
}

public class DequeRemoveHeadTests
{
    private sealed class Node(string name) : DequeNode<Node>
    {
        public readonly string Name = name;

        public override string ToString() => this.Name;
    }

    private static Deque<Node> Of(params Node[] nodes)
    {
        var deque = new Deque<Node>();
        foreach (var node in nodes)
            deque.Add(node, Side.B);
        return deque;
    }

    [Fact]
    public void RemovingTheHeadAtA_TakesTheAEndAndDoesNotThrow()
    {
        var deque = Of(new Node("a"), new Node("b"));

        // It used to throw here whatever happened: the "wrong side" throw at the
        // end of the method was reached on the success path too, so a caller got
        // the removal and an exception blaming its perfectly good argument.
        deque.RemoveHead(Side.A);

        Assert.Equal("b", string.Join(",", deque.A2B));
        Assert.Equal(1, deque.Count);
    }

    [Fact]
    public void RemovingTheHeadAtB_TakesTheBEnd()
    {
        var deque = Of(new Node("a"), new Node("b"));

        deque.RemoveHead(Side.B);

        Assert.Equal("a", string.Join(",", deque.A2B));
        Assert.Equal(1, deque.Count);
    }

    [Fact]
    public void RemovingTheHeadOfANonSide_IsRejected()
    {
        var deque = Of(new Node("a"));

        // A deque has two ends, so "both" and "neither" name no head to remove.
        Assert.Throws<ArgumentException>(() => deque.RemoveHead(Side.Both));
        Assert.Throws<ArgumentException>(() => deque.RemoveHead(Side.None));
    }

    [Fact]
    public void RemovingTheHeadOfAnEmptyDeque_IsRejected()
    {
        var deque = new Deque<Node>();

        Assert.Throws<ArgumentException>(() => deque.RemoveHead(Side.A));
    }
}
