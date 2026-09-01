using WrenchWorks.Api.Features.Auth.Login;
using WrenchWorks.Api.Features.Auth.RefreshToken;
using WrenchWorks.Api.Features.Auth.Register;
using WrenchWorks.Api.Features.Auth.VerifyEmail;
using WrenchWorks.Api.Features.Business;
using WrenchWorks.Api.Features.Billing;
using WrenchWorks.Api.Features.Calendar;
using WrenchWorks.Api.Features.Catalogue;
using WrenchWorks.Api.Features.Customers;
using WrenchWorks.Api.Features.Dashboard;
using WrenchWorks.Api.Features.Inventory;
using WrenchWorks.Api.Features.Jobs;
using WrenchWorks.Api.Features.Messaging;
using WrenchWorks.Api.Features.Tax;
using WrenchWorks.Api.Features.Users;
using WrenchWorks.Api.Features.Vehicles;
using WrenchWorks.Api.Features.Zones;

namespace WrenchWorks.Api.Features.Common;

/// <summary>
/// One registration per slice, mirroring the "Map Feature Endpoints" block in Program.cs.
/// A new slice needs a line in both places; nothing is discovered by convention, so a
/// missing line fails loudly on the first request rather than silently degrading.
/// </summary>
public static class FeatureServices
{
    public static IServiceCollection AddFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IRegisterService, RegisterService>();
        services.AddScoped<ILoginService, LoginService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IVerifyEmailService, VerifyEmailService>();
        services.AddScoped<IBusinessService, BusinessService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IZoneService, ZoneService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ICatalogueService, CatalogueService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ITaxService, TaxService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IMessagingService, MessagingService>();
        services.AddScoped<IBillingService, BillingService>();
        return services;
    }
}
