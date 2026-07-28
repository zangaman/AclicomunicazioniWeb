namespace AcliComunicazioni.Infrastructure.Authentication;

public sealed class DevelopmentAuthenticationOptions
{
    public const string SectionName = "DevelopmentAuthentication";

    public int UserId { get; set; } = 1;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = "User";
}
