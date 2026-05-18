using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Sodalis.Modules.Messaging.Branding;
using Sodalis.Modules.Messaging.Domain;
using Sodalis.Modules.Messaging.Persistence;
using Sodalis.Modules.Messaging.Providers;
using Sodalis.Modules.Messaging.Sending;
using Sodalis.Modules.Messaging.Settings;

namespace Sodalis.Modules.Messaging.UnitTests.Sending;

public class MessageSenderTests
{
    private static readonly Guid TestGameId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task SendEmailVerification_SuccessOnFirstAttempt_CallsProviderOnce()
    {
        var provider = new ProgrammableEmailProvider(attempt => Task.CompletedTask);
        var logger = new RecordingLogger();
        var sender = NewSender(provider, logger);

        await sender.SendEmailVerificationAsync(
            TestGameId, "to@example.test", "Player", "https://example.test/verify", CancellationToken.None);

        var ok = await provider.WaitForCallsAsync(1, TimeSpan.FromSeconds(2));
        ok.ShouldBeTrue();
        provider.CallCount.ShouldBe(1);
        logger.WarningCount.ShouldBe(0);
        logger.ErrorCount.ShouldBe(0);
    }

    [Fact]
    public async Task SendEmailVerification_FailsTwiceThenSucceeds_CallsProviderThreeTimes()
    {
        var provider = new ProgrammableEmailProvider(attempt =>
            attempt < 3 ? Task.FromException(new InvalidOperationException("smtp blip")) : Task.CompletedTask);
        var logger = new RecordingLogger();
        var sender = NewSender(provider, logger);

        await sender.SendEmailVerificationAsync(
            TestGameId, "to@example.test", "Player", "https://example.test/verify", CancellationToken.None);

        // Real backoff is 2s + 4s = 6s before the 3rd attempt fires.
        var ok = await provider.WaitForCallsAsync(3, TimeSpan.FromSeconds(15));
        ok.ShouldBeTrue();
        provider.CallCount.ShouldBe(3);
        logger.WarningCount.ShouldBe(2);
        logger.ErrorCount.ShouldBe(0);
    }

    [Fact]
    public async Task SendEmailVerification_AllAttemptsFail_LogsErrorAndDoesNotThrowToCaller()
    {
        var provider = new ProgrammableEmailProvider(attempt =>
            Task.FromException(new InvalidOperationException("smtp permanently dead")));
        var logger = new RecordingLogger();
        var sender = NewSender(provider, logger);

        var act = () => sender.SendEmailVerificationAsync(
            TestGameId, "to@example.test", "Player", "https://example.test/verify", CancellationToken.None);
        await act.ShouldNotThrowAsync();

        var ok = await provider.WaitForCallsAsync(3, TimeSpan.FromSeconds(15));
        ok.ShouldBeTrue();
        provider.CallCount.ShouldBe(3);

        // wait briefly for the outer-catch LogError to land after the 3rd throw.
        var errorLogged = await WaitUntilAsync(() => logger.ErrorCount >= 1, TimeSpan.FromSeconds(2));
        errorLogged.ShouldBeTrue();
        logger.ErrorCount.ShouldBe(1);
        logger.WarningCount.ShouldBe(2);
    }

    [Fact]
    public async Task SendPasswordReset_PassesResetUrlAndExpiry()
    {
        var provider = new ProgrammableEmailProvider(_ => Task.CompletedTask);
        var sender = NewSender(provider, new RecordingLogger());

        await sender.SendPasswordResetAsync(
            TestGameId, "to@example.test", "Player",
            "https://example.test/reset?t=abc",
            TimeSpan.FromMinutes(30),
            CancellationToken.None);

        var ok = await provider.WaitForCallsAsync(1, TimeSpan.FromSeconds(2));
        ok.ShouldBeTrue();
        var sent = provider.Sent.Single();
        sent.HtmlBody.ShouldContain("https://example.test/reset?t=abc");
        sent.TextBody.ShouldContain("https://example.test/reset?t=abc");
        sent.HtmlBody.ShouldContain("30 minutes");
        sent.Subject.ShouldContain("Reset your");
    }

    [Fact]
    public async Task SendPasswordChanged_PassesChangedAtAndCorrectSubject()
    {
        var provider = new ProgrammableEmailProvider(_ => Task.CompletedTask);
        var sender = NewSender(provider, new RecordingLogger());

        var changedAt = new DateTimeOffset(2026, 5, 18, 12, 34, 56, TimeSpan.Zero);
        await sender.SendPasswordChangedNotificationAsync(
            TestGameId, "to@example.test", "Player", changedAt, CancellationToken.None);

        var ok = await provider.WaitForCallsAsync(1, TimeSpan.FromSeconds(2));
        ok.ShouldBeTrue();
        var sent = provider.Sent.Single();
        sent.HtmlBody.ShouldContain("2026-05-18 12:34:56Z");
        sent.Subject.ShouldContain("password was changed");
    }

    private static MessageSender NewSender(IEmailProvider provider, ILogger<MessageSender> logger)
    {
        var settings = Options.Create(new MessagingSettings
        {
            Smtp = new SmtpSettings { FromAddress = "noreply@example.test", FromName = "From" },
            DefaultBranding = new BrandingDefaults
            {
                BrandName = "TestBrand",
                FooterText = "© Test"
            }
        });

        var dbOptions = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseInMemoryDatabase($"messagesender-{Guid.NewGuid()}")
            .Options;
        var db = new MessagingDbContext(dbOptions);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var resolver = new BrandingResolver(db, cache, settings);
        var renderer = new EmailTemplateRenderer();

        // Fire-and-forget creates a fresh scope and resolves IEmailProvider from
        // it — we wire a tiny container that always hands back the same provider.
        var scopeFactory = new SingletonScopeFactory(provider);

        return new MessageSender(resolver, renderer, scopeFactory, logger);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return true;
            await Task.Delay(25);
        }
        return predicate();
    }

    private sealed class ProgrammableEmailProvider(Func<int, Task> behavior) : IEmailProvider
    {
        private int _callCount;
        private readonly List<EmailMessage> _sent = new();
        private readonly Lock _lock = new();

        public int CallCount => Volatile.Read(ref _callCount);
        public IReadOnlyList<EmailMessage> Sent { get { lock (_lock) return _sent.ToArray(); } }

        public async Task SendAsync(EmailMessage message, CancellationToken ct)
        {
            var attempt = Interlocked.Increment(ref _callCount);
            await behavior(attempt);
            lock (_lock) _sent.Add(message);
        }

        public async Task<bool> WaitForCallsAsync(int expected, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (CallCount >= expected) return true;
                await Task.Delay(25);
            }
            return CallCount >= expected;
        }
    }

    private sealed class RecordingLogger : ILogger<MessageSender>
    {
        private int _warningCount;
        private int _errorCount;

        public int WarningCount => Volatile.Read(ref _warningCount);
        public int ErrorCount => Volatile.Read(ref _errorCount);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            switch (logLevel)
            {
                case LogLevel.Warning: Interlocked.Increment(ref _warningCount); break;
                case LogLevel.Error: Interlocked.Increment(ref _errorCount); break;
            }
        }
    }

    private sealed class SingletonScopeFactory(IEmailProvider provider) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new SingletonScope(provider);

        private sealed class SingletonScope(IEmailProvider provider) : IServiceScope, IServiceProvider
        {
            public IServiceProvider ServiceProvider => this;
            public void Dispose() { }
            public object? GetService(Type serviceType) =>
                serviceType == typeof(IEmailProvider) ? provider : null;
        }
    }
}
