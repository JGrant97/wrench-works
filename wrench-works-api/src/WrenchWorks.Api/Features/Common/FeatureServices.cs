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
        services.AddScoped<IRegisterEndpointHandler, RegisterEndpointHandler>();
        services.AddScoped<IRegisterService, RegisterService>();
        services.AddScoped<IRegisterRepository, RegisterRepository>();
        services.AddScoped<ILoginEndpointHandler, LoginEndpointHandler>();
        services.AddScoped<ILoginService, LoginService>();
        services.AddScoped<ILoginRepository, LoginRepository>();
        services.AddScoped<IRefreshTokenEndpointHandler, RefreshTokenEndpointHandler>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IVerifyEmailEndpointHandler, VerifyEmailEndpointHandler>();
        services.AddScoped<IVerifyEmailService, VerifyEmailService>();
        services.AddScoped<IVerifyEmailRepository, VerifyEmailRepository>();
        services.AddScoped<IBusinessEndpointHandler, BusinessEndpointHandler>();
        services.AddScoped<IBusinessService, BusinessService>();
        services.AddScoped<IBusinessRepository, BusinessRepository>();
        services.AddScoped<IUserEndpointHandler, UserEndpointHandler>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IZoneEndpointHandler, ZoneEndpointHandler>();
        services.AddScoped<IZoneService, ZoneService>();
        services.AddScoped<IZoneRepository, ZoneRepository>();
        services.AddScoped<ICustomerEndpointHandler, CustomerEndpointHandler>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICatalogueEndpointHandler, CatalogueEndpointHandler>();
        services.AddScoped<ICatalogueService, CatalogueService>();
        services.AddScoped<ICatalogueRepository, CatalogueRepository>();
        services.AddScoped<IVehicleEndpointHandler, VehicleEndpointHandler>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<ICalendarEndpointHandler, CalendarEndpointHandler>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<ICalendarRepository, CalendarRepository>();
        services.AddScoped<IJobEndpointHandler, JobEndpointHandler>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IDashboardEndpointHandler, DashboardEndpointHandler>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<ITaxEndpointHandler, TaxEndpointHandler>();
        services.AddScoped<ITaxService, TaxService>();
        services.AddScoped<ITaxRepository, TaxRepository>();
        services.AddScoped<IInventoryEndpointHandler, InventoryEndpointHandler>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IMessagingEndpointHandler, MessagingEndpointHandler>();
        services.AddScoped<IMessagingService, MessagingService>();
        services.AddScoped<IMessagingRepository, MessagingRepository>();
        services.AddScoped<IBillingEndpointHandler, BillingEndpointHandler>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IBillingRepository, BillingRepository>();
        return services;
    }
}
