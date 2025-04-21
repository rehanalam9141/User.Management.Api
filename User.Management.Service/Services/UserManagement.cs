using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Asn1.Ocsp;
using User.Management.Service.Models;
using User.Management.Service.Models.Authentication.SignUp;
using User.Management.Service.Models.Authentication.User;

namespace User.Management.Service.Services;

public class UserManagement : IUserManagement
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    public UserManagement(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        IEmailService emailService
    )
    {
        _userManager = userManager;
        _signInManager = signManager;
        _roleManager = roleManager;
    }

    public async Task<ApiResponse<CreateUserResponse>> CreateUserWithTokenAsync(RegisterUser registerUser)
    {
        //check if user already exist
        var userExits = await _userManager.FindByEmailAsync(registerUser.Email);
        if (userExits != null)
        {
            //return BadRequest("Email already exists");
            return new ApiResponse<CreateUserResponse> { IsSuccess = false, StatusCode = 403, Message = "User already exists" };
        }
        //add user into database
        IdentityUser user = new()
        {
            Email = registerUser.Email,
            UserName = registerUser.Email,
            SecurityStamp = Guid.NewGuid().ToString(),
            TwoFactorEnabled = true
        };
      
        var result = await _userManager.CreateAsync(user, registerUser.Password);
        if (result.Succeeded)
        { 
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            return new ApiResponse<CreateUserResponse> { Response = new CreateUserResponse(){User = user,token = token }, IsSuccess = true, StatusCode = 200, Message = "User created" };
        }
        else
        {
            return new ApiResponse<CreateUserResponse> { IsSuccess = false, StatusCode = 500, Message = "Failed to create user" };
        }
    }
    

    public async Task<ApiResponse<List<string>>> AssignRoleToUserAsync(List<string> roles, IdentityUser user)
    {
        var assignedRole = new List<string>();
        foreach (var role in roles)
        {
            // check if role exist
            if (await _roleManager.RoleExistsAsync(role))
            {
                if (!await _userManager.IsInRoleAsync(user, role))
                {
                    await _userManager.AddToRoleAsync(user, role);
                    assignedRole.Add(role);
                }
            }
        }
        return new ApiResponse<List<string>> { IsSuccess = true, StatusCode = 200, Message = "Assigned roles successfully", Response = assignedRole };
    }
}