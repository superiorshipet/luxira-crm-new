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
[Route("api/v1/employees/notes")]
[Route("PersonalNotes")]
public class PersonalNotesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PersonalNotesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetNotes")]
    public async Task<ActionResult<List<PersonalNote>>> GetNotes(CancellationToken ct)
    {
        var userId = User.GetUserId() ?? "system";
        var notes = await _context.PersonalNotes
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

        return Ok(notes);
    }

    [HttpPost]
    [HttpPost("SaveNote")]
    public async Task<ActionResult<PersonalNote>> SaveNote([FromBody] SavePersonalNoteRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId() ?? "system";
        var note = new PersonalNote
        {
            UserId = userId,
            Title = request.Title,
            Content = request.Content,
            ReminderAt = request.ReminderAt,
            CreatedAt = DateTime.UtcNow
        };

        await _context.PersonalNotes.AddAsync(note, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(note);
    }
}

public record SavePersonalNoteRequest(string Title, string Content, DateTime? ReminderAt);
