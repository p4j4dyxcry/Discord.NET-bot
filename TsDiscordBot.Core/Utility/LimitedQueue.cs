using System.Collections;

namespace TsDiscordBot.Core.Utility;

public class LimitedQueue<T> : IEnumerable<T>
{
    private readonly Queue<T> _queue;

    public int Count => _queue.Count;

    public int Capacity { get; }

    public LimitedQueue(int capacity)
    {
        Capacity = capacity;
        _queue = new Queue<T>(capacity);
    }

    public void Enqueue(T item)
    {
        _queue.Enqueue(item);

        if (Count > Capacity)
        {
            Dequeue();
        }
    }

    public T Dequeue() => _queue.Dequeue();

    public T Peek() => _queue.Peek();

    public void Clear() => _queue.Clear();

    public IEnumerator<T> GetEnumerator() => _queue.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _queue.GetEnumerator();
}