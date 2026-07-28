using System.ComponentModel.DataAnnotations;

namespace AcliComunicazioni.Web.Models.Account;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Inserisci il nome utente.")]
    [Display(Name = "Nome utente")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Inserisci la password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Resta connesso")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
