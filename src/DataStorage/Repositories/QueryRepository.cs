using Microsoft.EntityFrameworkCore;
using Ravuno.DataStorage.Models;
using Ravuno.DataStorage.Repositories.Contracts;

namespace Ravuno.DataStorage.Repositories;

public class QueryRepository(DataStorageContext dbContext) : IQueryRepository
{
    private readonly DataStorageContext _dbContext = dbContext;

    public async Task<List<Query>> GetAllAsync()
    {
        return await this._dbContext.Queries.OrderBy(q => q.Title).AsNoTracking().ToListAsync();
    }

    public async Task<List<Query>> GetAllByUserAsync(int userId)
    {
        return await this
            ._dbContext.Queries.Where(q => q.UserId == userId)
            .OrderBy(q => q.Title)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Query?> GetByIdAsync(long id)
    {
        return await this
            ._dbContext.Queries.Include(q => q.EmailReceivers)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<Query?> GetByIdAndUserAsync(long id, int userId)
    {
        return await this
            ._dbContext.Queries.Where(q => q.Id == id && q.UserId == userId)
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    public async Task<Query?> GetByPublicIdAsync(string publicId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicId);
        return await this
            ._dbContext.Queries.Include(q => q.EmailReceivers)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.PublicId == publicId);
    }

    public async Task<Query> CreateAsync(Query query)
    {
        ArgumentNullException.ThrowIfNull(query);
        await this._dbContext.Queries.AddAsync(query);
        await this._dbContext.SaveChangesAsync();
        return query;
    }

    public async Task UpdateAsync(Query query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entry = this._dbContext.Entry(query);
        if (entry.State == EntityState.Detached)
        {
            this._dbContext.Queries.Attach(query);
        }

        entry.Property(q => q.Title).IsModified = true;
        entry.Property(q => q.SqlQuery).IsModified = true;
        entry.Property(q => q.UserId).IsModified = true;

        await this._dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(Query query)
    {
        ArgumentNullException.ThrowIfNull(query);
        this._dbContext.Queries.Remove(query);
        await this._dbContext.SaveChangesAsync();
    }

    public async Task<bool> TitleExistsForUserAsync(
        string title,
        int userId,
        long? excludeId = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var query = this._dbContext.Queries.Where(q =>
            q.UserId == userId && EF.Functions.Like(q.Title, title)
        );

        if (excludeId.HasValue)
        {
            query = query.Where(q => q.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
}
