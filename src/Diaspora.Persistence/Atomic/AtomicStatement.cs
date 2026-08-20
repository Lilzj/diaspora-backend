using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace Diaspora.Persistence.Atomic;

/// <summary>One bound value, and the model property whose store type shapes it.</summary>
/// <param name="Property">
/// Null for a plain integer delta, which every provider maps the same way. Non-null for anything
/// the model converts — a strongly typed identifier, an enum stored as text, an instant stored as
/// ticks — so the parameter is written in the form the column actually holds.
/// </param>
internal sealed record AtomicParameter(string Name, IProperty? Property, object? Value);

/// <summary>
/// The SQL text of a guarded update, and the values bound to it.
/// <para>
/// <b>Every identifier here comes from EF's model and passes through the provider's own delimiter;
/// every value is a parameter.</b> Nothing in this file names a provider, and nothing that reaches
/// it from a caller is ever concatenated into the text — which is what lets one statement be
/// correct on PostgreSQL, SQL Server and SQLite at once, and what makes the injection question
/// answerable rather than argued.
/// </para>
/// </summary>
internal sealed class AtomicStatement
{
    private AtomicStatement(string sql, IReadOnlyList<AtomicParameter> parameters)
    {
        Sql = sql;
        Parameters = parameters;
    }

    public string Sql { get; }

    public IReadOnlyList<AtomicParameter> Parameters { get; }

    /// <summary>The guarded <c>UPDATE</c>.</summary>
    public static AtomicStatement Update<TEntity>(
        IModel model, ISqlGenerationHelper sql, AtomicUpdate<TEntity> update)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(update);

        if (update.Assignments.Count == 0)
        {
            throw new InvalidOperationException(
                "A guarded update with no assignments would lock a row and change nothing.");
        }

        var writer = new StatementWriter(model, sql, typeof(TEntity));

        return writer.WriteUpdate(update);
    }

    /// <summary>The read-back: the same row's counters, as the statement left them.</summary>
    public static AtomicStatement Read<TEntity>(
        IModel model, ISqlGenerationHelper sql, object key, IReadOnlyList<string> paths)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(paths);

        var writer = new StatementWriter(model, sql, typeof(TEntity));

        return writer.WriteRead(key, paths);
    }

    /// <summary>
    /// Writes one statement for one entity type. Instantiated per statement, so the model, the
    /// provider's delimiter and the entity being addressed are carried once rather than threaded
    /// through every private method.
    /// </summary>
    private sealed class StatementWriter(IModel model, ISqlGenerationHelper sql, Type entityType)
    {
        private readonly List<AtomicParameter> _parameters = [];
        private readonly StringBuilder _text = new();

        public AtomicStatement WriteUpdate<TEntity>(AtomicUpdate<TEntity> update)
            where TEntity : class
        {
            var target = AtomicColumnResolver.Target(model, entityType);

            _text.Append("UPDATE ")
                .Append(sql.DelimitIdentifier(target.TableName, target.Schema))
                .Append(" SET ");

            for (var index = 0; index < update.Assignments.Count; index++)
            {
                if (index > 0)
                {
                    _text.Append(", ");
                }

                AppendAssignment(update.Assignments[index]);
            }

            _text.Append(" WHERE ")
                .Append(sql.DelimitIdentifier(target.KeyColumnName))
                .Append(" = ")
                .Append(Bind(target.KeyProperty, update.Key));

            foreach (var condition in update.Conditions)
            {
                _text.Append(" AND ");
                AppendCondition(condition);
            }

            return Build();
        }

        public AtomicStatement WriteRead(object key, IReadOnlyList<string> paths)
        {
            var target = AtomicColumnResolver.Target(model, entityType);

            _text.Append("SELECT ");

            for (var index = 0; index < paths.Count; index++)
            {
                if (index > 0)
                {
                    _text.Append(", ");
                }

                _text.Append(Column(paths[index]));
            }

            _text.Append(" FROM ")
                .Append(sql.DelimitIdentifier(target.TableName, target.Schema))
                .Append(" WHERE ")
                .Append(sql.DelimitIdentifier(target.KeyColumnName))
                .Append(" = ")
                .Append(Bind(target.KeyProperty, key));

            return Build();
        }

        private void AppendAssignment(AtomicAssignment assignment)
        {
            var column = Column(assignment.Path);

            _text.Append(column).Append(" = ");

            switch (assignment.Kind)
            {
                case AtomicAssignmentKind.Set:
                    _text.Append(Bind(Property(assignment.Path), assignment.Value));
                    break;

                case AtomicAssignmentKind.Add:
                    _text.Append(column).Append(" + ").Append(BindNumber(assignment.Value));
                    break;

                case AtomicAssignmentKind.Subtract:
                    _text.Append(column).Append(" - ").Append(BindNumber(assignment.Value));
                    break;

                case AtomicAssignmentKind.SubtractFloored:
                {
                    // Every right-hand side in one UPDATE is evaluated against the pre-update row,
                    // on all three providers. That is what lets the clamp and the subtraction agree,
                    // and what lets the two halves of a floored move stay consistent below.
                    var delta = BindNumber(assignment.Value);

                    _text.Append("CASE WHEN ").Append(column).Append(" >= ").Append(delta)
                        .Append(" THEN ").Append(column).Append(" - ").Append(delta)
                        .Append(" ELSE 0 END");
                    break;
                }

                case AtomicAssignmentKind.AddLimited:
                {
                    var delta = BindNumber(assignment.Value);
                    var limit = Column(assignment.LimitPath!);

                    _text.Append(column).Append(" + CASE WHEN ").Append(limit).Append(" >= ")
                        .Append(delta).Append(" THEN ").Append(delta)
                        .Append(" ELSE ").Append(limit).Append(" END");
                    break;
                }

                default:
                    throw new InvalidOperationException(
                        $"Unknown assignment kind '{assignment.Kind}'.");
            }
        }

        private void AppendCondition(AtomicCondition condition)
        {
            if (condition.IsValueComparison)
            {
                var column = Column(condition.ValuePath!);

                if (condition.Value is null)
                {
                    _text.Append(column)
                        .Append(condition.Comparison == AtomicComparison.Equal
                            ? " IS NULL"
                            : " IS NOT NULL");

                    return;
                }

                _text.Append(column)
                    .Append(' ').Append(Operator(condition.Comparison)).Append(' ')
                    .Append(Bind(Property(condition.ValuePath!), condition.Value));

                return;
            }

            _text.Append('(');
            AppendTerm(condition.Left!);
            _text.Append(") ").Append(Operator(condition.Comparison)).Append(" (");
            AppendTerm(condition.Right!);
            _text.Append(')');
        }

        private void AppendTerm(AtomicTerm term)
        {
            var written = false;

            foreach (var column in term.Columns)
            {
                if (written)
                {
                    _text.Append(column.IsSubtracted ? " - " : " + ");
                }
                else if (column.IsSubtracted)
                {
                    _text.Append("0 - ");
                }

                _text.Append(Column(column.Path));
                written = true;
            }

            if (written && term.Constant == 0)
            {
                return;
            }

            if (written)
            {
                _text.Append(term.Constant < 0 ? " - " : " + ")
                    .Append(BindNumber(Math.Abs(term.Constant)));

                return;
            }

            _text.Append(BindNumber(term.Constant));
        }

        private AtomicStatement Build() => new(_text.ToString(), _parameters);

        private string Column(string path) =>
            sql.DelimitIdentifier(AtomicColumnResolver.Column(model, entityType, path).ColumnName);

        private IProperty Property(string path) =>
            AtomicColumnResolver.Column(model, entityType, path).Property;

        private string Bind(IProperty property, object? value) => Add(property, value);

        private string BindNumber(object? value) =>
            Add(property: null, Convert.ToInt64(value, CultureInfo.InvariantCulture));

        private string Add(IProperty? property, object? value)
        {
            var name = "atomic_p" + _parameters.Count.ToString(CultureInfo.InvariantCulture);

            _parameters.Add(new AtomicParameter(name, property, value));

            return sql.GenerateParameterName(name);
        }

        private static string Operator(AtomicComparison comparison) => comparison switch
        {
            AtomicComparison.Equal => "=",
            AtomicComparison.NotEqual => "<>",
            AtomicComparison.GreaterThan => ">",
            AtomicComparison.GreaterThanOrEqual => ">=",
            AtomicComparison.LessThan => "<",
            AtomicComparison.LessThanOrEqual => "<=",
            _ => throw new InvalidOperationException($"Unknown comparison '{comparison}'.")
        };
    }
}
