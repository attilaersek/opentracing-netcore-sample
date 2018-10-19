using System;
using System.Reflection;
using System.Threading.Tasks;
using Consul;
using Jaeger;
using Jaeger.Reporters;
using Jaeger.Samplers;
using Jaeger.Senders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTracing;
using OpenTracing.Contrib.NetCore.CoreFx;
using OpenTracing.Util;
using Sample.Infrastructure;
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
                      services.AddLogging();
                      services.Configure<ConsulOptions>(context.Configuration.GetSection("consul"));
                      services.Configure<TracerOptions>(context.Configuration.GetSection("tracer"));
                      services.AddSingleton<ITracer>(serviceProvider =>
                      {
                          var tracerOptions = serviceProvider.GetRequiredService<IOptions<TracerOptions>>();
                          var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

                          var serviceName = tracerOptions.Value?.ServiceName ?? Assembly.GetEntryAssembly().GetName().Name;
                          var sampler = new ConstSampler(sample: true);
                          var mode = tracerOptions.Value?.Mode ?? TracerMode.Udp;

                          var tracer = new Tracer.Builder(serviceName)
                          .WithReporter(
                              new RemoteReporter.Builder()
                                  .WithLoggerFactory(loggerFactory)
                                  .WithSender(
                                      mode == TracerMode.Http ?
                                      (ISender)new HttpSender(tracerOptions.Value?.HttpEndPoint ?? "http://jaeger:14268/api/traces") :
                                      new UdpSender(tracerOptions.Value?.UdpEndPoint?.Host ?? "jaeger", tracerOptions.Value?.UdpEndPoint?.Port ?? 6831, 0))
                                  .Build())
                          .WithLoggerFactory(loggerFactory)
                          .WithSampler(sampler)
                          .Build();

                          GlobalTracer.Register(tracer);

                          // Prevent endless loops when OpenTracing is tracking HTTP requests to Jaeger.
                          services.Configure<HttpHandlerDiagnosticOptions>(options =>
                          {
                              options.IgnorePatterns.Add(request => new Uri(tracerOptions.Value?.HttpEndPoint ?? "http://jaeger:14268/api/traces").IsBaseOf(request.RequestUri));
                          });

                          return tracer;
                      });
                      services.AddSingleton<IConsulClient, ConsulClient>(serviceProvider =>
                      {
                          var consulOptions = serviceProvider.GetRequiredService<IOptions<ConsulOptions>>();
                          return new ConsulClient(options =>
                          {
                              options.Address = new Uri(consulOptions.Value?.Address ?? "http://consul:8500");
                          });
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
