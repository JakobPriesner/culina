using Application.Abstractions;
using Domain.Shared;
using Domain.Users;
using Npgsql;

namespace Infrastructure.Persistence.Users;

/// <summary>Stores user accounts.</summary>
/// <param name="executor">Runs the SQL inside the request's transaction.</param>
internal sealed class UserRepository(DbExecutor executor) : IUserRepository
{
    private const string Columns = "id, email, display_name, password_hash, created_at, version";

    /// <summary>The unique index the email column carries, named for the catch below.</summary>
    private const string EmailUniqueConstraint = "users_email_key";

    public async Task<Result<User>> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await executor.QuerySingleOrDefaultAsync<UserRow>(
            $"select {Columns} from users where id = @userId;",
            new { userId },
            cancellationToken).ConfigureAwait(false);

        return row is null ? UserErrors.NotFound(userId) : row.ToDomain();
    }

    public async Task<Result<User>> FindByEmailAsync(Email email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        var row = await executor.QuerySingleOrDefaultAsync<UserRow>(
            $"select {Columns} from users where email = @email;",
            new { email = email.Value },
            cancellationToken).ConfigureAwait(false);

        return row is null ? UserErrors.NotFound(Guid.Empty) : row.ToDomain();
    }

    public Task<int> CountAsync(CancellationToken cancellationToken) =>
        executor.ExecuteScalarAsync<int>("select count(*) from users;", null, cancellationToken)!;

    public async Task<Result> AddAsync(User user, bool isAdmin, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            await executor.ExecuteAsync(
                """
                insert into users (id, email, display_name, password_hash, is_admin, created_at, version)
                values (@id, @email, @displayName, @passwordHash, @isAdmin, @createdAt, 1);
                """,
                new
                {
                    id = user.Id,
                    email = user.Email.Value,
                    displayName = user.DisplayName.Value,
                    passwordHash = user.PasswordHash,
                    isAdmin,
                    createdAt = user.CreatedAt
                },
                cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }
        catch (PostgresException failure) when (failure.ConstraintName == EmailUniqueConstraint)
        {
            // The sanctioned exception to "never catch to return a failure": a
            // second registration can commit between the existence check and
            // this insert, and the database is the only place that can settle
            // the race. The constraint name is named so a future rename fails
            // loudly here instead of silently turning a conflict into a 500.
            return UserErrors.EmailAlreadyUsed;
        }
    }

    public async Task<Result<long>> UpdateAsync(
        User user,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var version = await executor.ExecuteScalarAsync<long?>(
            """
            update users
            set email = @email,
                display_name = @displayName,
                password_hash = @passwordHash,
                version = version + 1
            where id = @id and version = @expectedVersion
            returning version;
            """,
            new
            {
                id = user.Id,
                email = user.Email.Value,
                displayName = user.DisplayName.Value,
                passwordHash = user.PasswordHash,
                expectedVersion
            },
            cancellationToken).ConfigureAwait(false);

        // No row matched, so either the user is gone or someone else wrote
        // first. The version check lives in the WHERE, never in C#.
        return version is null ? ConcurrencyErrors.VersionMismatch : version.Value;
    }

    public async Task<bool> IsAdminAsync(Guid userId, CancellationToken cancellationToken) =>
        await executor.ExecuteScalarAsync<bool>(
            "select coalesce((select is_admin from users where id = @userId), false);",
            new { userId },
            cancellationToken).ConfigureAwait(false);
}
