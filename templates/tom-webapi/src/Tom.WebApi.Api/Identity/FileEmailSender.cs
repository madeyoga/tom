using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Tom.WebApi.Api.Identity;

/// <summary>
/// Writes Identity emails to the log and to <c>emails/</c> under the content root.
/// This is a real <see cref="IEmailSender{TUser}"/> (not Identity's no-op).
/// Replace with SMTP before Production.
/// </summary>
public sealed class FileEmailSender(
    IWebHostEnvironment environment,
    TimeProvider clock,
    ILogger<FileEmailSender> logger) : IEmailSender<AppUser>
{
    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink)
        => WriteAsync("Confirm email", email, confirmationLink);

    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink)
        => WriteAsync("Reset password", email, resetLink);

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode)
        => WriteAsync("Password reset code", email, resetCode);

    private async Task WriteAsync(string subject, string email, string body)
    {
        logger.LogInformation(
            "Identity email [{Subject}] to {Email}: {Body}",
            subject,
            email,
            body);

        var directory = Path.Combine(environment.ContentRootPath, "emails");
        Directory.CreateDirectory(directory);

        var safeEmail = string.Join("_", email.Split(Path.GetInvalidFileNameChars()));
        var stamp = clock.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(directory, $"{stamp}-{safeEmail}.txt");
        var contents = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"To: {email}")
            .AppendLine(CultureInfo.InvariantCulture, $"Subject: {subject}")
            .AppendLine()
            .AppendLine(body)
            .ToString();

        await File.WriteAllTextAsync(path, contents);
    }
}
