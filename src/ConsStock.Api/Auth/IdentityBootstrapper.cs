using Microsoft.AspNetCore.Identity;

namespace ConsStock.Api.Auth;

public static class IdentityBootstrapper
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var name in new[] { "Admin", "Operator" })
        {
            if (!await roles.RoleExistsAsync(name))
            {
                var created = await roles.CreateAsync(new IdentityRole<Guid>(name));
                if (!created.Succeeded) throw new InvalidOperationException("Não foi possível criar os papéis iniciais.");
            }
        }

        var email = configuration["Bootstrap:AdminEmail"];
        var password = configuration["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password)) return;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Configure Bootstrap:AdminEmail e Bootstrap:AdminPassword juntos.");

        var users = services.GetRequiredService<UserManager<IdentityUser<Guid>>>();
        if (await users.FindByEmailAsync(email) is not null) return;
        var user = new IdentityUser<Guid> { Id = Guid.NewGuid(), UserName = email, Email = email };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException("Administrador inicial inválido: " +
                string.Join("; ", result.Errors.Select(x => x.Description)));
        result = await users.AddToRoleAsync(user, "Admin");
        if (!result.Succeeded) throw new InvalidOperationException("Não foi possível atribuir o papel Admin.");
    }
}
