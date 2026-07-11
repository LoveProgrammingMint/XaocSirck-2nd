using System;
using System.Collections.Generic;
using System.Text;

namespace Charwolf.AhoCorasick;

internal sealed class DoubleArrayResult
{
    public Int16[] Base = null!;
    public Int16[] Check = null!;
    public Int16[] Fail = null!;
    public Int32[] OutputHead = null!;
    public Int32[] OutputNext = null!;
    public Int32[] OutputPattern = null!;
    public Int32[] PatternLength = null!;
}

internal class DoubleArrayBuilder
{

    private readonly List<Int16> _base = [ 0 ];
    private readonly List<Int16> _check = [ 0 ];
    private readonly List<Boolean> _used = [ true ];
    private readonly Int32 _nextFree = 1;

    public static DoubleArrayResult Build(TrieNode root, Int32 patternCount)
    {
        DoubleArrayBuilder builder = new();
        Queue<TrieNode> queue = new();

        root.Index = 0;
        root.IsAllocated = true;
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            TrieNode node = queue.Dequeue();
            builder.AllocateChildren(node);

            foreach (TrieNode child in node.Children.Values)
            {
                if (!child.IsAllocated)
                {
                    queue.Enqueue(child);
                }
            }
        }

        DoubleArrayBuilder.BuildFailureLinks(root);

        (Int32[] Head, Int32[] Next, Int32[] Pattern)= builder.BuildOutputTable(root);

        Int32 size = builder._base.Count;
        DoubleArrayResult result = new()
        {
            Base = [.. builder._base.Take(size)],
            Check = [.. builder._check.Take(size)], 
            Fail = new Int16[size],
            OutputHead = Head,
            OutputNext = Next,
            OutputPattern = Pattern,
            PatternLength = new Int32[patternCount]
        };

        queue.Clear();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            TrieNode node = queue.Dequeue();
            result.Fail[node.Index] = (Int16)(node.Failure?.Index ?? 0);
            foreach (TrieNode child in node.Children.Values)
                queue.Enqueue(child);
        }

        return result;
    }

    private void AllocateChildren(TrieNode parent)
    {
        if (parent.Children.Count == 0) return;

        Byte[] keys = [.. parent.Children.Keys];
        Int32 firstKey = keys[0];

        Int32 baseValue = FindValidBase(firstKey, keys);
        _base[parent.Index] = (Int16)baseValue;

        foreach (Byte b in keys)
        {
            Int32 pos = baseValue + b;
            EnsureCapacity(pos);

            TrieNode child = parent.Children[b];
            child.Index = pos;
            child.IsAllocated = true;
            _check[pos] = (Int16)parent.Index;
            _used[pos] = true;
        }
    }

    private Int32 FindValidBase(Int32 firstKey, Byte[] keys)
    {
        Int32 candidate = Math.Max(1, _nextFree - firstKey);

        while (true)
        {
            Boolean valid = true;
            foreach (Byte b in keys)
            {
                Int32 pos = candidate + b;
                if (pos < _used.Count && _used[pos])
                {
                    valid = false;
                    break;
                }
            }

            if (valid) return candidate;
            candidate++;
        }
    }

    private void EnsureCapacity(Int32 index)
    {
        while (_base.Count <= index)
        {
            _base.Add(0);
            _check.Add(0);
            _used.Add(false);
        }
    }

    private static void BuildFailureLinks(TrieNode root)
    {
        Queue<TrieNode> queue = new();
        root.Failure = root;

        foreach (TrieNode child in root.Children.Values)
        {
            child.Failure = root;
            queue.Enqueue(child);
        }

        while (queue.Count > 0)
        {
            TrieNode node = queue.Dequeue();

            foreach ((Byte b, TrieNode child) in node.Children)
            {
                TrieNode fail = node.Failure!;
                while (fail != root && !fail.Children.ContainsKey(b))
                    fail = fail.Failure!;

                if (fail.Children.TryGetValue(b, out TrieNode? failChild) && failChild != child)
                    child.Failure = failChild;
                else
                    child.Failure = root;

                child.Output.AddRange(child.Failure.Output);
                queue.Enqueue(child);
            }
        }
    }

    private (Int32[] Head, Int32[] Next, Int32[] Pattern) BuildOutputTable(TrieNode root)
    {
        List<Int32> head = [ 1 ];
        List<Int32> next = [];
        List<Int32> pattern = [];

        Queue<TrieNode> queue = new();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            TrieNode node = queue.Dequeue();
            Int32 headIdx = -1;

            for (Int32 i = node.Output.Count - 1; i >= 0; i--)
            {
                Int32 pid = node.Output[i];
                next.Add(headIdx);
                pattern.Add(pid);
                headIdx = next.Count - 1;
            }

            while (head.Count <= node.Index) head.Add(-1);
            head[node.Index] = headIdx;

            foreach (TrieNode child in node.Children.Values)
                queue.Enqueue(child);
        }

        while (head.Count < _base.Count) head.Add(-1);

        return (head.ToArray(), next.ToArray(), pattern.ToArray());
    }
}
