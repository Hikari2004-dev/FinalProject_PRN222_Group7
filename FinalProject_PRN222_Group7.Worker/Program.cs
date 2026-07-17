using FinalProject_PRN222_Group7.Worker;
using FinalProject_PRN222_Group7.BLL.Services.Subscriptions;
using FinalProject_PRN222_Group7.DAL.Data;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "FinalProject PRN222 Subscription Worker";
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

builder.Services
    .Configure<SubscriptionWorkerOptions>(
        builder.Configuration.GetSection(SubscriptionWorkerOptions.SectionName))
    .AddScoped<ISubscriptionMaintenanceService, SubscriptionMaintenanceService>()
    .AddHostedService<SubscriptionWorker>();

var host = builder.Build();
host.Run();
