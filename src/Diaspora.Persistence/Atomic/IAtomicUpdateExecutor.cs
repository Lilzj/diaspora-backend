namespace Diaspora.Persistence.Atomic;

/// <summary>
/// Runs a guarded update as one statement the database arbitrates.
/// <para>
/// <b>The platform owns this, and modules must not reimplement it.</b> That is not a stylistic
/// preference: a module writing its own conditional SQL writes it for one provider, tests it on
/// one provider, and leaves the next module to write a second half that disagrees with the first.
/// B10 and B11 both said so in their evolution reports, and
/// <c>PersistenceRuleArchitectureTests.No_module_writes_its_own_guarded_update</c> is what makes it
/// true rather than agreed.
/// </para>
/// <para>
/// <b>Retry safety.</b> The statement is relative — <c>col = col + n</c> guarded on the row's own
/// arithmetic — so replaying it is not the same as applying it twice: a replay after a rolled-back
/// attempt starts from the value the rollback restored. It composes with B1.0.2's execution
/// strategy for the same reason, and needs nothing from it.
/// </para>
/// </summary>
public interface IAtomicUpdateExecutor
{
    /// <summary>
    /// Applies <paramref name="update"/> inside the caller's transaction, reads back whatever it
    /// asked for, and reconciles its tracked instance if it supplied one.
    /// </summary>
    Task<AtomicUpdateResult> ExecuteAsync<TEntity>(
        AtomicUpdate<TEntity> update, CancellationToken cancellationToken = default)
        where TEntity : class;
}
