using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FileTracking;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<IReportGenerator, ReportGenerator>();

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "File Tracking Service";
});

var host = builder.Build();
host.Run();
