using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using User.Management.API.Models;
using User.Management.API.Models.Authentication.Login;
using User.Management.API.Models.Authentication.SignUp;
using User.Management.Service.Models;
using User.Management.Service.Services;

namespace User.Management.API.Controllers;
[ApiController]
[Route("api/[controller]")] 
public class AuthenticationController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    public AuthenticationController(
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
        _configuration = configuration;
        _emailService = emailService;
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterUser registerUser, string role)
    {
        //check if user already exist
        var userExits = await _userManager.FindByEmailAsync(registerUser.Email);
        if (userExits != null)
        {
            //return BadRequest("Email already exists");
            return StatusCode(StatusCodes.Status403Forbidden, new Response { status = "Error", message = "User already exists" });

        }
        //add user into database
        IdentityUser user = new()
        {
            Email = registerUser.Email,
            UserName = registerUser.Email,
            SecurityStamp = Guid.NewGuid().ToString(),
            TwoFactorEnabled = true
        };
      
        // check if role exist
        if (await _roleManager.RoleExistsAsync(role))
        {
            var result = await _userManager.CreateAsync(user, registerUser.Password);
            if (!result.Succeeded)
            { 
                return StatusCode(StatusCodes.Status500InternalServerError, new Response { status = "Error", message = "User Failed to created" });
            } 
            
            // add role to the user

            await _userManager.AddToRoleAsync(user, role);
            
            //add token to verify the email
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = Url.Action(nameof(ConfirmEmail), "Authentication", new {token, email = user.Email},
                protocol: Request.Scheme,
                host: Request.Host.Value);
            var message  = new Message(new string[] { user.Email! }, "Confirmation Email Link",confirmationLink!);
            _emailService.SendEmail(message);
            
            
            return StatusCode(StatusCodes.Status200OK, new Response { status = "Success", message = $"User Created  & Email sent to {user.Email} Successfully" });

        }
        else
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new Response { status = "Error", message = "This role dose not exists" });
        }
    }
    
    //send email
    // [HttpGet]
    // public IActionResult TestEmail()
    // {
    //     var message = new Message(new string[] { "rehanalam9141@yahoo.com" }, "Testing Email", "<h1>Hello Word</h1>");
    //     _emailService.SendEmail(message);
    //    
    //     return StatusCode(StatusCodes.Status200OK, new Response { status = "Success", message = "Test Email send Successfully" });
    // }

    //email confirmation
    [HttpGet("ConfirmEmail")]
    public async Task<IActionResult> ConfirmEmail(string token, string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user != null)
        {
           var result = await _userManager.ConfirmEmailAsync(user, token);
           if (result.Succeeded)
           {
               return StatusCode(StatusCodes.Status200OK, new Response { status = "Success", message = "Email Verified Successfully" });
           }
        }
        return StatusCode(StatusCodes.Status500InternalServerError, new Response { status = "Error", message = "the user does not exist" });
    }

    //login
    [HttpPost]
    [Route("Login")]
    public async Task<IActionResult> Login([FromBody] LoginModel loginModel)
    {
        // checking the user name
        var user = await _userManager.FindByNameAsync(loginModel.Username);
        if (user.TwoFactorEnabled)
        {
            await _signInManager.SignOutAsync();
            await _signInManager.PasswordSignInAsync(user, loginModel.Password, false, true);
            var token = await _userManager.GenerateTwoFactorTokenAsync(user,"Email");
            var message  = new Message(new string[] { user.Email! }, "OTP Confirmation",token);
            _emailService.SendEmail(message);
            
            // Add OTP to response header (only visible in browser dev tools)
            //Response.Headers.Add("X-Dev-OTP", token);
            
            return StatusCode(StatusCodes.Status200OK, new 
            {
                status = "Success", 
                message = $"OTP sent to your email {user.Email}",
                OTP = token
            });
        }
        // checking the password
        if (user != null && await _userManager.CheckPasswordAsync(user, loginModel.Password))
        {
            //create claim list
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            //we add roles to the  list
            var userRoles = await _userManager.GetRolesAsync(user);
            foreach (var role in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, role));
            }
            
            //generate the token with the claim

            var jwtToken = GetToken(authClaims);

            //return the token
            
            return Ok( new
            {
                token = new JwtSecurityTokenHandler().WriteToken(jwtToken),
                expiration = jwtToken.ValidTo
            });
        }

        return Unauthorized();
        
    }
    
    // otp verification
    [HttpPost]
    [Route("Login-2FA")]
    public async Task<IActionResult> LoginWithOTP(string code, string username)
    {
        var user = await _userManager.FindByNameAsync(username);
        var signIn = await _signInManager.TwoFactorSignInAsync("Email",code,false,false);
        if (signIn.Succeeded)
        {
            if (user != null)
            {
                //create claim list
                var authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                };

                //we add roles to the  list
                var userRoles = await _userManager.GetRolesAsync(user);
                foreach (var role in userRoles)
                {
                    authClaims.Add(new Claim(ClaimTypes.Role, role));
                }
            
                //generate the token with the claim

                var jwtToken = GetToken(authClaims);

                //return the token
            
                return Ok( new
                {
                    token = new JwtSecurityTokenHandler().WriteToken(jwtToken),
                    expiration = jwtToken.ValidTo
                });
            }
        }
        return StatusCode(StatusCodes.Status404NotFound, new Response { status = "Error", message = $"Invalid code" });
    }

    //forgot password 
    [HttpPost]
    [AllowAnonymous]
    [Route("forgot-password")]
    public async Task<IActionResult> ForgotPassword([Required] string email)
    {
       var user = await _userManager.FindByEmailAsync(email);
       if (user!=null)
       {
           var token = await _userManager.GeneratePasswordResetTokenAsync(user);
           var forgotPasswordLink = Url.Action("ResetPassword", "Authentication", new { token, email = user.Email }, Request.Scheme);
           var message  = new Message(new string[] { user.Email! }, "Forgot password Email Link",forgotPasswordLink!);
           _emailService.SendEmail(message);
           
           return StatusCode(StatusCodes.Status200OK, new Response
           {
               status = "Success", 
               message = $"Password change request is sent on email {user.Email}. Please check your email address and verify your Link"
           });
       }
       return StatusCode(StatusCodes.Status400BadRequest, new Response
       {
           status = "Error", 
           message = $"Could not send reset password link. Please try again."
       });
    }
    
    // reset password get
    [HttpGet("reset-password")]
    public async Task<IActionResult> ResetPassword(string token, string email)
    {
        var model = new ResetPassword{Token= token, Email= email};
        return Ok(new
        {
            model
        });
    }
    
    //reset password post
    [HttpPost]
    [AllowAnonymous]
    [Route("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPassword resetPassword)
    {
        var user = await _userManager.FindByEmailAsync(resetPassword.Email);
        if (user!=null)
        {
           var resetPassResult = await _userManager.ResetPasswordAsync(user, resetPassword.Token, resetPassword.Password);
           if (!resetPassResult.Succeeded)
           {
               foreach (var error in resetPassResult.Errors)
               {
                   ModelState.AddModelError(error.Code, error.Description);
               }
               return Ok(ModelState);
           }
           return StatusCode(StatusCodes.Status200OK, new Response
           {
               status = "Success", 
               message = $"Password has been  changed"
           });
        }
        return StatusCode(StatusCodes.Status400BadRequest, new Response
        {
            status = "Error", 
            message = $"Password not changed. Please try again."
        });
    }

    //generate token
    private JwtSecurityToken GetToken(List<Claim> authClaims)
    {
        var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"]));

        var token = new JwtSecurityToken(
            issuer: _configuration["JWT:ValidIssuer"],
            audience: _configuration["JWT:ValidAudience"],
            expires: DateTime.Now.AddHours(3),
            claims: authClaims,
            signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
        );

        return token;
    }

}