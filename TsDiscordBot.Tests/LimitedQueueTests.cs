using System.Linq;
using TsDiscordBot.Discord.Utility;
using Xunit;

namespace TsDiscordBot.Tests;

public class LimitedQueueTests
{
    [Fact]
    public void EnqueueBeyondCapacity_DiscardsOldestItems()
    {
        var queue = new LimitedQueue<int>(3);
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Enqueue(3);
        queue.Enqueue(4);

        Assert.Equal(new[] { 2, 3, 4 }, queue.ToArray());
    }

    [Fact]
    public void EnqueueBeyondCapacity_DoesNotExceedCapacity()
    {
        var queue = new LimitedQueue<int>(3);
        for (int i = 0; i < 5; i++)
        {
            queue.Enqueue(i);
        }

        Assert.Equal(queue.Capacity, queue.Count);
    }
}
