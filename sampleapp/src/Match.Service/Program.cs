using System;
using System.Reflection;
using System.Threading.Tasks;
using Jaeger;
using Jaeger.Reporters;
using Jaeger.Samplers;
using Jaeger.Senders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Contrib.NetCore.CoreFx;
using OpenTracing.Util;
using Serilog;

namespace Match.Service
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var hostBuilder = new HostBuilder()
                  .ConfigureAppConfiguration((context, configuration) =>
                  {
                      configuration.AddEnvironmentVariables();
                      configuration.AddJsonFile("appsettings.json", optional: true);
                      configuration.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                      configuration.AddCommandLine(args);
                  })
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
                      services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = true);
                      services.AddLogging();
                      services.AddSingleton<ITracer>(serviceProvider =>
                      {
                          string serviceName = context.Configuration["service"] ?? Assembly.GetEntryAssembly().GetName().Name;

                          var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

                          var sampler = new ConstSampler(sample: true);

                          var mode = context.Configuration["jaeger-mode"] ?? "udp";

                          var tracer = new Tracer.Builder(serviceName)
                            .WithReporter(
                                new RemoteReporter.Builder()
                                    .WithLoggerFactory(loggerFactory)
                                    .WithSender(
                                        mode == "http" ?
                                        (ISender)new HttpSender(context.Configuration["jaeger-http"] ?? "http://jaeger:14268/api/traces") :
                                        new UdpSender(context.Configuration["jaeger-host"] ?? "jaeger", int.Parse(context.Configuration["jaeger-port"] ?? "6831"), 0))
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
                          options.IgnorePatterns.Add(request => new Uri(context.Configuration["jaeger-http"] ?? "http://jaeger:14268/api/traces").IsBaseOf(request.RequestUri));
                      });

                      services.AddOpenTracing();
                  })
                  .UseConsoleLifetime();

            var host = hostBuilder.Build();
            using (host)
            {
                await host.StartAsync();
                await host.WaitForShutdownAsync();
            }
        }
    }
}
