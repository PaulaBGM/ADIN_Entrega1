using System.Collections.Generic;

namespace JPS4
{
    /// <summary>
    /// Managed equivalent of NativeMinHeap for non-job pathfinding classes.
    /// Behavior intentionally mirrors NativeMinHeap (same tie handling and decrease-key behavior).
    /// </summary>
    public sealed class ManagedMinHeap<TNode> where TNode : struct
    {
        private readonly List<TNode> items;
        private readonly List<int> priorities;
        private readonly Dictionary<TNode, int> positions; // node -> heapIndex
        private int count;

        public int Count => count;
        public int Capacity => items.Count;
        public bool IsEmpty => count == 0;

        private ManagedMinHeap(IEqualityComparer<TNode> comparer = null)
        {
            items = new List<TNode>();
            priorities = new List<int>();
            positions = comparer == null
                ? new Dictionary<TNode, int>()
                : new Dictionary<TNode, int>(comparer);
            count = 0;
        }

        public static ManagedMinHeap<TNode> Create(IEqualityComparer<TNode> comparer = null)
        {
            return new ManagedMinHeap<TNode>(comparer);
        }

        public bool Contains(TNode node)
        {
            return positions.ContainsKey(node);
        }

        public bool TryPush(TNode node, int priority)
        {
            if (positions.ContainsKey(node))
            {
                return false;
            }

            int i = count;
            items.Add(node);
            priorities.Add(priority);
            positions[node] = i;
            count++;

            BubbleUp(i);
            return true;
        }

        public bool TryDecreasePriority(TNode node, int newPriority)
        {
            if (!positions.TryGetValue(node, out int i))
            {
                return false;
            }

            if (newPriority >= priorities[i])
            {
                return false;
            }

            priorities[i] = newPriority;
            BubbleUp(i);
            return true;
        }

        public void PushOrDecrease(TNode node, int priority)
        {
            if (!positions.TryGetValue(node, out int i))
            {
                i = count;
                items.Add(node);
                priorities.Add(priority);
                positions[node] = i;
                count++;
                BubbleUp(i);
            }
            else if (priority < priorities[i])
            {
                priorities[i] = priority;
                BubbleUp(i);
            }
        }

        public bool TryPopMin(out TNode node, out int priority)
        {
            if (count == 0)
            {
                node = default;
                priority = -1;
                return false;
            }

            node = items[0];
            priority = priorities[0];
            positions.Remove(node);

            count--;
            if (count > 0)
            {
                items[0] = items[count];
                priorities[0] = priorities[count];
                positions[items[0]] = 0;
            }

            items.RemoveAt(count);
            priorities.RemoveAt(count);

            if (count > 0)
            {
                BubbleDown(0);
            }

            return true;
        }

        public bool TryPopMin(out TNode node)
        {
            return TryPopMin(out node, out _);
        }

        public bool TryPeek(out TNode node, out int priority)
        {
            if (count == 0)
            {
                node = default;
                priority = -1;
                return false;
            }

            node = items[0];
            priority = priorities[0];
            return true;
        }

        public void Clear()
        {
            items.Clear();
            priorities.Clear();
            positions.Clear();
            count = 0;
        }

        private void BubbleUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (priorities[parent] <= priorities[i])
                {
                    break;
                }

                Swap(i, parent);
                i = parent;
            }
        }

        private void BubbleDown(int i)
        {
            while (true)
            {
                int left = (i << 1) + 1;
                if (left >= count)
                {
                    break;
                }

                int right = left + 1;
                int smallest = (right < count && priorities[right] < priorities[left]) ? right : left;

                if (priorities[i] <= priorities[smallest])
                {
                    break;
                }

                Swap(i, smallest);
                i = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            TNode tmpItem = items[a];
            items[a] = items[b];
            items[b] = tmpItem;

            int tmpPriority = priorities[a];
            priorities[a] = priorities[b];
            priorities[b] = tmpPriority;

            positions[items[a]] = a;
            positions[items[b]] = b;
        }
    }
}
