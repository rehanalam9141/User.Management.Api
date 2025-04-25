namespace User.Management.Service.Models.Authentication.User;

public class RefreshToken
{
    public string? Token { get; set; }
    public DateTime ExpiryTokenDate { get; set; }
}