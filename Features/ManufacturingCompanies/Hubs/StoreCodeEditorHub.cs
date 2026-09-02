using System.Collections.Concurrent;
using System.Security.Claims;
using Luxira.Api.Data;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.ManufacturingCompanies.Hubs;

[Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
public class StoreCodeEditorHub : Hub
{
    private static readonly ConcurrentDictionary<int, StoreCodeEditorRoomState> Rooms = new();
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(6);
    private readonly ApplicationDbContext _context;

    public StoreCodeEditorHub(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task JoinFile(int folderId)
    {
        var folder = await _context.StoreCodeFolders
            .Include(item => item.ManufacturingCompany)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == folderId && !item.IsDeleted,
                Context.ConnectionAborted);

        if (folder is null)
        {
            await Clients.Caller.SendAsync("StoreCodeError", "الملف غير موجود.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(folderId));

        var room = GetRoom(folderId);
        CleanupExpiredLock(room);
        await Clients.Caller.SendAsync("LoadInitialContent", new
        {
            content = folder.Content ?? string.Empty,
            updatedAt = folder.UpdatedAt.ToString("yyyy/MM/dd hh:mm tt"),
            updatedAtIso = folder.UpdatedAt.ToString("o")
        });

        if (room.HasActiveWriter && room.ActiveConnectionId != Context.ConnectionId)
        {
            await SendLockStateAsync(Clients.Caller, room);
        }
    }

    public async Task<bool> RequestTyping(int folderId)
    {
        var folderExists = await _context.StoreCodeFolders
            .AsNoTracking()
            .AnyAsync(
                item => item.Id == folderId && !item.IsDeleted,
                Context.ConnectionAborted);
        if (!folderExists)
        {
            await Clients.Caller.SendAsync("StoreCodeError", "الملف غير موجود.");
            return false;
        }

        var room = GetRoom(folderId);
        var userInfo = await GetCurrentUserInfoAsync();
        var canWrite = false;
        lock (room.SyncRoot)
        {
            CleanupExpiredLock(room);
            if (!room.HasActiveWriter || room.ActiveConnectionId == Context.ConnectionId)
            {
                room.ActiveConnectionId = Context.ConnectionId;
                room.ActiveUserId = userInfo.UserId;
                room.ActiveUserName = userInfo.DisplayName;
                room.ActiveUserImageUrl = userInfo.ImageUrl;
                room.LastTypingAt = DateTime.UtcNow;
                canWrite = true;
            }
        }

        if (canWrite)
        {
            await Clients.Group(GetGroupName(folderId)).SendAsync("EditingLocked", new
            {
                userId = userInfo.UserId,
                userName = userInfo.DisplayName,
                userImageUrl = userInfo.ImageUrl,
                connectionId = Context.ConnectionId
            });
        }
        else
        {
            await SendLockStateAsync(Clients.Caller, room);
        }

        return canWrite;
    }

    public Task KeepTyping(int folderId)
    {
        var room = GetRoom(folderId);
        lock (room.SyncRoot)
        {
            if (room.ActiveConnectionId == Context.ConnectionId)
                room.LastTypingAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public async Task SendChange(int folderId, string? content)
    {
        var room = GetRoom(folderId);
        var canWrite = false;
        lock (room.SyncRoot)
        {
            CleanupExpiredLock(room);
            canWrite = room.ActiveConnectionId == Context.ConnectionId;
            if (canWrite) room.LastTypingAt = DateTime.UtcNow;
        }

        if (!canWrite)
        {
            await Clients.Caller.SendAsync("WriteDenied", new
            {
                userName = room.ActiveUserName ?? "مستخدم آخر",
                userImageUrl = room.ActiveUserImageUrl ?? string.Empty
            });
            return;
        }

        var userInfo = await GetCurrentUserInfoAsync();
        var folder = await _context.StoreCodeFolders
            .Include(item => item.ManufacturingCompany)
            .FirstOrDefaultAsync(
                item => item.Id == folderId && !item.IsDeleted,
                Context.ConnectionAborted);
        if (folder is null)
        {
            await Clients.Caller.SendAsync("StoreCodeError", "الملف غير موجود.");
            return;
        }

        var oldContent = folder.Content ?? string.Empty;
        var newContent = content ?? string.Empty;
        if (!string.Equals(oldContent, newContent, StringComparison.Ordinal))
        {
            AddCodeHistoryRows(folder, oldContent, newContent, userInfo);
            folder.Content = newContent;
            folder.UpdatedAt = DateTime.Now;
            folder.UpdatedByUserId = userInfo.UserId;
            folder.UpdatedByName = userInfo.DisplayName;
            await _context.SaveChangesAsync(Context.ConnectionAborted);
        }

        var payload = new
        {
            content = newContent,
            userId = userInfo.UserId,
            userName = userInfo.DisplayName,
            userImageUrl = userInfo.ImageUrl,
            updatedAt = folder.UpdatedAt.ToString("yyyy/MM/dd hh:mm tt"),
            updatedAtIso = folder.UpdatedAt.ToString("o")
        };
        await Clients.GroupExcept(GetGroupName(folderId), Context.ConnectionId)
            .SendAsync("ReceiveCodeUpdate", payload);
        await Clients.Caller.SendAsync("SaveStatus", payload);
    }

    public async Task UpdateContent(int folderId, string content)
    {
        if (await RequestTyping(folderId))
            await SendChange(folderId, content);
    }

    public Task StopTyping(int folderId) => ReleaseLockIfOwned(folderId);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Task.WhenAll(Rooms.Keys.Select(ReleaseLockIfOwned));
        await base.OnDisconnectedAsync(exception);
    }

    private async Task ReleaseLockIfOwned(int folderId)
    {
        if (!Rooms.TryGetValue(folderId, out var room)) return;

        var released = false;
        lock (room.SyncRoot)
        {
            if (room.ActiveConnectionId == Context.ConnectionId)
            {
                room.ActiveConnectionId = null;
                room.ActiveUserId = null;
                room.ActiveUserName = null;
                room.ActiveUserImageUrl = null;
                room.LastTypingAt = null;
                released = true;
            }
        }

        if (released)
            await Clients.Group(GetGroupName(folderId)).SendAsync("EditingUnlocked");
    }

    private async Task<StoreCodeEditorUserInfo> GetCurrentUserInfoAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
            Context.UserIdentifier ?? string.Empty;
        var displayName = NormalizeDisplayName(
            Context.User?.FindFirstValue("FullName") ??
            Context.User?.FindFirstValue("EmployeeName") ??
            Context.User?.Identity?.Name);
        var imageUrl = NormalizeImageUrl(
            Context.User?.FindFirstValue("ImageUrl") ??
            Context.User?.FindFirstValue("ProfileImageUrl"));

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.ApplicationUserId == userId,
                    Context.ConnectionAborted);
            if (employee is not null)
            {
                displayName = NormalizeDisplayName(employee.DisplayName ?? employee.Name);
                imageUrl = NormalizeImageUrl(employee.ImageUrl);
            }
            else
            {
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == userId, Context.ConnectionAborted);
                if (!string.IsNullOrWhiteSpace(user?.Name))
                    displayName = NormalizeDisplayName(user.Name);
            }
        }

        return new StoreCodeEditorUserInfo(userId, displayName, imageUrl);
    }

    private void AddCodeHistoryRows(
        StoreCodeFolder folder,
        string oldContent,
        string newContent,
        StoreCodeEditorUserInfo userInfo)
    {
        if (string.IsNullOrWhiteSpace(oldContent)) return;

        var oldLines = SplitLines(oldContent).ToList();
        var newLines = SplitLines(newContent).ToList();
        var lineCount = Math.Max(oldLines.Count, newLines.Count);
        for (var index = 0; index < lineCount; index++)
        {
            var oldLine = index < oldLines.Count ? oldLines[index] : string.Empty;
            var newLine = index < newLines.Count ? newLines[index] : string.Empty;
            if (oldLine == newLine || string.IsNullOrWhiteSpace(oldLine)) continue;

            var changed = GetChangedTextOnly(oldLine, newLine);
            if (string.IsNullOrWhiteSpace(changed.OldValue) &&
                string.IsNullOrWhiteSpace(changed.NewValue))
            {
                continue;
            }

            _context.StoreCodeEditHistories.Add(new StoreCodeEditHistory
            {
                StoreCodeFolderId = folder.Id,
                ManufacturingCompanyId = folder.ManufacturingCompanyId,
                FileName = folder.ManufacturingCompany?.Name,
                LineNumber = index + 1,
                OldValue = changed.OldValue,
                NewValue = changed.NewValue,
                IsRestoreAction = false,
                CreatedAt = DateTime.Now,
                CreatedByUserId = userInfo.UserId,
                CreatedByName = userInfo.DisplayName
            });
        }
    }

    private static (string OldValue, string NewValue) GetChangedTextOnly(
        string oldLine,
        string newLine)
    {
        var prefixLength = 0;
        var maxPrefix = Math.Min(oldLine.Length, newLine.Length);
        while (prefixLength < maxPrefix && oldLine[prefixLength] == newLine[prefixLength])
            prefixLength++;

        var oldSuffix = oldLine.Length - 1;
        var newSuffix = newLine.Length - 1;
        while (oldSuffix >= prefixLength && newSuffix >= prefixLength &&
               oldLine[oldSuffix] == newLine[newSuffix])
        {
            oldSuffix--;
            newSuffix--;
        }

        var oldChanged = oldSuffix >= prefixLength
            ? oldLine.Substring(prefixLength, oldSuffix - prefixLength + 1)
            : string.Empty;
        var newChanged = newSuffix >= prefixLength
            ? newLine.Substring(prefixLength, newSuffix - prefixLength + 1)
            : string.Empty;
        return (oldChanged.Trim(), newChanged.Trim());
    }

    private static string[] SplitLines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

    private static Task SendLockStateAsync(
        IClientProxy client,
        StoreCodeEditorRoomState room) =>
        client.SendAsync("EditingLocked", new
        {
            userId = room.ActiveUserId,
            userName = room.ActiveUserName,
            userImageUrl = room.ActiveUserImageUrl,
            connectionId = room.ActiveConnectionId
        });

    private static StoreCodeEditorRoomState GetRoom(int folderId) =>
        Rooms.GetOrAdd(folderId, _ => new StoreCodeEditorRoomState());

    private static string GetGroupName(int folderId) => $"store-code-file-{folderId}";

    private static void CleanupExpiredLock(StoreCodeEditorRoomState room)
    {
        if (!room.HasActiveWriter || !room.LastTypingAt.HasValue ||
            DateTime.UtcNow - room.LastTypingAt.Value <= LockTimeout)
        {
            return;
        }

        room.ActiveConnectionId = null;
        room.ActiveUserId = null;
        room.ActiveUserName = null;
        room.ActiveUserImageUrl = null;
        room.LastTypingAt = null;
    }

    private static string NormalizeDisplayName(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Contains('@')) text = text.Split('@')[0].Trim();
        return string.IsNullOrWhiteSpace(text) ? "مستخدم آخر" : text;
    }

    private static string NormalizeImageUrl(string? value)
    {
        var text = value?.Trim().Replace('\\', '/') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return "/static/circle-user-solid.svg";
        if (text.StartsWith("~/", StringComparison.Ordinal)) return text[1..];
        if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith('/'))
        {
            return text;
        }

        return "/" + text.TrimStart('/');
    }

    private sealed class StoreCodeEditorRoomState
    {
        public object SyncRoot { get; } = new();
        public string? ActiveConnectionId { get; set; }
        public string? ActiveUserId { get; set; }
        public string? ActiveUserName { get; set; }
        public string? ActiveUserImageUrl { get; set; }
        public DateTime? LastTypingAt { get; set; }
        public bool HasActiveWriter => !string.IsNullOrWhiteSpace(ActiveConnectionId);
    }

    private sealed record StoreCodeEditorUserInfo(
        string UserId,
        string DisplayName,
        string ImageUrl);
}
