using System.Text.Json;
using System.Text.Json.Serialization;
using Diaspora.Modules.Guides.Application.Services.Snapshots;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// The one place a guide's structure becomes JSON and comes back.
/// <para>
/// <b>Deterministic by construction.</b> The options instance is fixed and shared, camelCase,
/// unindented; property order is pinned by attributes on the snapshot records rather than
/// left to reflection (the B1.2 property-order trap). Two serializations of the same content
/// are byte-identical, which is what lets a test compare snapshots and an operator diff two
/// versions in a database console.
/// </para>
/// <para>
/// Static, because the shape is a contract, not a preference: a second serializer with
/// different options would write versions the first cannot faithfully compare.
/// </para>
/// </summary>
public static class GuideSnapshotSerializer
{
    /// <summary>Fixed and shared. Never vary these per call site.</summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>Snapshots the guide's current structure. The publish path calls this.</summary>
    public static string Serialize(Guide guide)
    {
        ArgumentNullException.ThrowIfNull(guide);

        var snapshot = new GuideSnapshot
        {
            Sections = guide.Sections
                .Select(section => new GuideSectionSnapshot
                {
                    SectionId = section.Id,
                    Title = section.Title,
                    Order = section.Order,
                    Blocks = section.Blocks
                        .Select(block => new GuideBlockSnapshot
                        {
                            BlockId = block.Id,
                            Kind = block.Kind.ToString(),
                            Order = block.Order,
                            Content = block.Content,
                            MediaId = block.MediaId,
                            Metadata = block.Metadata
                        })
                        .ToList()
                })
                .ToList()
        };

        return JsonSerializer.Serialize(snapshot, Options);
    }

    /// <summary>Serializes an already-built snapshot — the auto-save path.</summary>
    public static string Serialize(GuideSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return JsonSerializer.Serialize(snapshot, Options);
    }

    /// <summary>
    /// Reads a stored snapshot back. Failure is a <see cref="GuideErrors.SnapshotInvalid"/>
    /// result, never an exception — a corrupt row must degrade to an error response, not a 500.
    /// </summary>
    public static Result<GuideSnapshot> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result.Failure<GuideSnapshot>(GuideErrors.SnapshotInvalid);
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<GuideSnapshot>(json, Options);

            return snapshot is null
                ? Result.Failure<GuideSnapshot>(GuideErrors.SnapshotInvalid)
                : Result.Success(snapshot);
        }
        catch (JsonException)
        {
            return Result.Failure<GuideSnapshot>(GuideErrors.SnapshotInvalid);
        }
    }

    /// <summary>
    /// Turns a snapshot back into the drafts <c>Guide.RestoreVersion</c> takes — the restore
    /// path. An unknown block kind fails the whole restore rather than silently dropping the
    /// block: a partial restore is a corruption with a success code.
    /// </summary>
    public static Result<IReadOnlyList<GuideSectionDraft>> ToDrafts(GuideSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var sections = new List<GuideSectionDraft>(snapshot.Sections.Count);

        foreach (var section in snapshot.Sections)
        {
            var blocks = new List<GuideBlockDraft>(section.Blocks.Count);

            foreach (var block in section.Blocks)
            {
                if (!Enum.TryParse<GuideBlockKind>(block.Kind, ignoreCase: true, out var kind)
                    || !Enum.IsDefined(kind))
                {
                    return Result.Failure<IReadOnlyList<GuideSectionDraft>>(
                        GuideErrors.SnapshotInvalid);
                }

                blocks.Add(new GuideBlockDraft
                {
                    BlockId = block.BlockId,
                    Kind = kind,
                    Order = block.Order,
                    Content = block.Content,
                    MediaId = block.MediaId,
                    Metadata = block.Metadata
                });
            }

            sections.Add(new GuideSectionDraft
            {
                SectionId = section.SectionId,
                Title = section.Title,
                Order = section.Order,
                Blocks = blocks
            });
        }

        return Result.Success<IReadOnlyList<GuideSectionDraft>>(sections);
    }
}
