using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Policies;
using Diaspora.Modules.Media.Domain.Uploads;
using Diaspora.Modules.Media;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Media;

/// <summary>
/// The chunked upload lifecycle.
/// <para>
/// The invariant under test throughout: <b>a session cannot produce an asset until every
/// declared part has arrived and the reassembled bytes hash to what the client promised.</b>
/// Both halves matter. Part-count alone accepts a file with a corrupt middle chunk; the digest
/// alone cannot be computed for a truncated one whose last part never came.
/// </para>
/// </summary>
public sealed class UploadSessionTests
{
    private static readonly UserId Member = UserId.New();
    private static readonly DateTimeOffset Now = new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    // ── Protocol selection ───────────────────────────────────────────────

    /// <summary>
    /// The server decides, not the client. A client choosing would need to know whether this
    /// deployment's provider supports presigned PUT — the exact provider knowledge the storage
    /// abstraction exists to keep out of callers.
    /// </summary>
    [Fact]
    public void The_server_picks_direct_for_a_small_file_and_chunked_for_a_large_one()
    {
        Start(ByteSize.FromMegabytes(2)).Protocol.Should().Be(UploadProtocol.Direct);
        Start(ByteSize.FromMegabytes(12)).Protocol.Should().Be(UploadProtocol.Chunked);
    }

    [Fact]
    public void A_chunked_session_declares_how_many_parts_to_expect()
    {
        var session = Start(ByteSize.FromMegabytes(12));

        // 12 MB at the default 5 MB part size: three parts, the last one short.
        session.TotalChunks.Should().Be(3);
        session.ChunkSize.Should().Be(MediaLimits.ChunkSize);
    }

    // ── Refusals before any bytes arrive ─────────────────────────────────

    [Fact]
    public void An_oversized_declaration_is_refused_at_initialisation()
    {
        var session = UploadSession.Start(
            Member,
            MediaKind.Image,
            MediaContentType.Create("image/jpeg"),
            SafeFileName.Create("huge.jpg"),
            ByteSize.FromMegabytes(200),
            null,
            "filesystem",
            "diaspora-media",
            Now);

        session.IsFailure.Should().BeTrue();
        session.Error.Code.Should().Be(MediaErrorCodes.UploadTooLarge);
    }

    [Fact]
    public void A_disallowed_type_is_refused_at_initialisation()
    {
        var session = UploadSession.Start(
            Member,
            MediaKind.Image,
            MediaContentType.Create("application/x-msdownload"),
            SafeFileName.Create("thing.exe"),
            ByteSize.FromMegabytes(1),
            null,
            "filesystem",
            "diaspora-media",
            Now);

        session.IsFailure.Should().BeTrue();
        session.Error.Code.Should().Be(MediaErrorCodes.ContentTypeNotAllowed);
    }

    // ── Receiving parts ──────────────────────────────────────────────────

    [Fact]
    public void Progress_is_reported_in_the_units_the_clients_progress_bar_uses()
    {
        var session = Start(ByteSize.FromMegabytes(10));

        session.ProgressPercent.Should().Be(0);

        Receive(session, 1, ByteSize.FromMegabytes(5));

        session.ProgressPercent.Should().Be(50);
        session.Status.Should().Be(UploadSessionStatus.Uploading);
        session.ReceivedChunkNumbers.Should().Equal(1);
    }

    /// <summary>
    /// What makes the client's retry safe: a part whose response was lost on the way back gets
    /// sent again, and the second attempt must not be a conflict.
    /// </summary>
    [Fact]
    public void Re_sending_an_identical_part_is_a_no_op()
    {
        var session = Start(ByteSize.FromMegabytes(10));

        var hash = ContentHash.Compute("part-one"u8);

        session.ReceiveChunk(1, ByteSize.FromMegabytes(5), hash, Key(1), Now)
            .IsSuccess.Should().BeTrue();

        session.ReceiveChunk(1, ByteSize.FromMegabytes(5), hash, Key(1), Now)
            .IsSuccess.Should().BeTrue();

        session.Chunks.Should().ContainSingle();
    }

    /// <summary>Same part number, different bytes, is a genuine conflict.</summary>
    [Fact]
    public void Re_sending_a_part_with_different_bytes_is_refused()
    {
        var session = Start(ByteSize.FromMegabytes(10));

        session.ReceiveChunk(
            1, ByteSize.FromMegabytes(5), ContentHash.Compute("a"u8), Key(1), Now);

        var conflict = session.ReceiveChunk(
            1, ByteSize.FromMegabytes(5), ContentHash.Compute("b"u8), Key(1), Now);

        conflict.IsFailure.Should().BeTrue();
        conflict.Error.Code.Should().Be(MediaErrorCodes.UploadChunkChecksumMismatch);
    }

    [Fact]
    public void A_part_number_outside_the_declared_range_is_refused()
    {
        var session = Start(ByteSize.FromMegabytes(10));

        session.ReceiveChunk(0, ByteSize.FromMegabytes(5), Hash(), Key(0), Now)
            .IsFailure.Should().BeTrue();

        session.ReceiveChunk(99, ByteSize.FromMegabytes(5), Hash(), Key(99), Now)
            .IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// Without a floor, a client can open a session and send ten thousand one-byte parts, each
    /// of which is a database row and a stored object.
    /// </summary>
    [Fact]
    public void A_tiny_non_final_part_is_refused()
    {
        var session = Start(ByteSize.FromMegabytes(10));

        var refused = session.ReceiveChunk(
            1, ByteSize.FromBytes(64), Hash(), Key(1), Now);

        refused.IsFailure.Should().BeTrue();
        refused.Error.Code.Should().Be(MediaErrorCodes.UploadChunkSizeInvalid);
    }

    /// <summary>The last part is legitimately short.</summary>
    [Fact]
    public void The_final_part_may_be_short()
    {
        var session = Start(ByteSize.FromMegabytes(12));

        Receive(session, 1, ByteSize.FromMegabytes(5));
        Receive(session, 2, ByteSize.FromMegabytes(5));

        session.ReceiveChunk(3, ByteSize.FromMegabytes(2), Hash(), Key(3), Now)
            .IsSuccess.Should().BeTrue();

        session.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Parts_that_would_exceed_the_declared_size_are_refused()
    {
        var session = Start(ByteSize.FromMegabytes(12));

        Receive(session, 1, ByteSize.FromMegabytes(5));
        Receive(session, 2, ByteSize.FromMegabytes(5));

        session.ReceiveChunk(3, ByteSize.FromMegabytes(5), Hash(), Key(3), Now)
            .IsFailure.Should().BeTrue();
    }

    // ── Pause and resume ─────────────────────────────────────────────────

    /// <summary>
    /// The state the client's reducer already handles, and the reason uploads are sessions
    /// rather than a single POST — a one-shot upload can never reach it.
    /// </summary>
    [Fact]
    public void An_upload_pauses_and_resumes_without_losing_its_parts()
    {
        var session = Start(ByteSize.FromMegabytes(10));

        Receive(session, 1, ByteSize.FromMegabytes(5));

        session.Pause().IsSuccess.Should().BeTrue();
        session.Status.Should().Be(UploadSessionStatus.Paused);

        // The parts already received survive the pause. That is the whole point: a member who
        // pauses on a train and resumes at home re-sends nothing.
        session.Chunks.Should().ContainSingle();
        session.ProgressPercent.Should().Be(50);

        session.Resume().IsSuccess.Should().BeTrue();
        session.Status.Should().Be(UploadSessionStatus.Uploading);

        session.ReceiveChunk(2, ByteSize.FromMegabytes(5), Hash(), Key(2), Now)
            .IsSuccess.Should().BeTrue();

        session.IsComplete.Should().BeTrue();
    }

    // ── Completion ───────────────────────────────────────────────────────

    [Fact]
    public void An_incomplete_session_cannot_be_completed()
    {
        var session = Start(ByteSize.FromMegabytes(10));

        Receive(session, 1, ByteSize.FromMegabytes(5));

        var completed = session.Complete(
            MediaId.New(), Hash(), ByteSize.FromMegabytes(10));

        completed.IsFailure.Should().BeTrue();
        completed.Error.Code.Should().Be(MediaErrorCodes.UploadSessionIncomplete);
    }

    /// <summary>
    /// The check part-count alone cannot make: every part arrived, and the reassembled bytes
    /// are not what the client promised. Storing this would be storing a file that looks fine
    /// until somebody opens it.
    /// </summary>
    [Fact]
    public void A_digest_mismatch_fails_the_session_even_when_every_part_arrived()
    {
        var declared = ContentHash.Compute("the-real-file"u8);
        var session = Start(ByteSize.FromMegabytes(10), declared);

        Receive(session, 1, ByteSize.FromMegabytes(5));
        Receive(session, 2, ByteSize.FromMegabytes(5));

        var completed = session.Complete(
            MediaId.New(), ContentHash.Compute("something-else"u8), ByteSize.FromMegabytes(10));

        completed.IsFailure.Should().BeTrue();
        session.Status.Should().Be(UploadSessionStatus.Failed);
    }

    [Fact]
    public void A_size_mismatch_fails_the_session()
    {
        var session = Start(ByteSize.FromMegabytes(10));

        Receive(session, 1, ByteSize.FromMegabytes(5));
        Receive(session, 2, ByteSize.FromMegabytes(5));

        session.Complete(MediaId.New(), Hash(), ByteSize.FromMegabytes(9))
            .IsFailure.Should().BeTrue();

        session.Status.Should().Be(UploadSessionStatus.Failed);
    }

    [Fact]
    public void A_complete_and_matching_session_produces_an_asset()
    {
        var declared = ContentHash.Compute("file"u8);
        var session = Start(ByteSize.FromMegabytes(10), declared);

        Receive(session, 1, ByteSize.FromMegabytes(5));
        Receive(session, 2, ByteSize.FromMegabytes(5));

        var mediaId = MediaId.New();

        session.Complete(mediaId, declared, ByteSize.FromMegabytes(10))
            .IsSuccess.Should().BeTrue();

        session.Status.Should().Be(UploadSessionStatus.Completed);
        session.MediaId.Should().Be(mediaId);
        session.IsOpen.Should().BeFalse();
    }

    // ── Expiry ───────────────────────────────────────────────────────────

    [Fact]
    public void A_part_arriving_after_expiry_expires_the_session()
    {
        var session = Start(ByteSize.FromMegabytes(10));

        var late = session.ReceiveChunk(
            1, ByteSize.FromMegabytes(5), Hash(), Key(1), Now.AddDays(2));

        late.IsFailure.Should().BeTrue();
        late.Error.Code.Should().Be(MediaErrorCodes.UploadSessionExpired);
        session.Status.Should().Be(UploadSessionStatus.Expired);
    }

    /// <summary>Ordering for reassembly must not depend on the order parts arrived in.</summary>
    [Fact]
    public void Parts_reassemble_in_number_order_regardless_of_arrival_order()
    {
        var session = Start(ByteSize.FromMegabytes(15));

        Receive(session, 3, ByteSize.FromMegabytes(5));
        Receive(session, 1, ByteSize.FromMegabytes(5));
        Receive(session, 2, ByteSize.FromMegabytes(5));

        session.OrderedChunkKeys().Select(key => key.Value)
            .Should().BeInAscendingOrder();

        session.ReceivedChunkNumbers.Should().Equal(1, 2, 3);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static UploadSession Start(ByteSize size, ContentHash? declaredHash = null) =>
        UploadSession.Start(
            Member,
            MediaKind.Image,
            MediaContentType.Create("image/jpeg"),
            SafeFileName.Create("photo.jpg"),
            size,
            declaredHash,
            "filesystem",
            "diaspora-media",
            Now).Value;

    private static void Receive(UploadSession session, int number, ByteSize size) =>
        session.ReceiveChunk(number, size, Hash(), Key(number), Now)
            .IsSuccess.Should().BeTrue();

    private static ContentHash Hash() => ContentHash.Compute(Guid.NewGuid().ToByteArray());

    /// <summary>
    /// A fixed session id, so the generated keys sort by part number. Reassembly order comes
    /// from the part number rather than the key, but a test that used random ids could not
    /// tell the difference.
    /// </summary>
    private static readonly Guid StagingSession = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static StorageKey Key(int number) =>
        StorageKey.ForUploadPart(StagingSession, Math.Max(0, number));
}
