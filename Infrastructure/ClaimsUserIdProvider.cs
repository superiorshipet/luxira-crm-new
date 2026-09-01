using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.SignalR;

namespace Luxira.Api.Infrastructure;

/// <summary>
/// Keeps SignalR user routing aligned with the JWT subject used by the API.
/// </summary>
public sealed class ClaimsUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User.GetUserId();
}
