using Shouldly;
using Sodalis.Modules.Messaging.Sending;

namespace Sodalis.Modules.Messaging.UnitTests.Sending;

public class EmailTemplateRendererTests
{
    private readonly EmailTemplateRenderer _renderer = new();

    [Theory]
    [InlineData(TemplateKind.EmailVerification)]
    [InlineData(TemplateKind.PasswordReset)]
    [InlineData(TemplateKind.PasswordChanged)]
    public void Render_Html_ReturnsNonEmpty_ForEveryKind(TemplateKind kind)
    {
        var html = _renderer.Render(kind, TemplateFormat.Html, FullVars());

        html.ShouldNotBeNullOrWhiteSpace();
        html.ShouldContain("<html", Case.Insensitive);
    }

    [Theory]
    [InlineData(TemplateKind.EmailVerification)]
    [InlineData(TemplateKind.PasswordReset)]
    [InlineData(TemplateKind.PasswordChanged)]
    public void Render_Text_ReturnsNonEmpty_ForEveryKind(TemplateKind kind)
    {
        var text = _renderer.Render(kind, TemplateFormat.Text, FullVars());

        text.ShouldNotBeNullOrWhiteSpace();
        text.ShouldNotContain("<html", Case.Insensitive);
    }

    [Fact]
    public void Render_SubstitutesVariables()
    {
        var vars = FullVars();
        vars["player_name"] = "Alice";
        vars["brand_name"] = "AcmeGame";
        vars["verification_url"] = "https://example.test/verify?t=abc";

        var html = _renderer.Render(TemplateKind.EmailVerification, TemplateFormat.Html, vars);

        html.ShouldContain("Alice");
        html.ShouldContain("AcmeGame");
        html.ShouldContain("https://example.test/verify?t=abc");
    }

    [Fact]
    public void Render_LeavesNoUnresolvedPlaceholders_WhenAllVarsProvided()
    {
        var html = _renderer.Render(TemplateKind.EmailVerification, TemplateFormat.Html, FullVars());

        html.ShouldNotMatch(@"\{\{[#/]?\w+\}\}");
    }

    [Fact]
    public void Render_MissingVariable_BecomesEmptyString()
    {
        var vars = MinimalVars();
        vars.Remove("verification_url");

        var html = _renderer.Render(TemplateKind.EmailVerification, TemplateFormat.Html, vars);

        html.ShouldNotContain("{{verification_url}}");
    }

    [Fact]
    public void Render_BlockKept_WhenVariableIsPresentAndNonEmpty()
    {
        var vars = MinimalVars();
        vars["support_url"] = "https://support.example.test";

        var html = _renderer.Render(TemplateKind.EmailVerification, TemplateFormat.Html, vars);

        html.ShouldContain("https://support.example.test");
        html.ShouldContain("Contact support");
    }

    [Fact]
    public void Render_BlockRemoved_WhenVariableMissing()
    {
        var vars = MinimalVars();
        vars.Remove("support_url");

        var html = _renderer.Render(TemplateKind.EmailVerification, TemplateFormat.Html, vars);

        html.ShouldNotContain("Contact support");
    }

    [Fact]
    public void Render_BlockRemoved_WhenVariableIsEmptyString()
    {
        var vars = MinimalVars();
        vars["support_url"] = "";

        var html = _renderer.Render(TemplateKind.EmailVerification, TemplateFormat.Html, vars);

        html.ShouldNotContain("Contact support");
    }

    [Fact]
    public void Render_BlockRemoved_WhenVariableIsNull()
    {
        var vars = MinimalVars();
        vars["support_url"] = null;

        var html = _renderer.Render(TemplateKind.EmailVerification, TemplateFormat.Html, vars);

        html.ShouldNotContain("Contact support");
    }

    [Fact]
    public void GetSubject_UsesBrandName_WhenProvided()
    {
        var subject = _renderer.GetSubject(
            TemplateKind.EmailVerification,
            new Dictionary<string, string?> { ["brand_name"] = "AcmeGame" });

        subject.ShouldBe("Verify your email for AcmeGame");
    }

    [Fact]
    public void GetSubject_FallsBackToSodalis_WhenBrandMissing()
    {
        var subject = _renderer.GetSubject(
            TemplateKind.PasswordReset,
            new Dictionary<string, string?>());

        subject.ShouldBe("Reset your Sodalis password");
    }

    [Fact]
    public void GetSubject_FallsBackToSodalis_WhenBrandEmpty()
    {
        var subject = _renderer.GetSubject(
            TemplateKind.PasswordChanged,
            new Dictionary<string, string?> { ["brand_name"] = "" });

        subject.ShouldBe("Your Sodalis password was changed");
    }

    [Fact]
    public void Render_DoesNotHtmlEscape_SubstitutedValues_DocumentedBehavior()
    {
        // The renderer does not HTML-escape — values land verbatim in the html
        // template. If we ever surface user-controlled strings (DisplayName) into
        // a template, that variable will need escaping in MessageSender before
        // it reaches here. This test pins the current behavior so we notice if
        // it changes.
        var vars = FullVars();
        vars["player_name"] = "<script>alert(1)</script>";

        var html = _renderer.Render(TemplateKind.EmailVerification, TemplateFormat.Html, vars);

        html.ShouldContain("<script>alert(1)</script>");
    }

    private static Dictionary<string, string?> FullVars() => new()
    {
        ["brand_name"] = "TestBrand",
        ["player_name"] = "Tester",
        ["primary_color"] = "#112233",
        ["logo_url"] = "https://cdn.example.test/logo.png",
        ["support_url"] = "https://support.example.test",
        ["footer_text"] = "© TestBrand",
        ["verification_url"] = "https://example.test/verify?t=token",
        ["reset_url"] = "https://example.test/reset?t=token",
        ["expires_in"] = "1 hour",
        ["changed_at_utc"] = "2026-05-18 12:00:00Z"
    };

    private static Dictionary<string, string?> MinimalVars() => new()
    {
        ["brand_name"] = "TestBrand",
        ["player_name"] = "Tester",
        ["primary_color"] = "#112233",
        ["footer_text"] = "© TestBrand",
        ["verification_url"] = "https://example.test/verify?t=token"
    };
}
