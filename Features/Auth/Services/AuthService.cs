using Luxira.Api.Features.Auth.DTOs;
using Luxira.Api.Features.Auth.Models;
using Luxira.Api.Features.Auth.Repositories;
using Luxira.Api.Utils.Exceptions;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;

namespace Luxira.Api.Features.Auth.Services;

public class AuthService
{
    private readonly UserRepository _userRepository;
    private readonly JwtService _jwtService;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserRepository userRepository,
        JwtService jwtService,
        IPasswordHasher<ApplicationUser> passwordHasher,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByUsernameAsync(request.Username, ct);
        if (user == null || !VerifyPassword(user, request.Password))
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
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            throw new BadRequestException("Username, email, and password are required.");
        }

        if (request.Password.Length < 8)
        {
            throw new BadRequestException("Password must be at least 8 characters.");
        }

        var normalizedUsername = request.Username.Trim().ToUpperInvariant();
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var existing = await _userRepository.FindByNormalizedIdentityAsync(
            normalizedUsername,
            normalizedEmail,
            ct);
        if (existing is not null)
        {
            throw new BadRequestException("Username or email is already registered.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Username.Trim(),
            NormalizedUserName = normalizedUsername,
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            Name = request.Name,
            Country = request.Country,
            AcessId = request.AccessId,
            Role = string.IsNullOrWhiteSpace(request.Role) ? "Employee" : request.Role.Trim(),
            IsActive = true
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

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

    private bool VerifyPassword(
        ApplicationUser user,
        string password)
    {
        var storedHash = user.PasswordHash;
        if (string.IsNullOrEmpty(storedHash)) return false;

        if (!storedHash.Contains('.', StringComparison.Ordinal))
        {
            return _passwordHasher.VerifyHashedPassword(
                       user,
                       storedHash,
                       password) is PasswordVerificationResult.Success or
                           PasswordVerificationResult.SuccessRehashNeeded;
        }

        // Compatibility for accounts created by the first .NET 10 scaffold.
        var parts = storedHash.Split('.');
        if (parts.Length != 2) return false;

        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expectedHash = Convert.FromBase64String(parts[1]);
            var computedHash = KeyDerivation.Pbkdf2(
                password,
                salt,
                KeyDerivationPrf.HMACSHA256,
                100_000,
                256 / 8);

            return CryptographicOperations.FixedTimeEquals(
                expectedHash,
                computedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
