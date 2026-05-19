using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Sodalis.Core;
using Sodalis.Modules.Tenancy.ApiKeys;
using Sodalis.Modules.Tenancy.Domain;
using Sodalis.Modules.Tenancy.Persistence;

namespace Sodalis.Modules.Tenancy.UnitTests.ApiKeys;

public class ApiKeyMiddlewareTests
{
    private static readonly Guid GameId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private const string RawKey = "sodalis_test_middleware_known_key_aaaaaaaaaa";

    [Fact]
    public async Task Invoke_MissingHeader_Returns401_AndDoesNotCallNext()
    {
        await using var harness = await TestHarness.CreateAsync(seedKey: false);

        await harness.InvokeAsync();

        harness.Context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        harness.NextCalled.ShouldBeFalse();
        harness.GameContext.IsResolved.ShouldBeFalse();

        var problem = await harness.ReadResponseAsync();
        problem.GetProperty("status").GetInt32().ShouldBe(401);
        problem.GetProperty("detail").GetString()!.ShouldContain(ApiKeyMiddleware.HeaderName);
    }

    [Fact]
    public async Task Invoke_EmptyHeader_Returns401()
    {
        await using var harness = await TestHarness.CreateAsync(seedKey: false);
        harness.Context.Request.Headers[ApiKeyMiddleware.HeaderName] = "";

        await harness.InvokeAsync();

        harness.Context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        harness.NextCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task Invoke_WhitespaceHeader_Returns401()
    {
        await using var harness = await TestHarness.CreateAsync(seedKey: false);
        harness.Context.Request.Headers[ApiKeyMiddleware.HeaderName] = "   ";

        await harness.InvokeAsync();

        harness.Context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        harness.NextCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task Invoke_UnknownKey_Returns401_AndDoesNotCallNext()
    {
        await using var harness = await TestHarness.CreateAsync(seedKey: false);
        harness.Context.Request.Headers[ApiKeyMiddleware.HeaderName] = "sodalis_test_not_seeded_xxxxxxxxxxxxxxxxxxxxxx";

        await harness.InvokeAsync();

        harness.Context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        harness.NextCalled.ShouldBeFalse();
        harness.GameContext.IsResolved.ShouldBeFalse();

        var problem = await harness.ReadResponseAsync();
        problem.GetProperty("detail").GetString()!.ShouldContain("Invalid");
    }

    [Fact]
    public async Task Invoke_RevokedKey_Returns401()
    {
        await using var harness = await TestHarness.CreateAsync(seedKey: true, revoked: true);
        harness.Context.Request.Headers[ApiKeyMiddleware.HeaderName] = RawKey;

        await harness.InvokeAsync();

        harness.Context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        harness.NextCalled.ShouldBeFalse();
        harness.GameContext.IsResolved.ShouldBeFalse();
    }

    [Fact]
    public async Task Invoke_ValidKey_SetsGameContext_AndCallsNext()
    {
        await using var harness = await TestHarness.CreateAsync(seedKey: true);
        harness.Context.Request.Headers[ApiKeyMiddleware.HeaderName] = RawKey;

        await harness.InvokeAsync();

        harness.NextCalled.ShouldBeTrue();
        harness.GameContext.IsResolved.ShouldBeTrue();
        harness.GameContext.GameId.ShouldBe(GameId);
        harness.Context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    private sealed class TestHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _root;
        private readonly IServiceScope _requestScope;
        private readonly MemoryStream _responseBody;

        public DefaultHttpContext Context { get; }
        public GameContext GameContext { get; }
        public ApiKeyResolver Resolver { get; }
        public ApiKeyMiddleware Middleware { get; }
        public bool NextCalled { get; private set; }

        private TestHarness(
            ServiceProvider root,
            IServiceScope requestScope,
            MemoryStream responseBody,
            DefaultHttpContext context,
            ApiKeyResolver resolver)
        {
            _root = root;
            _requestScope = requestScope;
            _responseBody = responseBody;
            Context = context;
            Resolver = resolver;
            GameContext = new GameContext();
            Middleware = new ApiKeyMiddleware(
                next: _ =>
                {
                    NextCalled = true;
                    Context.Response.StatusCode = StatusCodes.Status200OK;
                    return Task.CompletedTask;
                },
                logger: NullLogger<ApiKeyMiddleware>.Instance);
        }

        public Task InvokeAsync() => Middleware.InvokeAsync(Context, GameContext, Resolver);

        public async Task<JsonElement> ReadResponseAsync()
        {
            _responseBody.Position = 0;
            using var doc = await JsonDocument.ParseAsync(_responseBody);
            return doc.RootElement.Clone();
        }

        public static async Task<TestHarness> CreateAsync(bool seedKey, bool revoked = false)
        {
            var dbName = $"middleware-{Guid.NewGuid()}";

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<TenancyDbContext>(o => o.UseInMemoryDatabase(dbName));
            services.AddMemoryCache();
            services.AddScoped<ApiKeyResolver>();
            var root = services.BuildServiceProvider();

            if (seedKey)
            {
                using var seedScope = root.CreateScope();
                var db = seedScope.ServiceProvider.GetRequiredService<TenancyDbContext>();
                db.Games.Add(new Game { GameId = GameId, Name = "G", IsActive = true });
                db.GameApiKeys.Add(new GameApiKey
                {
                    KeyHash = ApiKeyHasher.Hash(RawKey),
                    GameId = GameId,
                    Prefix = ApiKeyHasher.Prefix(RawKey),
                    Name = "default",
                    CreatedAt = DateTimeOffset.UtcNow,
                    RevokedAt = revoked ? DateTimeOffset.UtcNow : null
                });
                await db.SaveChangesAsync();
            }

            var requestScope = root.CreateScope();
            var resolver = requestScope.ServiceProvider.GetRequiredService<ApiKeyResolver>();

            var responseBody = new MemoryStream();
            var ctx = new DefaultHttpContext
            {
                RequestServices = requestScope.ServiceProvider
            };
            ctx.Response.Body = responseBody;

            return new TestHarness(root, requestScope, responseBody, ctx, resolver);
        }

        public async ValueTask DisposeAsync()
        {
            _requestScope.Dispose();
            await _responseBody.DisposeAsync();
            await _root.DisposeAsync();
        }
    }
}
