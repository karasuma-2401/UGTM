using UGTM.Modules.Notifications.Infrastructure.Email;

namespace UGTM.Architecture.Tests;

public sealed class SmtpOptionsValidatorTests
{
    [Fact]
    public void Validate_AcceptsAuthenticatedStartTlsConfiguration()
    {
        var validator = new SmtpOptionsValidator(allowUnencrypted: false);

        var result = validator.Validate(null, ValidOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_RejectsUnencryptedSmtpInProduction()
    {
        var validator = new SmtpOptionsValidator(allowUnencrypted: false);
        var options = ValidOptions() with { Security = SmtpSecurityMode.None };

        var result = validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure =>
            failure.Contains("only allowed in Development", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsIncompleteCredentials()
    {
        var validator = new SmtpOptionsValidator(allowUnencrypted: true);
        var options = ValidOptions() with { Password = null };

        var result = validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure =>
            failure.Contains("configured together", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AllowsUnencryptedLocalSmtpInDevelopment()
    {
        var validator = new SmtpOptionsValidator(allowUnencrypted: true);
        var options = ValidOptions() with
        {
            Host = "mailpit",
            Port = 1025,
            Username = null,
            Password = null,
            Security = SmtpSecurityMode.None
        };

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    private static SmtpOptions ValidOptions() => new()
    {
        Host = "smtp.example.com",
        Port = 587,
        FromAddress = "no-reply@example.com",
        FromName = "UGTM",
        Username = "no-reply@example.com",
        Password = "provider-issued-secret",
        Security = SmtpSecurityMode.StartTls,
        TimeoutSeconds = 30
    };
}
