using Microsoft.EntityFrameworkCore;
using Luxira.Api.Data;
using Luxira.Api.Features.Media.Models;

namespace Luxira.Api.Features.Media.Repositories;

public class MediaRepository
{
    private readonly ApplicationDbContext _context;

    public MediaRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<S3StoredObject?> GetByKeyAsync(string s3Key, CancellationToken ct = default)
    {
        return await _context.S3StoredObjects.AsNoTracking().FirstOrDefaultAsync(s => s.Key == s3Key, ct);
    }

    public async Task<S3StoredObject> AddAsync(S3StoredObject media, CancellationToken ct = default)
    {
        var result = await _context.S3StoredObjects.AddAsync(media, ct);
        await _context.SaveChangesAsync(ct);
        return result.Entity;
    }
}
