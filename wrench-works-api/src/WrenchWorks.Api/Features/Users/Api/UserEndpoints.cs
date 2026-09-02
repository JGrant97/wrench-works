namespace WrenchWorks.Api.Features.Users;

public static class UserEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization("users.manage");

        group.MapGet("/",
            (IUserEndpointHandler handler, CancellationToken ct) =>
                handler.ListAsync(ct));

        group.MapPost("/invite",
            (InviteUserRequest request, IUserEndpointHandler handler, CancellationToken ct) =>
                handler.InviteAsync(request, ct));

        // "/me" is deliberately OUTSIDE the group above. Group metadata is additive, so
        // declaring it inside meant reading your own profile also required users.manage --
        // i.e. only admins could see who they were. Any authenticated user may read theirs.
        app.MapGet("/api/users/me",
            (IUserEndpointHandler handler, CancellationToken ct) =>
                handler.GetMeAsync(ct))
           .WithTags("Users")
           .RequireAuthorization();
    }
}
