namespace Luxira.Api.Features.Communication.DTOs;

public record PasswordEmailDto(
    int Id,
    int? ManufacturingCompanyId,
    string? ManufacturingCompanyName,
    string Email,
    string Password,
    string? PhoneNumber,
    string? PageStatusName,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? DeletedAt,
    string? LastChangedByName);

public record SavePasswordEmailRequest(
    int ManufacturingCompanyId,
    string Email,
    string Password,
    string? PhoneNumber,
    string? PageStatusName);

public record PasswordEmailHistoryDto(
    int Id,
    int PasswordEmailId,
    string ActionType,
    string? OldEmail,
    string? NewEmail,
    string? OldPassword,
    string? NewPassword,
    string? OldPhoneNumber,
    string? NewPhoneNumber,
    string? OldPageStatusName,
    string? NewPageStatusName,
    DateTime ChangedAt,
    string? ChangedByName);

public readonly record struct PasswordEmailActor(string UserId, string? Name);
