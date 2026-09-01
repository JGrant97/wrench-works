using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WrenchWorks.Api.Auth;
using WrenchWorks.Infrastructure.Persistence;

namespace WrenchWorks.Api.Features.Auth.Login;

// Currency rides along with the session because every screen formats money and the
// alternative is a business lookup on each one. It lands in the readable ww_user cookie,
// which is what lets both client components and server components format consistently.
public record UserDto(Guid Id, string Name, string Email, Guid BusinessId, string BusinessName, string Currency, IEnumerable<string> Permissions, IEnumerable<string> Features);
