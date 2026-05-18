using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Sodalis.Modules.Messaging.Sending;

/// <summary>
/// Loads email templates from embedded resources and renders them with a small,
/// regex-based substitution syntax:
///   {{var}}          — replaced with the value (empty string if missing).
///   {{#var}}…{{/var}} — block kept only if `var` is present and non-empty.
/// </summary>
/// <remarks>
/// Sufficient for the three transactional templates we have. If templates ever
/// need loops or conditionals, swap this for Scriban — the public API
/// (<see cref="Render"/>) stays the same.
/// </remarks>
public sealed class EmailTemplateRenderer
{
    private static readonly Regex VariablePattern =
        new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    // TODO(footgun): non-greedy block match doesn't nest correctly when two
    // blocks share the same variable name (`{{#x}} … {{#x}} … {{/x}} … {{/x}}`).
    // The inner `{{/x}}` would close the outer block. Not used by current
    // templates, but if someone ever writes nested same-name blocks they'll get
    // silent garbage. When that day comes, swap to Scriban (or write a real
    // stack-based parser).
    private static readonly Regex BlockPattern =
        new(@"\{\{#(\w+)\}\}(.*?)\{\{/\1\}\}", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly ConcurrentDictionary<string, string> ResourceCache = new();

    private readonly Assembly _assembly = typeof(EmailTemplateRenderer).Assembly;

    public string Render(TemplateKind kind, TemplateFormat format, IReadOnlyDictionary<string, string?> vars)
    {
        var template = LoadTemplate(kind, format);

        // Process blocks first so {{var}} substitution doesn't accidentally
        // wipe the block markers.
        var withBlocks = BlockPattern.Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            return vars.TryGetValue(name, out var v) && !string.IsNullOrEmpty(v)
                ? match.Groups[2].Value
                : "";
        });

        return VariablePattern.Replace(withBlocks, match =>
        {
            var name = match.Groups[1].Value;
            return vars.TryGetValue(name, out var v) && v is not null ? v : "";
        });
    }

    public string GetSubject(TemplateKind kind, IReadOnlyDictionary<string, string?> vars)
    {
        var brand = vars.TryGetValue("brand_name", out var b) && !string.IsNullOrEmpty(b) ? b : "Sodalis";
        return kind switch
        {
            TemplateKind.EmailVerification => $"Verify your email for {brand}",
            TemplateKind.PasswordReset => $"Reset your {brand} password",
            TemplateKind.PasswordChanged => $"Your {brand} password was changed",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private string LoadTemplate(TemplateKind kind, TemplateFormat format)
    {
        var resourceName = ResourceName(kind, format);
        return ResourceCache.GetOrAdd(resourceName, name =>
        {
            using var stream = _assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException(
                    $"Email template '{name}' not found in {_assembly.GetName().Name}.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }

    private static string ResourceName(TemplateKind kind, TemplateFormat format)
    {
        var stem = kind switch
        {
            TemplateKind.EmailVerification => "EmailVerification",
            TemplateKind.PasswordReset => "PasswordReset",
            TemplateKind.PasswordChanged => "PasswordChanged",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var ext = format == TemplateFormat.Html ? "html" : "txt";
        return $"Sodalis.Modules.Messaging.Templates.{stem}.{ext}";
    }
}

public enum TemplateFormat
{
    Html,
    Text
}
