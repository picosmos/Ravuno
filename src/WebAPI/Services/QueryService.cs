using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ravuno.DataStorage;
using Ravuno.DataStorage.Models;
using Ravuno.WebAPI.Services.Contracts;

namespace Ravuno.WebAPI.Services;

public partial class QueryService(DataStorageContext dbContext) : IQueryService
{
    private readonly DataStorageContext _dbContext = dbContext;

    [GeneratedRegex(@"^\s*SELECT\s", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SelectOnlyRegex();

    [GeneratedRegex(
        @"\b(INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|TRUNCATE|EXEC|EXECUTE)\b",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex DangerousKeywordsRegex();

    public async Task<List<Query>> GetAllByUserAsync(int userId)
    {
        return await this
            ._dbContext.Queries.Where(s => s.UserId == userId)
            .OrderBy(s => s.Title)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Query?> GetByIdAsync(long id, int userId)
    {
        return await this
            ._dbContext.Queries.Where(s => s.Id == id && s.UserId == userId)
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    public async Task<Query> CreateAsync(string title, string query, int userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        if (!this.ValidateSelectOnlyQuery(query, out var error))
        {
            throw new InvalidOperationException(error);
        }

        if (await this.TitleExistsForUserAsync(title, userId))
        {
            throw new InvalidOperationException($"A query with title '{title}' already exists");
        }

        var sqlScript = new Query
        {
            Title = title.Trim(),
            SqlQuery = query.Trim(),
            UserId = userId,
        };

        await this._dbContext.Queries.AddAsync(sqlScript);
        await this._dbContext.SaveChangesAsync();

        return sqlScript;
    }

    public async Task UpdateAsync(long id, string title, string query, int userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        if (!this.ValidateSelectOnlyQuery(query, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var sqlScript = await this._dbContext.Queries.FirstOrDefaultAsync(s =>
            s.Id == id && s.UserId == userId
        );

        if (sqlScript is null)
        {
            throw new InvalidOperationException("Query not found or access denied");
        }

        if (await this.TitleExistsForUserAsync(title, userId, id))
        {
            throw new InvalidOperationException($"A query with title '{title}' already exists");
        }

        sqlScript.Title = title.Trim();
        sqlScript.SqlQuery = query.Trim();

        await this._dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(long id, int userId)
    {
        var sqlScript = await this._dbContext.Queries.FirstOrDefaultAsync(s =>
            s.Id == id && s.UserId == userId
        );

        if (sqlScript is null)
        {
            throw new InvalidOperationException("Query not found or access denied");
        }

        this._dbContext.Queries.Remove(sqlScript);
        await this._dbContext.SaveChangesAsync();
    }

    public async Task<bool> TitleExistsForUserAsync(
        string title,
        int userId,
        long? excludeId = null
    )
    {
        var query = this._dbContext.Queries.Where(s =>
            s.UserId == userId && EF.Functions.Like(s.Title, title)
        );

        if (excludeId.HasValue)
        {
            query = query.Where(s => s.Id != excludeId.Value);
        }

        return await query.AnyAsync();
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
}
