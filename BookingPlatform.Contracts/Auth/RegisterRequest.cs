namespace BookingPlatform.Contracts.Auth;

public sealed class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    // Opcionalno: ako business još nema nijednog člana,
    // prvi registrovani korisnik može da ga preuzme kao Owner.
    public long? InitialBusinessId { get; set; }
}