using System.ComponentModel.DataAnnotations;
using Luxira.Api.Features.Communication.DTOs;
using Luxira.Api.Features.Communication.Models;
using Luxira.Api.Features.Communication.Repositories;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Time;

namespace Luxira.Api.Features.Communication.Services;

public sealed class PasswordEmailService(PasswordEmailRepository repository)
{
    public async Task<List<PasswordEmailDto>> ListAsync(
        bool deleted,
        string? email,
        int? storeId,
        CancellationToken ct) =>
        (await repository.ListAsync(deleted, email, storeId, ct)).Select(Map).ToList();

    public async Task<PasswordEmailDto> GetAsync(int id, CancellationToken ct)
    {
        var item = await repository.GetAsync(id, false, ct)
            ?? throw new NotFoundException("Password email was not found.");
        return Map(item);
    }

    public async Task<PasswordEmailDto> CreateAsync(
        SavePasswordEmailRequest request,
        PasswordEmailActor actor,
        CancellationToken ct)
    {
        var values = await ValidateAsync(request, null, ct);
        var now = IstanbulTimeHelper.Now;
        var item = new PasswordEmail
        {
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            Email = values.Email,
            Password = values.Password,
            PhoneNumber = values.PhoneNumber,
            PageStatusName = values.PageStatusName,
            CreatedAt = now,
            CreatedByUserId = actor.UserId,
            CreatedByName = actor.Name
        };
        item.Histories.Add(CreateHistory(item, "Create", actor, now, includeOld: false));
        await repository.AddAsync(item, ct);
        return Map(item);
    }

    public async Task<PasswordEmailDto> UpdateAsync(
        int id,
        SavePasswordEmailRequest request,
        PasswordEmailActor actor,
        CancellationToken ct)
    {
        var item = await repository.GetAsync(id, false, ct)
            ?? throw new NotFoundException("Password email was not found.");
        var values = await ValidateAsync(request, id, ct);
        var now = IstanbulTimeHelper.Now;
        var history = CreateHistory(item, "Edit", actor, now, includeOld: true);

        item.ManufacturingCompanyId = request.ManufacturingCompanyId;
        item.Email = values.Email;
        item.Password = values.Password;
        item.PhoneNumber = values.PhoneNumber;
        item.PageStatusName = values.PageStatusName;
        item.UpdatedAt = now;
        item.UpdatedByUserId = actor.UserId;
        item.UpdatedByName = actor.Name;
        SetNewValues(history, item);

        await repository.SaveAsync(history, ct);
        return Map(item);
    }

    public async Task DeleteAsync(int id, PasswordEmailActor actor, CancellationToken ct)
    {
        var item = await repository.GetAsync(id, false, ct)
            ?? throw new NotFoundException("Password email was not found.");
        var now = IstanbulTimeHelper.Now;
        item.IsDeleted = true;
        item.DeletedAt = now;
        item.DeletedByUserId = actor.UserId;
        item.DeletedByName = actor.Name;
        await repository.SaveAsync(CreateHistory(item, "Delete", actor, now, includeOld: true), ct);
    }

    public async Task RestoreAsync(int id, PasswordEmailActor actor, CancellationToken ct)
    {
        var item = await repository.GetAsync(id, true, ct)
            ?? throw new NotFoundException("Deleted password email was not found.");
        var now = IstanbulTimeHelper.Now;
        item.IsDeleted = false;
        item.DeletedAt = null;
        item.DeletedByUserId = null;
        item.DeletedByName = null;
        item.UpdatedAt = now;
        item.UpdatedByUserId = actor.UserId;
        item.UpdatedByName = actor.Name;
        await repository.SaveAsync(CreateHistory(item, "Restore", actor, now, includeOld: false), ct);
    }

    public async Task PermanentlyDeleteAsync(int id, CancellationToken ct)
    {
        var item = await repository.GetAsync(id, true, ct)
            ?? throw new NotFoundException("Deleted password email was not found.");
        await repository.PermanentlyDeleteAsync(item, ct);
    }

    public async Task<List<PasswordEmailHistoryDto>> ListHistoryAsync(int? itemId, CancellationToken ct) =>
        (await repository.ListHistoryAsync(itemId, ct)).Select(history => new PasswordEmailHistoryDto(
            history.Id,
            history.PasswordEmailId,
            history.ActionType,
            history.OldEmail,
            history.NewEmail,
            history.OldPassword,
            history.NewPassword,
            history.OldPhoneNumber,
            history.NewPhoneNumber,
            history.OldPageStatusName,
            history.NewPageStatusName,
            history.ChangedAt,
            history.ChangedByName)).ToList();

    private async Task<(string Email, string Password, string? PhoneNumber, string? PageStatusName)> ValidateAsync(
        SavePasswordEmailRequest request,
        int? excludedId,
        CancellationToken ct)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        var password = request.Password?.Trim() ?? string.Empty;
        if (!new EmailAddressAttribute().IsValid(email) || email.Length > 256)
            throw new BadRequestException("A valid email address is required.");
        if (password.Length is < 1 or > 500)
            throw new BadRequestException("Password is required and cannot exceed 500 characters.");
        if (request.PhoneNumber?.Length > 80 || request.PageStatusName?.Length > 200)
            throw new BadRequestException("Phone number or page status is too long.");
        if (request.ManufacturingCompanyId <= 0 ||
            !await repository.StoreExistsAsync(request.ManufacturingCompanyId, ct))
            throw new BadRequestException("Manufacturing company was not found.");
        if (await repository.EmailExistsAsync(email, excludedId, ct))
            throw new BadRequestException("This email already exists.");
        return (email, password, request.PhoneNumber?.Trim(), request.PageStatusName?.Trim());
    }

    private static PasswordEmailHistory CreateHistory(
        PasswordEmail item,
        string action,
        PasswordEmailActor actor,
        DateTime now,
        bool includeOld)
    {
        var history = new PasswordEmailHistory
        {
            PasswordEmailId = item.Id,
            ActionType = action,
            ChangedAt = now,
            ChangedByUserId = actor.UserId,
            ChangedByName = actor.Name
        };
        if (includeOld)
        {
            history.OldEmail = item.Email;
            history.OldPassword = item.Password;
            history.OldPhoneNumber = item.PhoneNumber;
            history.OldPageStatusName = item.PageStatusName;
        }
        else
        {
            SetNewValues(history, item);
        }
        return history;
    }

    private static void SetNewValues(PasswordEmailHistory history, PasswordEmail item)
    {
        history.NewEmail = item.Email;
        history.NewPassword = item.Password;
        history.NewPhoneNumber = item.PhoneNumber;
        history.NewPageStatusName = item.PageStatusName;
    }

    private static PasswordEmailDto Map(PasswordEmail item) => new(
        item.Id,
        item.ManufacturingCompanyId,
        item.ManufacturingCompany?.Name,
        item.Email,
        item.Password,
        item.PhoneNumber,
        item.PageStatusName,
        item.IsDeleted,
        item.CreatedAt,
        item.UpdatedAt,
        item.DeletedAt,
        item.UpdatedByName ?? item.DeletedByName ?? item.CreatedByName);
}
