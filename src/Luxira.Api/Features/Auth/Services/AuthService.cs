using Luxira.Api.Features.Auth.DTOs;
using Luxira.Api.Features.Auth.Models;
using Luxira.Api.Features.Auth.Repositories;
using Luxira.Api.Utils.Exceptions;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Security.Cryptography;

namespace Luxira.Api.Features.Auth.Services;

public class AuthService
{
    private readonly UserRepository _userRepository;
    private readonly JwtService _jwtService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(UserRepository userRepository, JwtService jwtService, ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByUsernameAsync(request.Username, ct);
        if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            throw new ForbidException("Account is disabled. Please contact administrator.");
        }

        var (token, expiresAt) = _jwtService.GenerateToken(user);
        var userDto = new UserDto(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email,
            user.Name,
            user.Country,
            user.AcessId,
            user.Role,
            user.IsActive
        );

        return new AuthResponse(token, Guid.NewGuid().ToString(), expiresAt, userDto);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var existing = await _userRepository.GetByUsernameAsync(request.Username, ct);
        if (existing != null)
        {
            throw new BadRequestException("Username or email is already registered.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Username,
            NormalizedUserName = request.Username.ToUpperInvariant(),
            Email = request.Email,
            NormalizedEmail = request.Email.ToUpperInvariant(),
            Name = request.Name,
            Country = request.Country,
            AcessId = request.AccessId,
            PasswordHash = HashPassword(request.Password),
            IsActive = true
        };

        await _userRepository.AddAsync(user, ct);
        var (token, expiresAt) = _jwtService.GenerateToken(user);

        var userDto = new UserDto(
            user.Id,
            user.UserName,
            user.Email,
            user.Name,
            user.Country,
            user.AcessId,
            user.Role,
            user.IsActive
        );

        return new AuthResponse(token, Guid.NewGuid().ToString(), expiresAt, userDto);
    }

    public async Task<AuthResponse> SwitchUserAsync(string currentUserId, string targetUserId, CancellationToken ct = default)
    {
        var targetUser = await _userRepository.GetByIdAsync(targetUserId, ct);
        if (targetUser == null || !targetUser.IsActive)
        {
            throw new NotFoundException("Target user not found or inactive.");
        }

        var groups = await _userRepository.GetSwitchGroupsForUserAsync(currentUserId, ct);
        bool hasAccess = groups.Any(g => g.Members.Any(m => m.UserId == targetUserId) || g.CreatedByUserId == targetUserId);
        
        if (!hasAccess)
        {
            _logger.LogWarning("Unauthorized user switch attempt from {CurrentUserId} to {TargetUserId}", currentUserId, targetUserId);
            throw new ForbidException("You do not have permission to switch to this account.");
        }

        var (token, expiresAt) = _jwtService.GenerateToken(targetUser);
        var userDto = new UserDto(
            targetUser.Id,
            targetUser.UserName ?? string.Empty,
            targetUser.Email,
            targetUser.Name,
            targetUser.Country,
            targetUser.AcessId,
            targetUser.Role,
            targetUser.IsActive
        );

        return new AuthResponse(token, Guid.NewGuid().ToString(), expiresAt, userDto);
    }

    private static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(128 / 8);
        string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 100000,
            numBytesRequested: 256 / 8));
        return $"{Convert.ToBase64String(salt)}.{hashed}";
    }

    private static bool VerifyPassword(string password, string? storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;
        var parts = storedHash.Split('.');
        if (parts.Length != 2) return false;

        byte[] salt = Convert.FromBase64String(parts[0]);
        string hash = parts[1];

        string computedHash = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 100000,
            numBytesRequested: 256 / 8));

        return hash == computedHash;
    }
}
