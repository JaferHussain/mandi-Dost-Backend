using MandiDost.Api.Entities;
using Microsoft.AspNetCore.Identity;

namespace MandiDost.Api.Auth;

public interface IPasswordService
{
    string Hash(User user, string password);
    PasswordVerificationResult Verify(User user, string hash, string password);
    /// <summary>Runs a verification against a fixed hash so "unknown phone" takes as long as "wrong password".</summary>
    void SimulateVerify(string password);
    bool IsTooWeak(string password);
}

/// <summary>
/// Uses ASP.NET Core Identity's hasher: PBKDF2 with HMAC-SHA512, 100,000 iterations, random salt,
/// versioned format (old hashes are upgraded on login via SuccessRehashNeeded).
/// </summary>
public class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _hasher = new();
    private readonly string _dummyHash;

    // Passwords that are trivially guessable – rejected at registration.
    private static readonly HashSet<string> Common =
    [
        "123456", "1234567", "12345678", "123456789", "1234567890", "654321", "111111", "000000",
        "password", "pakistan", "qwerty", "abc123", "112233", "786786", "786000",
    ];

    public PasswordService() => _dummyHash = _hasher.HashPassword(new User(), Guid.NewGuid().ToString());

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    public PasswordVerificationResult Verify(User user, string hash, string password) =>
        _hasher.VerifyHashedPassword(user, hash, password);

    public void SimulateVerify(string password) => _hasher.VerifyHashedPassword(new User(), _dummyHash, password);

    public bool IsTooWeak(string password) =>
        Common.Contains(password.ToLowerInvariant()) || password.Distinct().Count() <= 2;
}
