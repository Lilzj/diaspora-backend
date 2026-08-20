namespace Diaspora.Modules.Media.Infrastructure.Storage;

/// <summary>
/// A read-only window over another stream, used to serve an HTTP range without copying.
/// <para>
/// Exists because <see cref="FileStream"/> has no "read exactly this many bytes from here"
/// mode, and the alternatives are both bad: buffering the range into memory costs a copy of
/// every byte served, and letting the caller over-read hands a client bytes beyond the range
/// it asked for. Serving a video means many concurrent ranges over large files, which is
/// exactly where a per-request copy hurts most.
/// </para>
/// <para>
/// Disposing this disposes the underlying stream. Every construction site is a
/// caller-returned response body, and the alternative is a leaked file handle per range
/// request.
/// </para>
/// </summary>
internal sealed class BoundedReadStream : Stream
{
    private readonly Stream _inner;
    private readonly long _length;
    private long _position;

    public BoundedReadStream(Stream inner, long length)
    {
        ArgumentNullException.ThrowIfNull(inner);

        _inner = inner;
        _length = length < 0 ? 0 : length;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        var allowed = (int)Math.Min(count, _length - _position);

        if (allowed <= 0)
        {
            return 0;
        }

        var read = _inner.Read(buffer, offset, allowed);
        _position += read;

        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var allowed = (int)Math.Min(buffer.Length, _length - _position);

        if (allowed <= 0)
        {
            return 0;
        }

        var read = await _inner.ReadAsync(buffer[..allowed], cancellationToken);
        _position += read;

        return read;
    }

    public override void Flush() => _inner.Flush();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync();
        await base.DisposeAsync();
    }
}
