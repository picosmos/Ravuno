using System.Text.RegularExpressions;
using Ravuno.DataStorage.Models;
using Ravuno.DataStorage.Repositories.Contracts;
using Ravuno.WebAPI.Models;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Services;

public partial class QueryService(IQueryRepository queryRepository, IItemRepository itemRepository)
    : IQueryService
{
    private readonly IQueryRepository _queryRepository = queryRepository;
    private readonly IItemRepository _itemRepository = itemRepository;

    [GeneratedRegex(@"^\s*SELECT\s", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SelectOnlyRegex();

    [GeneratedRegex(
        @"\b(INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|TRUNCATE|EXEC|EXECUTE)\b",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex DangerousKeywordsRegex();

    // Query entity operations
    public async Task<List<Query>> GetAllByUserAsync(int userId)
    {
        return await this._queryRepository.GetAllByUserAsync(userId);
    }

    public async Task<Query?> GetByIdAsync(long id, int userId)
    {
        return await this._queryRepository.GetByIdAndUserAsync(id, userId);
    }

    public async Task<Query> CreateAsync(string title, string query, string email, int userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        if (!this.ValidateSelectOnlyQuery(query, out var error))
        {
            throw new InvalidOperationException(error);
        }

        if (await this.TitleExistsForUserAsync(title, userId))
        {
            throw new InvalidOperationException($"A query with title '{title}' already exists");
        }

        const string chars = "0123456789abcdefghijklmnopqrstuvwxzy";
        var publicId = string.Create(
            32,
            Random.Shared,
            (span, random) =>
            {
                for (var i = 0; i < span.Length; i++)
                {
                    span[i] = chars[random.Next(chars.Length)];
                }
            }
        );

        var queryEntity = new Query
        {
            Title = title.Trim(),
            SqlQuery = query.Trim(),
            Email = email.Trim(),
            PublicId = publicId,
            UserId = userId,
        };

        return await this._queryRepository.CreateAsync(queryEntity);
    }

    public async Task UpdateAsync(long id, string title, string query, string email, int userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        if (!this.ValidateSelectOnlyQuery(query, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var queryEntity =
            await this._queryRepository.GetByIdAndUserAsync(id, userId)
            ?? throw new InvalidOperationException("Query not found or access denied");

        if (await this.TitleExistsForUserAsync(title, userId, id))
        {
            throw new InvalidOperationException($"A query with title '{title}' already exists");
        }

        queryEntity.Title = title.Trim();
        queryEntity.SqlQuery = query.Trim();
        queryEntity.Email = email.Trim();

        await this._queryRepository.UpdateAsync(queryEntity);
    }

    public async Task ReassignQueryAsync(long id, int newUserId)
    {
        var queryEntity =
            await this._queryRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Query not found");

        queryEntity.UserId = newUserId;
        await this._queryRepository.UpdateAsync(queryEntity);
    }

    public async Task DeleteAsync(long id, int userId)
    {
        var queryEntity =
            await this._queryRepository.GetByIdAndUserAsync(id, userId)
            ?? throw new InvalidOperationException("Query not found or access denied");

        await this._queryRepository.DeleteAsync(queryEntity);
    }

    public async Task<bool> TitleExistsForUserAsync(
        string title,
        int userId,
        long? excludeId = null
    )
    {
        return await this._queryRepository.TitleExistsForUserAsync(title, userId, excludeId);
    }

    public bool ValidateSelectOnlyQuery(string query, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(query))
        {
            errorMessage = "Query cannot be empty";
            return false;
        }

        var trimmedQuery = query.Trim();

        if (!SelectOnlyRegex().IsMatch(trimmedQuery))
        {
            errorMessage = "Query must start with SELECT";
            return false;
        }

        if (DangerousKeywordsRegex().IsMatch(trimmedQuery))
        {
            errorMessage =
                "Query contains forbidden keywords (INSERT, UPDATE, DELETE, DROP, CREATE, ALTER, TRUNCATE, EXEC, EXECUTE)";
            return false;
        }

        return true;
    }

    // UpdateConfiguration operations (merged from UpdateConfigurationService)
    public async Task<List<UpdateConfiguration>> GetUpdateConfigurationsAsync()
    {
        var queries = await this._queryRepository.GetAllAsync();
        return
        [
            .. queries.Select(q => new UpdateConfiguration
            {
                Id = q.Id,
                QueryTitle = q.Title,
                SqlQuery = q.SqlQuery,
                PublicId = q.PublicId,
                Email = q.Email,
            }),
        ];
    }

    public async Task<List<UpdateConfiguration>> GetUpdateConfigurationsByUserAsync(int userId)
    {
        var queries = await this._queryRepository.GetAllByUserAsync(userId);
        return
        [
            .. queries.Select(q => new UpdateConfiguration
            {
                Id = q.Id,
                QueryTitle = q.Title,
                SqlQuery = q.SqlQuery,
                PublicId = q.PublicId,
                Email = q.Email,
            }),
        ];
    }

    public async Task<UpdateConfiguration?> GetUpdateConfigurationByIdAsync(long id)
    {
        var query = await this._queryRepository.GetByIdAsync(id);

        if (query == null)
        {
            return null;
        }

        return new UpdateConfiguration
        {
            Id = query.Id,
            QueryTitle = query.Title,
            SqlQuery = query.SqlQuery,
            PublicId = query.PublicId,
            Email = query.Email,
        };
    }

    public async Task<UpdateConfiguration?> GetUpdateConfigurationByPublicIdAsync(string publicId)
    {
        var query = await this._queryRepository.GetByPublicIdAsync(publicId);

        if (query == null)
        {
            return null;
        }

        return new UpdateConfiguration
        {
            Id = query.Id,
            QueryTitle = query.Title,
            SqlQuery = query.SqlQuery,
            PublicId = query.PublicId,
            Email = query.Email,
        };
    }

    // SQL execution operations
    public async Task<List<Item>> ExecuteSqlQueryAsync(
        string sqlQuery,
        CancellationToken cancellationToken = default
    )
    {
        return await this._itemRepository.ExecuteSqlQueryAsync(sqlQuery, cancellationToken);
    }
}
