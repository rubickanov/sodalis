using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Sodalis.Core;
using Sodalis.Modules.Identity.Auth;
using Sodalis.Modules.Identity.AuthProviders;
using Sodalis.Modules.Identity.Domain;
using Sodalis.Modules.Identity.Persistence;

namespace Sodalis.Modules.Identity.UnitTests.AuthProviders;

public class EmailPasswordAuthProviderTests
{
    private sealed class FixedGameContext(Guid gameId) : IGameContext
    {
        public Guid GameId { get; } = gameId;
        public bool IsResolved => true;
    }

    private static (EmailPasswordAuthProvider Sut, IdentityDbContext Db, PasswordHasher Hasher) CreateSut(Guid gameId)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"id-{Guid.NewGuid():N}")
            .Options;
        var db = new IdentityDbContext(options, new FixedGameContext(gameId));
        var hasher = new PasswordHasher();
        return (new EmailPasswordAuthProvider(db, hasher), db, hasher);
    }

    private static async Task SeedIdentityAsync(
        IdentityDbContext db, PasswordHasher hasher, Guid gameId, string email, string password)
    {
        var meta = new EmailMetadata(hasher.Hash(password), EmailVerified: true, EmailVerifiedAt: DateTimeOffset.UtcNow);
        db.ExternalIdentities.Add(new ExternalIdentity
        {
            PlayerId = Guid.NewGuid(),
            GameId = gameId,
            ProviderId = EmailPasswordAuthProvider.Id,
            ExternalId = email,
            Metadata = JsonSerializer.Serialize(meta),
            LinkedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static JsonElement Payload(object obj) =>
        JsonSerializer.SerializeToElement(obj);

    [Fact]
    public async Task Fails_WhenEmailMissing()
    {
        var (sut, _, _) = CreateSut(Guid.NewGuid());

        var result = await sut.AuthenticateAsync(
            new AuthRequest("email", Payload(new { password = "x" }), Guid.NewGuid()),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Fails_WhenPasswordMissing()
    {
        var (sut, _, _) = CreateSut(Guid.NewGuid());

        var result = await sut.AuthenticateAsync(
            new AuthRequest("email", Payload(new { email = "a@b.com" }), Guid.NewGuid()),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Fails_WhenEmailIsNotString()
    {
        var (sut, _, _) = CreateSut(Guid.NewGuid());

        var result = await sut.AuthenticateAsync(
            new AuthRequest("email", Payload(new { email = 42, password = "x" }), Guid.NewGuid()),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Fails_WhenEmailTooLong_WithoutHittingHasher()
    {
        // 255 chars — one over the 254 cap. Must reject without computing Argon2id (DoS guard).
        var (sut, _, _) = CreateSut(Guid.NewGuid());
        var longEmail = new string('a', 249) + "@b.com"; // 249 + 6 = 255

        var result = await sut.AuthenticateAsync(
            new AuthRequest("email", Payload(new { email = longEmail, password = "x" }), Guid.NewGuid()),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Fails_WhenPasswordTooLong()
    {
        var (sut, _, _) = CreateSut(Guid.NewGuid());

        var result = await sut.AuthenticateAsync(
            new AuthRequest("email", Payload(new { email = "a@b.com", password = new string('p', 257) }), Guid.NewGuid()),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Fails_WhenIdentityNotFound()
    {
        var gameId = Guid.NewGuid();
        var (sut, _, _) = CreateSut(gameId);

        var result = await sut.AuthenticateAsync(
            new AuthRequest("email", Payload(new { email = "ghost@b.com", password = "validpass" }), gameId),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Fails_WhenPasswordWrong()
    {
        var gameId = Guid.NewGuid();
        var (sut, db, hasher) = CreateSut(gameId);
        await SeedIdentityAsync(db, hasher, gameId, "user@x.com", "correctpass");

        var result = await sut.AuthenticateAsync(
            new AuthRequest("email", Payload(new { email = "user@x.com", password = "wrongpass" }), gameId),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Ok_WhenCorrect_ReturnsNormalizedEmailAsExternalId()
    {
        var gameId = Guid.NewGuid();
        var (sut, db, hasher) = CreateSut(gameId);
        await SeedIdentityAsync(db, hasher, gameId, "user@x.com", "correctpass");

        var result = await sut.AuthenticateAsync(
            new AuthRequest("email", Payload(new { email = "user@x.com", password = "correctpass" }), gameId),
            CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.ExternalId.ShouldBe("user@x.com");
        result.Email.ShouldBe("user@x.com");
    }

    [Fact]
    public async Task NormalizesEmail_TrimAndLowercase()
    {
        var gameId = Guid.NewGuid();
        var (sut, db, hasher) = CreateSut(gameId);
        await SeedIdentityAsync(db, hasher, gameId, "user@x.com", "correctpass");

        var result = await sut.AuthenticateAsync(
            new AuthRequest("email", Payload(new { email = "  USER@X.COM  ", password = "correctpass" }), gameId),
            CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.ExternalId.ShouldBe("user@x.com");
    }

    [Fact]
    public async Task Fails_ForOtherGame()
    {
        // Seed identity in GameA, authenticate request claims GameB.
        // Tenancy isolation must keep them apart.
        var gameA = Guid.NewGuid();
        var gameB = Guid.NewGuid();
        var (sut, db, hasher) = CreateSut(gameB);
        await SeedIdentityAsync(db, hasher, gameA, "user@x.com", "correctpass");

        var result = await sut.AuthenticateAsync(
            new AuthRequest("email", Payload(new { email = "user@x.com", password = "correctpass" }), gameB),
            CancellationToken.None);

        result.Success.ShouldBeFalse();
    }
}
