using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ConsStock.Api.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace ConsStock.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithTags("Auth");
        auth.MapPost("/login", Login).RequireRateLimiting("login");
        auth.MapPost("/users", CreateUser).RequireAuthorization("Admin");
        auth.MapGet("/me", Me).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> Login(LoginInput input,
        UserManager<IdentityUser<Guid>> users, JwtSettings jwt)
    {
        if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrEmpty(input.Password))
            return InvalidCredentials();
        var user = await users.FindByEmailAsync(input.Email);
        if (user is null || await users.IsLockedOutAsync(user)) return InvalidCredentials();
        if (!await users.CheckPasswordAsync(user, input.Password))
        {
            await users.AccessFailedAsync(user);
            return InvalidCredentials();
        }
        await users.ResetAccessFailedCountAsync(user);
        var roles = await users.GetRolesAsync(user);
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(jwt.LifetimeMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? "")
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims, now, expires,
            new SigningCredentials(new SymmetricSecurityKey(jwt.Key), SecurityAlgorithms.HmacSha256));
        return Results.Ok(new
        {
            accessToken = new JwtSecurityTokenHandler().WriteToken(token),
            tokenType = "Bearer",
            expiresAt = new DateTimeOffset(expires, TimeSpan.Zero)
        });
    }

    private static async Task<IResult> CreateUser(CreateUserInput input,
        UserManager<IdentityUser<Guid>> users)
    {
        if (input.Role is not ("Admin" or "Operator"))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Use Admin ou Operator."] });
        if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.Password))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["user"] = ["Email e senha são obrigatórios."] });
        if (await users.FindByEmailAsync(input.Email) is not null)
            return Results.Problem(statusCode: 409, title: "Usuário já cadastrado.");
        var user = new IdentityUser<Guid>
        {
            Id = Guid.NewGuid(), UserName = input.Email.Trim(), Email = input.Email.Trim()
        };
        var result = await users.CreateAsync(user, input.Password);
        if (!result.Succeeded)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["user"] = result.Errors.Select(x => x.Description).ToArray()
            });
        result = await users.AddToRoleAsync(user, input.Role);
        if (!result.Succeeded) throw new InvalidOperationException("Não foi possível atribuir o papel.");
        return Results.Created($"/api/v1/auth/users/{user.Id}", new { user.Id, user.Email, input.Role });
    }

    private static IResult Me(ClaimsPrincipal user) => Results.Ok(new
    {
        id = user.FindFirstValue(ClaimTypes.NameIdentifier),
        email = user.FindFirstValue(ClaimTypes.Email),
        roles = user.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray()
    });

    private static IResult InvalidCredentials()
        => Results.Problem(statusCode: 401, title: "Credenciais inválidas.");
}

public sealed record LoginInput(string Email, string Password);
public sealed record CreateUserInput(string Email, string Password, string Role);
