using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Web;
using User.Management.API.Models;
using User.Management.Data.Models;
using User.Management.Service.Models;
using User.Management.Service.Models.Authentication.Login;
using User.Management.Service.Models.Authentication.SignUp;
using User.Management.Service.Models.Authentication.User;
using User.Management.Service.Services;
using static Humanizer.In;
using static System.Net.WebRequestMethods;
namespace User.Management.API.Controllers;
[ApiController]
[Route("api/[controller]")] 
public class AuthenticationController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly IUserManagement _user;
    public AuthenticationController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        IEmailService emailService,
        IUserManagement user
        )
    {
        _userManager = userManager;
        _signInManager = signManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _emailService = emailService;
        _user = user;
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterUser registerUser)
    {
        var tokenResponse = await _user.CreateUserWithTokenAsync(registerUser);
        if (tokenResponse.IsSuccess)
        {
            await _user.AssignRoleToUserAsync(registerUser.Roles, tokenResponse.Response.User);
            var confirmationLink = Url.Action(nameof(ConfirmEmail), "Authentication", new {tokenResponse.Response.Token, email = registerUser.Email},
                protocol: Request.Scheme,
                host: Request.Host.Value);
            var message  = new Message(new string[] { registerUser.Email! }, "Confirmation Email Link",confirmationLink!);
            _emailService.SendEmail(message);
            return Ok("Email verification successful");
        }
        return BadRequest("Email verification failed");
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
        var loginOtpResponse=await _user.GetOtpByLoginAsync(loginModel);
        if (loginOtpResponse.Response!=null)
        {
            var user = loginOtpResponse.Response.User;
            if (user.TwoFactorEnabled)
            {
                var token = loginOtpResponse.Response.Token;
                var message = new Message(new string[] { user.Email! }, "OTP Confrimation", token);
                _emailService.SendEmail(message);

                return StatusCode(StatusCodes.Status200OK,
                    new Response { IsSuccess= loginOtpResponse.IsSuccess, status = "Success", message = $"We have sent an OTP to your Email {user.Email} and the token is {token}" });
            }
            if (user != null && await _userManager.CheckPasswordAsync(user, loginModel.Password))
            {
                var serviceResponse = await _user.GetJwtTokenAsync(user);
                return Ok(serviceResponse);

            }
        }
        return Unauthorized();
        
    }
    
    // otp verification
    [HttpPost]
    [Route("Login-2FA")]
    public async Task<IActionResult> LoginWithOTP([FromBody] LoginWithOTP loginWithOTP)
    {
        var jwt =await _user.LoginUserWithJWTokenAsync(loginWithOTP.Code, loginWithOTP.Username);
        if (jwt.IsSuccess)
        {
            return Ok(jwt);
                
        }
        return StatusCode(StatusCodes.Status404NotFound,
            new Response { status = "Success", message = $"Invalid Code" });
    }
    
    
    //refresh token
    [HttpPost]
    [Route("Refresh-Token")]
    public async Task<IActionResult> RefreshToken(LoginResponse tokens)
    {
        var jwt = await _user.RenewAccessTokenAsync(tokens);
        if (jwt.IsSuccess)
        {
            return Ok(jwt);
        }
        return StatusCode(StatusCodes.Status404NotFound,
            new Response { status = "Success", message = $"Invalid Code" });
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

    

}