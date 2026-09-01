using System.Collections.Concurrent;
using System.Security.Claims;
using Luxira.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.ManufacturingCompanies.Hubs;

[Authorize(Roles = "Admin,ExecutiveDirector,Administrator")]
public class StoreCodeEditorHub : Hub
{
    private readonly ApplicationDbContext _context;

    public StoreCodeEditorHub(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task JoinFile(int folderId)
    {
        var folder = await _context.StoreCodeFolders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == folderId);

        if (folder == null)
        {
            await Clients.Caller.SendAsync("StoreCodeError", "المجلد غير موجود.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"StoreFolder_{folderId}");

        await Clients.Caller.SendAsync("LoadInitialContent", new
        {
            content = folder.Content ?? string.Empty,
            updatedAt = folder.UpdatedAt.ToString("yyyy/MM/dd hh:mm tt")
        });
    }

    public async Task UpdateContent(int folderId, string content)
    {
        var folder = await _context.StoreCodeFolders.FirstOrDefaultAsync(x => x.Id == folderId);
        if (folder != null)
        {
            folder.Content = content;
            folder.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await Clients.Group($"StoreFolder_{folderId}").SendAsync("ContentUpdated", new
            {
                folderId,
                content,
                updatedBy = Context.User?.Identity?.Name ?? "مستخدم",
                updatedAt = folder.UpdatedAt.ToString("yyyy/MM/dd hh:mm tt")
            });
        }
    }
}
