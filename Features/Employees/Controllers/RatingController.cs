using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees/ratings")]
[Route("Rating")]
public class RatingController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public RatingController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetRatings")]
    public async Task<ActionResult<List<EmployeeRatingDto>>> GetRatings([FromQuery] int? employeeId, CancellationToken ct)
    {
        var query = _context.EmployeeRatings
            .Include(r => r.Employee)
            .AsNoTracking()
            .AsQueryable();

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(r => r.EmployeeId == employeeId.Value);
        }

        var list = await query.OrderByDescending(r => r.RatedAt)
            .Select(r => new EmployeeRatingDto(r.Id, r.EmployeeId, r.Employee != null ? r.Employee.Name : null, r.Score, r.Feedback, r.RatedByUserId, r.RatedAt))
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPost]
    [HttpPost("SubmitRating")]
    public async Task<ActionResult<EmployeeRatingDto>> SubmitRating([FromBody] SubmitRatingRequest request, CancellationToken ct)
    {
        var r = new EmployeeRating
        {
            EmployeeId = request.EmployeeId,
            Score = request.Score,
            Feedback = request.Feedback,
            RatedByUserId = User.GetUserId() ?? "system",
            RatedAt = DateTime.UtcNow
        };

        await _context.EmployeeRatings.AddAsync(r, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(new EmployeeRatingDto(r.Id, r.EmployeeId, null, r.Score, r.Feedback, r.RatedByUserId, r.RatedAt));
    }
}

public record EmployeeRatingDto(int Id, int EmployeeId, string? EmployeeName, int Score, string? Feedback, string RatedByUserId, DateTime RatedAt);
public record SubmitRatingRequest(int EmployeeId, int Score, string? Feedback);
