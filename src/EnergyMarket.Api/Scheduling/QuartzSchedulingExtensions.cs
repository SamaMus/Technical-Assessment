using Quartz;

namespace EnergyMarket.Api.Scheduling;

public static class QuartzSchedulingExtensions
{
    public static IServiceCollection AddImportScheduling(this IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            var jobKey = new JobKey("ImportDayAheadPricesJob");

            q.AddJob<ImportDayAheadPricesJob>(opts => opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("ImportDayAheadPricesJob-trigger")
                .WithSimpleSchedule(s => s.WithIntervalInHours(1).RepeatForever())
                .StartNow()); // also runs once immediately at startup
        });

        services.AddQuartzHostedService(opts => opts.WaitForJobsToComplete = true);

        return services;
    }
}
