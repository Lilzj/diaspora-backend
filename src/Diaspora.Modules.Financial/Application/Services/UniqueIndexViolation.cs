using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Application.Services;

/// <summary>
/// Whether a failed save was a unique-index violation.
/// <para>
/// Matched on the message, as the Creator and Programme modules do: production runs Npgsql and the
/// suite SQLite, and naming either driver's exception here would put a database provider in the
/// application layer.
/// </para>
/// </summary>
internal static class UniqueIndexViolation
{
    public static bool Is(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;

            if (message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key value", StringComparison.OrdinalIgnoreCase)
                || message.Contains("23505", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
