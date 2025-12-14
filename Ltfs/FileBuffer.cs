using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Ltfs;

public class FileBuffer
{
    private readonly MemoryPool<byte> memoryPoolForSmallFiles = MemoryPool<byte>.Shared;
    
    public sealed class SmallFileBufferItem
    {
        public required IMemoryOwner<byte> Owner;
        public int Length = 0;
    }

    private readonly ConcurrentDictionary<string, Channel<SmallFileBufferItem>> buffers = new();
    private readonly ConcurrentDictionary<string, Task> producers = new();

    public Task AddFileAsync(string path, int chunkSize, SemaphoreSlim? prefetchSemaphore = null)
    {
        // Ensure channel exists
        var ch = buffers.GetOrAdd(path, (p) => Channel.CreateBounded<SmallFileBufferItem>(new BoundedChannelOptions(8)
        {
            SingleReader = true,
            SingleWriter = true
        }));

        // Ensure only one producer task per path and return existing producer if present
        return producers.GetOrAdd(path, (p) => Task.Run(async () =>
        {
            try
            {
                if (prefetchSemaphore != null) await prefetchSemaphore.WaitAsync();

                try
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: chunkSize, useAsync: true);
                    while (true)
                    {
                        var owner = memoryPoolForSmallFiles.Rent(chunkSize);
                        int read = await fs.ReadAsync(owner.Memory[..chunkSize]);
                        if (read == 0)
                        {
                            owner.Dispose();
                            break;
                        }

                        var item = new SmallFileBufferItem { Owner = owner, Length = read };
                        await ch.Writer.WriteAsync(item);
                    }
                }
                catch
                {
                    // swallow producer errors; ensure channel completes so consumers don't hang
                    Logger.Error($"Prefetch error: {path}");
                }
            }
            finally
            {
                try { ch.Writer.Complete(); } catch { }
                if (prefetchSemaphore != null) prefetchSemaphore.Release();
                // remove producer from dictionary so future prefetches can recreate if needed
                producers.TryRemove(path, out _);
            }
        }));
    }

    // Backwards-compatible wrapper
    public Task AddFile(string path, int chunkSize)
    {
        return AddFileAsync(path, chunkSize, null);
    }

    public ChannelReader<SmallFileBufferItem>? GetReader(string path)
    {
        if (path == null) return null;
        if (buffers.TryGetValue(path, out var ch)) return ch.Reader;
        return null;
    }

    // Remove the channel for `path` and dispose any buffered memory still held
    public async Task RemoveAsync(string path)
    {
        if (path == null) return;
        if (!buffers.TryRemove(path, out var ch)) return;

        // Drain any remaining items and dispose their memory owners
        try
        {
            while (await ch.Reader.WaitToReadAsync())
            {
                while (ch.Reader.TryRead(out var item))
                {
                    try { item.Owner.Dispose(); } catch { }
                }
            }
        }
        catch
        {
            // ignore drain errors
        }

        // Ensure any producer record is removed
        producers.TryRemove(path, out _);
    }
}
