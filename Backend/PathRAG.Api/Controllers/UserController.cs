using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PathRAG.Api.Models;
using System.Security.Claims;

namespace PathRAG.Api.Controllers;

[ApiController]
[Route("api/user")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly ILogger<UserController> _logger;

    public UserController(ILogger<UserController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<UserDto> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue(ClaimTypes.Name);
        var email = User.FindFirstValue(ClaimTypes.Email);
        
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }
        
        var user = new UserDto
        {
            Id = userId,
            Name = name ?? "User",
            Email = email ?? "user@example.com"
        };
        
        return Ok(user);
    }
}
