using ExpenseTrackerAPI.Common;
using ExpenseTrackerAPI.Dtos;
using ExpenseTrackerAPI.Interfaces;
using ExpenseTrackerAPI.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTrackerAPI.Controllers;

/// <summary>Controller for managing authentication.</summary>
/// <param name="Context">The database context.</param>
/// <param name="Logger">The logger.</param>
[ApiController]
[Route("api/[controller]")]
public class AuthController(ApplicationDbContext Context, ILogger<AuthController> Logger)
    : ControllerBase
{
    /// <summary>Login a user.</summary>
    /// <param name="userLoginDto">The user login data.</param>
    /// <param name="jwtService">The JWT service for working with user token.</param>
    /// <returns>The logged in user.</returns>
    ///
    /// <response code="200">The user was logged in successfully.</response>
    /// <response code="400">Invalid user credentials.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserTokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<Results<BadRequest<ErrorResponse>, Ok<UserTokenDto>>> Login(
        UserLoginDto userLoginDto,
        [FromServices] IJwtService jwtService
    )
    {
        Logger.LogInformation("Logging in user {}.", userLoginDto.Email);

        var user = await Context
            .Users.Where(u => u.Email == userLoginDto.Email)
            .FirstOrDefaultAsync();

        if (user is null)
        {
            Logger.LogInformation("User {} not found.", userLoginDto.Email);
            return TypedResults.BadRequest(
                new ErrorResponse { Message = "Invalid user credentials." }
            );
        }

        if (!user.VerifyPassword(userLoginDto.Password))
        {
            Logger.LogInformation("User {} provided an incorrect password.", userLoginDto.Email);
            return TypedResults.BadRequest(
                new ErrorResponse { Message = "Invalid user credentials." }
            );
        }

        Logger.LogInformation("Generating access token of user {}...", user.Id);
        var token = jwtService.GenerateToken(user.Id.ToString());

        Logger.LogInformation("User {} logged in.", user.Id);
        return TypedResults.Ok(new UserTokenDto { Token = token });
    }

    /// <summary>Register a user.</summary>
    /// <returns>The registered user.</returns>
    /// <param name="userRegisterDto">The user registration data.</param>
    ///
    /// <response code="200">The user was successfully registered.</response>
    /// <response code="400">The data passed was invalid.</response>
    /// <response code="409">The data passed has conflicting values.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<Results<BadRequest, Conflict<ErrorResponse>, Ok<User>>> Register(
        UserRegisterDto userRegisterDto
    )
    {
        Logger.LogInformation("Registering user {}.", userRegisterDto.Email);

        var existingUser = await Context
            .Users.Where(u => u.Email == userRegisterDto.Email)
            .FirstOrDefaultAsync();

        if (existingUser is not null)
        {
            Logger.LogInformation(
                "User with the same email {} already exists.",
                userRegisterDto.Email
            );
            return TypedResults.Conflict(
                new ErrorResponse { Message = "Email is already in-use." }
            );
        }

        var user = new User
        {
            Name = userRegisterDto.Name,
            Email = userRegisterDto.Email,
            Password = userRegisterDto.Password,
        };

        await Context.Users.AddAsync(user);
        await Context.SaveChangesAsync();

        Logger.LogInformation("User {} registered.", user.Id);
        return TypedResults.Ok(user);
    }
}
