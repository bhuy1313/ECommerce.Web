using ECommerce.Web.Models.Entities;
using ECommerce.Web.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECommerce.Web.Tests.Services;

/// <summary>
/// Khóa brute-force: policy mật khẩu + lockout sau N lần sai.
/// UserManager thật trên sqlite, không mock.
/// </summary>
public class AccountSecurityTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public AccountSecurityTests()
    {
        (_, _connection) = TestDbContextFactory.CreateInMemoryDbContext();
    }

    private static UserManager<ApplicationUser> CreateUserManager(
        ECommerce.Web.Data.ApplicationDbContext db, Action<IdentityOptions>? configure = null)
    {
        var options = new IdentityOptions
        {
            Lockout =
            {
                AllowedForNewUsers = true,
                MaxFailedAccessAttempts = 5,
                DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5)
            }
        };
        configure?.Invoke(options);

        return new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser>(db),
            Options.Create(options),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> users)
    {
        var user = new ApplicationUser { UserName = "lock@test.local", Email = "lock@test.local" };
        var created = await users.CreateAsync(user, "Correct@123");
        Assert.True(created.Succeeded);
        return user;
    }

    /// <summary>Mô phỏng 1 lần đăng nhập sai như SignInManager: check + ghi nhận thất bại.</summary>
    private static async Task<bool> AttemptLoginAsync(
        UserManager<ApplicationUser> users, ApplicationUser user, string password)
    {
        if (await users.IsLockedOutAsync(user))
        {
            return false;
        }

        if (await users.CheckPasswordAsync(user, password))
        {
            await users.ResetAccessFailedCountAsync(user);
            return true;
        }

        await users.AccessFailedAsync(user);
        return false;
    }

    [Fact]
    public async Task FiveFailedLogins_LocksAccountOut()
    {
        var (db, _) = TestDbContextFactory.CreateInMemoryDbContext();
        using var _ = db;
        var users = CreateUserManager(db);
        var user = await CreateUserAsync(users);

        for (var i = 0; i < 5; i++)
        {
            Assert.False(await AttemptLoginAsync(users, user, "Wrong@123"));
        }

        Assert.True(await users.IsLockedOutAsync(user));
    }

    [Fact]
    public async Task CorrectPasswordWhileLockedOut_StillRejected()
    {
        var (db, _) = TestDbContextFactory.CreateInMemoryDbContext();
        using var _ = db;
        var users = CreateUserManager(db);
        var user = await CreateUserAsync(users);

        for (var i = 0; i < 5; i++)
        {
            await AttemptLoginAsync(users, user, "Wrong@123");
        }

        Assert.True(await users.IsLockedOutAsync(user));
        Assert.False(await AttemptLoginAsync(users, user, "Correct@123"));
    }

    [Fact]
    public async Task FewerThanFiveFailures_DoesNotLock()
    {
        var (db, _) = TestDbContextFactory.CreateInMemoryDbContext();
        using var _ = db;
        var users = CreateUserManager(db);
        var user = await CreateUserAsync(users);

        for (var i = 0; i < 4; i++)
        {
            await AttemptLoginAsync(users, user, "Wrong@123");
        }

        Assert.False(await users.IsLockedOutAsync(user));
        Assert.True(await AttemptLoginAsync(users, user, "Correct@123"));
    }

    [Theory]
    [InlineData("short1A", false)] // dưới 8 ký tự
    [InlineData("nouppercase@123", false)] // thiếu hoa (policy mới)
    [InlineData("NOLOWERCASE@123", false)] // thiếu thường (policy mới)
    [InlineData("NoDigitHere@", false)] // thiếu số
    [InlineData("Valid@123", true)]
    public async Task PasswordValidator_EnforcesConfiguredPolicy(string password, bool expectedValid)
    {
        var (db, _) = TestDbContextFactory.CreateInMemoryDbContext();
        using var _ = db;
        // Options mirror chính sách trong Program.cs (8 + số + hoa + thường).
        var users = CreateUserManager(db, o =>
        {
            o.Password.RequiredLength = 8;
            o.Password.RequireDigit = true;
            o.Password.RequireLowercase = true;
            o.Password.RequireUppercase = true;
            o.Password.RequireNonAlphanumeric = false;
        });
        var validator = new PasswordValidator<ApplicationUser>();
        var user = new ApplicationUser { UserName = "pw@test.local", Email = "pw@test.local" };

        var result = await validator.ValidateAsync(users, user, password);

        Assert.Equal(expectedValid, result.Succeeded);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection.Dispose();
            _disposed = true;
        }
    }
}
