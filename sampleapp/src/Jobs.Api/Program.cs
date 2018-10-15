using System;
using System.Reflection;
using App.Metrics.AspNetCore.Health;
using Jaeger;
using Jaeger.Reporters;
using Jaeger.Samplers;
using Jaeger.Senders;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Contrib.NetCore.CoreFx;
using OpenTracing.Util;
using Serilog;

namespace Jobs.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .CaptureStartupErrors(false)
                .SuppressStatusMessages(true)
                .ConfigureLogging((context, logging) =>
                {
                    logging.AddSerilog(
                        new LoggerConfiguration()
                            .ReadFrom.Configuration(context.Configuration)
                            .Enrich.FromLogContext()
                            .CreateLogger());
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddLogging();
                    services.AddSingleton<ITracer>(serviceProvider =>
                    {
                        string serviceName = context.Configuration["Tracing:ServiceName"] ?? Assembly.GetEntryAssembly().GetName().Name;

                        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

                        var sampler = new ConstSampler(sample: true);

                        var mode = context.Configuration["Tracing:Mode"] ?? "udp";

                        var tracer = new Tracer.Builder(serviceName)
                        .WithReporter(
                            new RemoteReporter.Builder()
                                .WithLoggerFactory(loggerFactory)
                                .WithSender(
                                    mode == "http" ?
                                    (ISender)new HttpSender(context.Configuration["Tracing:Http"] ?? "http://jaeger:14268/api/traces") :
                                    new UdpSender(context.Configuration["Tracing:Host"] ?? "jaeger", int.Parse(context.Configuration["Tracing:Port"] ?? "6831"), 0))
                                .Build())
                        .WithLoggerFactory(loggerFactory)
                        .WithSampler(sampler)
                        .Build();

                        GlobalTracer.Register(tracer);

                        return tracer;
                    });

                    // Prevent endless loops when OpenTracing is tracking HTTP requests to Jaeger.
                    services.Configure<HttpHandlerDiagnosticOptions>(options =>
                    {
                        options.IgnorePatterns.Add(request => new Uri(context.Configuration["Tracing:Http"] ?? "http://jaeger:14268/api/traces").IsBaseOf(request.RequestUri));
                    });

                    services.AddOpenTracing();
                })
                .UseStartup<Startup>()
                .UseHealth()
                .UseHealthEndpoints(options => { options.PingEndpointEnabled = false; })
                .ConfigureAppHealthHostingConfiguration(options => { options.HealthEndpoint = "/healthz"; })
                .UseSerilog();
    }
}
