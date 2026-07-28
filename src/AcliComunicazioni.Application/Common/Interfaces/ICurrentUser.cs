namespace AcliComunicazioni.Application.Common.Interfaces;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    int? UserId { get; }

    string? UserName { get; }

    string? DisplayName { get; }

    string? Role { get; }
}