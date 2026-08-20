using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Intelligence;

namespace Diaspora.Modules.Guides.Application.Intelligence.Mapping;

/// <summary>
/// Turns an official notice into what a client renders.
/// <para>
/// <b>Static, and every mapper in this module is.</b> A class with no state is a container entry
/// nobody resolves, and CA1822 in Release is what pushed B8's and B8.1's mappers static in the first
/// place. Two phases have recorded the same lesson; this is the third not paying for it.
/// </para>
/// <para>
/// <b>The category and the authority are passed in, never looked up.</b> A mapper that resolved them
/// would issue two queries per card and turn a twenty-row page into forty-one round trips. The
/// composer resolves both in one batched read and hands them here — which is the whole reason
/// <see cref="IntelligenceReferenceResolver"/> exists.
/// </para>
/// <para>
/// <b>Editor-only fields are gated by a flag rather than by a second mapper.</b> A verification note
/// and a revision are meaningless to a reader and a revision handed to one invites a client to send
/// it back on a write it is not allowed to make. One method with a parameter keeps the two responses
/// impossible to diverge; two methods would eventually differ in a field nobody meant.
/// </para>
/// </summary>
public static class OfficialNoticeMapper
{
    /// <summary>The card a list, a dashboard band and a search hit all render.</summary>
    public static OfficialNoticeCardResponse ToCard(
        OfficialNotice notice,
        NoticeCategory? category,
        IssuingAuthority? authority,
        DateTimeOffset asOf,
        string? locale)
    {
        ArgumentNullException.ThrowIfNull(notice);

        var (title, summary, _) = notice.Localized(locale);
        var (categoryName, _) = category?.Localized(locale) ?? (notice.CategoryKey, null);

        return new OfficialNoticeCardResponse
        {
            OfficialNoticeId = notice.Id.Value,
            Title = title,
            Summary = summary,
            CountryCode = notice.CountryCode,
            Region = notice.Region,
            CategoryKey = notice.CategoryKey,
            CategoryName = categoryName,
            CategoryIcon = category?.Icon,
            Severity = IntelligenceWireNames.ToWire(notice.Severity),
            Status = IntelligenceWireNames.ToWire(notice.Status),
            VerificationStatus = IntelligenceWireNames.ToWire(notice.VerificationState),

            AuthorityId = notice.AuthorityId.Value,

            // An authority that failed to resolve is a data fault rather than a normal state, and
            // the honest rendering is a blank name rather than a plausible wrong one. A reader
            // seeing no issuing body knows not to treat the card as official; a reader seeing
            // "Unknown Ministry" does not.
            AuthorityName = authority?.Name ?? string.Empty,
            AuthorityShortName = authority?.ShortName,
            AuthorityType = authority is null
                ? string.Empty
                : IntelligenceWireNames.ToWire(authority.AuthorityType),
            IsOfficial = authority?.IsVerified ?? false,

            PublicationDate = notice.PublicationDate,
            PublishedAt = notice.PublishedAt,
            EffectiveFrom = notice.EffectiveFrom,
            ExpiresAt = notice.ExpiresAt,
            IsActive = notice.IsActiveAt(asOf),
            IsExpired = notice.HasExpiredAt(asOf),
            SupersededById = notice.SupersededById?.Value,
            LastReviewedAt = notice.LastReviewedAt,

            ViewCount = notice.ViewCount,
            ReferenceCount = notice.References.Count,
            AttachmentCount = notice.Attachments.Count
        };
    }

    /// <summary>The full notice, for a detail screen.</summary>
    public static OfficialNoticeResponse ToResponse(
        OfficialNotice notice,
        NoticeCategory? category,
        IssuingAuthority? authority,
        DateTimeOffset asOf,
        string? locale,
        bool forEditor)
    {
        ArgumentNullException.ThrowIfNull(notice);

        var (_, _, content) = notice.Localized(locale);

        return new OfficialNoticeResponse
        {
            Card = ToCard(notice, category, authority, asOf, locale),
            Content = content,
            ReferenceNumber = notice.ReferenceNumber,
            SourceUrl = notice.SourceUrl,
            Languages = [.. notice.Languages.Select(language => language.Code)],
            AvailableLocales =
                [.. notice.Localizations.Select(localization => localization.Locale).Order()],
            ResolvedLocale = ResolveLocale(notice, locale),
            Attachments =
            [
                .. notice.Attachments
                    .OrderBy(attachment => attachment.Position)
                    .Select(attachment => new NoticeAttachmentResponse
                    {
                        MediaId = attachment.MediaId,
                        Caption = attachment.Caption,
                        Position = attachment.Position
                    })
            ],
            References =
            [
                .. notice.References
                    .OrderBy(reference => reference.Position)
                    .Select(reference => new NoticeReferenceResponse
                    {
                        Kind = IntelligenceWireNames.ToWire(reference.Kind),
                        TargetId = reference.TargetId,
                        Label = reference.Label,
                        Position = reference.Position
                    })
            ],
            VerificationNote = forEditor ? notice.VerificationNote : null,
            Revision = forEditor ? notice.Revision : null
        };
    }

    /// <summary>
    /// Which locale the reader actually got, after the exact→language→default ladder.
    /// <para>
    /// Reported rather than assumed, because a client that asked for <c>fr-MU</c> and received the
    /// <c>fr</c> translation needs to know — a language switcher that shows French as selected when
    /// the reader asked for Mauritian French is telling them something untrue about what they can
    /// change.
    /// </para>
    /// <para>
    /// The empty string means the notice's own language, which is the honest answer when no
    /// translation applied: this module does not record which language a notice was written in as a
    /// tag, and inventing one here would be a guess a client would render.
    /// </para>
    /// </summary>
    private static string ResolveLocale(OfficialNotice notice, string? locale)
    {
        var normalized = IntelligenceNormalization.Locale(locale);

        if (normalized is null)
        {
            return string.Empty;
        }

        if (notice.Localizations.Any(localization =>
            string.Equals(localization.Locale, normalized, StringComparison.Ordinal)))
        {
            return normalized;
        }

        var language = normalized.Split('-')[0];

        return notice.Localizations.Any(localization =>
            string.Equals(localization.Locale, language, StringComparison.Ordinal))
            ? language
            : string.Empty;
    }
}
