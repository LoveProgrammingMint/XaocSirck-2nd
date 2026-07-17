using System;
using System.Collections.Generic;
using System.Text;

namespace Charwolf.AhoCorasick;

public sealed class TrieNode
{
    public readonly Dictionary<Byte, TrieNode> Children = [];
    public TrieNode? Failure;
    public readonly List<Int32> Output = [];
    public Int32 Index;
    public Boolean IsAllocated;
}

public static class TrieBuilder
{
    public static TrieNode Build(ReadOnlySpan<ReadOnlyMemory<Byte>> patterns)
    {
        TrieNode root = new();

        for (Int32 pid = 0; pid < patterns.Length; pid++)
        {
            ReadOnlySpan<Byte> pat = patterns[pid].Span;
            if (pat.Length == 0) continue;

            TrieNode node = root;
            foreach (Byte b in pat)
            {
                if (!node.Children.TryGetValue(b, out TrieNode? child))
                {
                    child = new();
                    node.Children[b] = child;
                }
                node = child;
            }
            node.Output.Add(pid);
        }

        return root;
    }
}
