using Microsoft.AspNetCore.Http.HttpResults;

namespace WrenchWorks.Api.Features.Users;

public static class UserEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization("users.manage");

        group.MapGet("/", ListAsync);
        group.MapPost("/invite", InviteAsync);

        // "/me" is deliberately OUTSIDE the group above. Group metadata is additive, so
        // declaring it inside meant reading your own profile also required users.manage —
        // i.e. only admins could see who they were. Any authenticated user may read theirs.
        app.MapGet("/api/users/me", GetMeAsync)
           .WithTags("Users")
           .RequireAuthorization();
    }

    private static async Task<Ok<List<UserListItemDto>>> ListAsync(IUserService svc, CancellationToken ct) =>
        TypedResults.Ok(await svc.ListAsync(ct));

    private static async Task<Created<InvitedUserDto>> InviteAsync(IUserService svc, InviteUserRequest request, CancellationToken ct)
    {
        var result = await svc.InviteAsync(request, ct);
        return TypedResults.Created($"/api/users/{result.Id}", result);
    }

    private static async Task<Ok<CurrentUserDto>> GetMeAsync(IUserService svc, CancellationToken ct) =>
        TypedResults.Ok(await svc.GetMeAsync(ct));
}
