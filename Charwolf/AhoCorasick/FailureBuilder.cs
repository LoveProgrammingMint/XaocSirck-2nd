using System;
using System.Collections.Generic;
using System.Text;

namespace Charwolf.AhoCorasick;

internal static class FailureBuilder
{
    public static void Build(TrieNode root)
    {
        Queue<TrieNode> queue = new();

        foreach (TrieNode child in root.Children.Values)
        {
            child.Failure = root;
            queue.Enqueue(child);
        }

        while (queue.Count > 0)
        {
            TrieNode current = queue.Dequeue();

            foreach ((Byte input, TrieNode child) in current.Children)
            {
                TrieNode fallback = current.Failure!;

                while (fallback != root && !fallback.Children.ContainsKey(input))
                    fallback = fallback.Failure!;

                if (fallback.Children.TryGetValue(input, out TrieNode? target) && target != child)
                    child.Failure = target;
                else
                    child.Failure = root;

                child.Output.AddRange(child.Failure.Output);

                queue.Enqueue(child);
            }
        }
    }
}

