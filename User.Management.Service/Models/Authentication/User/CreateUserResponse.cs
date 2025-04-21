using Microsoft.AspNetCore.Identity;

namespace User.Management.Service.Models.Authentication.User;

public class CreateUserResponse
{
    public string token { get; set; }
    public IdentityUser User { get; set; }
}