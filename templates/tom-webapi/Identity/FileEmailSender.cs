using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Tom.WebApi.Identity;

/// <summary>
/// Writes Identity emails to the log and to <c>emails/</c> under the content root.
/// This is a real <see cref="IEmailSender{TUser}"/> (not Identity's no-op).
/// Replace with SMTP before Production.
/// </summary>
public sealed class FileEmailSender : IEmailSender<AppUser>
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileEmailSender> _logger;

    public FileEmailSender(IWebHostEnvironment environment, ILogger<FileEmailSender> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink)
        => WriteAsync("Confirm email", email, confirmationLink);

    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink)
        => WriteAsync("Reset password", email, resetLink);

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode)
        => WriteAsync("Password reset code", email, resetCode);

    private async Task WriteAsync(string subject, string email, string body)
    {
        _logger.LogInformation(
            "Identity email [{Subject}] to {Email}: {Body}",
            subject,
            email,
            body);

        var directory = Path.Combine(_environment.ContentRootPath, "emails");
        Directory.CreateDirectory(directory);

        var safeEmail = string.Join("_", email.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(directory, $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{safeEmail}.txt");
        var contents = new StringBuilder()
            .AppendLine($"To: {email}")
            .AppendLine($"Subject: {subject}")
            .AppendLine()
            .AppendLine(body)
            .ToString();

        await File.WriteAllTextAsync(path, contents);
    }
}
