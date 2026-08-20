using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Candidates;

/// <summary>
/// The caller's own candidate profile.
/// <para>
/// **Gap G-5, closed.** `POST /candidate/profile` and `PUT /candidate/profile` both existed and
/// `GET` on the same path answered **405** — a profile a member could create and update but never
/// read back. `src/app/job/candidate-profile.tsx` documents the consequence in a comment of its
/// own: *"There is no `GET /jobs/candidate/profile`"*, so the screen had nothing to populate an
/// edit form from and a member could only ever overwrite blindly.
/// </para>
/// <para>
/// Deliberately "mine" rather than "by id". A candidate profile carries a headline, a location and
/// a work history, and who may see somebody else's is governed by
/// <c>PUT /candidate/profile/visibility</c> and the employer-facing search — not by a route that
/// takes an identifier.
/// </para>
/// </summary>
public sealed record GetMyCandidateProfileQuery : IQuery<CandidateProfileResponse>;
