using Prime.Application.Features.Users;

namespace Prime.WebApi.Commands;

/// <summary>
/// <c>dotnet Prime.WebApi.dll bootstrap-admin someone@example.org</c>: makes the first SYSTEM_ADMIN of a new installation
/// (docs/analysis/workflow-security.md §4.2). The person signs up and signs in once first, so PRIME knows them as a
/// pending user. Refused once an active SYSTEM_ADMIN exists. Runs against the configured database and exits; it never
/// starts the web server.
/// </summary>
public static class BootstrapAdminCommand
{
    public const string Name = "bootstrap-admin";

    public static async Task<int> RunAsync(IServiceProvider services, string email)
    {
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IUserAccountService>().BootstrapFirstAdminAsync(email);
        if (result.IsSuccess)
        {
            Console.WriteLine(result.Value);
            return 0;
        }
        Console.Error.WriteLine($"{result.Code}: {result.Message}");
        return 1;
    }
}
