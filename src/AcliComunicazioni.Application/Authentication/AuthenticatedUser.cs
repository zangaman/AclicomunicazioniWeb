namespace AcliComunicazioni.Application.Authentication;

public sealed record AuthenticatedUser(
    int Id,
    string Username,
    string DisplayName,
    string Role);
