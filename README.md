# Distributed tracing and monitoring .net core (micro)services

## Brief

Monitoring gives us observability in our system and helps us to discover, understand, and address issues to minimize their impact on the business. We should aim for the best solutions out there; especially when we build a microservices solution that has brought up new challenges in regards to observability.
First, we need to extract metrics from our systems - like the Memory usage or the number of HTTP requests per seconds. The type of monitoring that provides details about the internal state of our application is called white-box monitoring, and the metrics extraction process is called instrumentation.
But the new era of highly dynamic distributed systems brought new challenges to observability. One of the new debugging methodologies is distributed tracing. It propagates transactions from distributed services and gains information from cross-process communication.

## Quickstart guide

First, you have create two networks used by the sample project:

```bash
docker network create monitoring
docker network create sample
```

To start up the tracing and monitoring infrastructure (including: jaeger, prometheus and grafana) run:

```bash
docker-compose -f ./docker-compose.monitoring.yml up -d
```

## Tutorial

Create a sample web application

```bash
md sample.web
dotnet new mvc --name sample.web --output sample.web --auth none
```

Install the needed packages to the project

```bash
dotnet add sample.web/sample.web.csproj package Jaeger
dotnet add sample.web/sample.web.csproj package OpenTracing
dotnet add sample.web/sample.web.csproj package OpenTracing.Contrib.NetCore
dotnet restore sample.web/sample.web.csproj
```

Open the project in your favourite editor, eg.: Visual Studio Code

```bash
code sample.web
```

Open ```Startup.cs``` and add the OpenTracing services to the project:

```csharp
        private static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .CaptureStartupErrors(false)
                .SuppressStatusMessages(true)
                .ConfigureServices(
                    (context, services) =>
                    {
                        services.AddSingleton<ITracer>(serviceProvider =>
                        {
                            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

                            var serviceName = Assembly.GetEntryAssembly().GetName().Name;
                            var sampler = new ConstSampler(sample: true);

                            var tracer = new Tracer.Builder(serviceName)
                                .WithReporter(
                                    new RemoteReporter.Builder()
                                        .WithLoggerFactory(loggerFactory)
                                        .WithSender(new UdpSender("localhost", 6831))
                                        .Build())
                                .WithLoggerFactory(loggerFactory)
                                .WithSampler(sampler)
                                .Build();

                            GlobalTracer.Register(tracer);

                            return tracer;
                        });
                        services.AddOpenTracing(builder =>
                        {
                            builder.ConfigureAspNetCore(options =>
                            {
                                // This example shows how to ignore certain requests to prevent spamming the tracer with irrelevant data
                                options.Hosting.IgnorePatterns.Add(request => request.Request.Path.Value?.StartsWith("/healthz") == true);
                            });
                        });
                        services.Configure<HttpHandlerDiagnosticOptions>(options =>
                        {
                            // Prevent endless loops when OpenTracing is tracking HTTP requests to Jaeger. Not effective when UdpSender is used.
                            options.IgnorePatterns.Add(request => new Uri("http://localhost:14268/api/traces").IsBaseOf(request.RequestUri));
                        });
                    })
                .UseStartup<Startup>();
    }
```

Build and run the solution. Open some pages to generate logs. For further examples check the examples folder.

### Testing all the applications and microservices

Once the containers are deployed, you should be able to access any of the services in the following URLs or connection string, from your dev machine:

* Jaeger: <http://localhost:16686>
* Prometheus: <http://localhost:9090>
* Graphana: <http://localhost:3000>

## References

* [Jaeger](https://www.jaegertracing.io/), inspired by Dapper and OpenZipkin, is a distributed tracing system released as open source by Uber Technologies. It is used for monitoring and troubleshooting microservices-based distributed systems. It has an OpenTracing compatible datamodel and instrumentation libraries for many languages. Jaeger is hosted by the Cloud Native Computing Foundation (CNCF).
* [OpenTracing](https://opentracing.io/) API provides a standard, vendor neutral framework for instrumentation.
* [OpenTracing  API for C#](https://github.com/opentracing/opentracing-csharp) solution includes the .NET platform API for OpenTracing.
* [OpenTracing instrumentation for .NET Core apps](https://github.com/opentracing-contrib/csharp-netcore) provides OpenTracing instrumentation for .NET Core based applications. It can be used with any OpenTracing compatible tracer. Supports any library or framework that uses .NET's DiagnosticSource to instrument its code. It will create a span for every Activity and it will create span.Log calls for all other diagnostic events. Also provides enhanced instrumentation (Inject/Extract, tags, configuration options) for the following libraries / frameworks: ASP.NET Core, Entity Framework Core and HttpClient.
* [Prometheus](https://prometheus.io) is an open-source systems monitoring and alerting toolkit originally built at SoundCloud. Since its inception in 2012, many companies and organizations have adopted Prometheus, and the project has a very active developer and user community. It is now a standalone open source project and maintained independently of any company. To emphasize this, and to clarify the project's governance structure, Prometheus joined the Cloud Native Computing Foundation in 2016. Prometheus scrapes metrics from instrumented jobs, either directly or via an intermediary push gateway for short-lived jobs. It stores all scraped samples locally and runs rules over this data to either aggregate and record new time series from existing data or generate alerts.
* [AppMetrics](https://github.com/AppMetrics/AppMetrics) is an open-source and cross-platform .NET library used to record metrics within an application. App Metrics can run on .NET Core or on the full .NET framework also supporting .NET 4.5.2. App Metrics abstracts away the underlaying repository of your Metrics for example InfluxDB, Graphite, Elasticsearch etc, by sampling and aggregating in memory and providing extensibility points to flush metrics to a repository at a specified interval.
* [AppMetrics Prometheus reporting extension](https://github.com/AppMetrics/Prometheus) is an AppMetrics extension for Prometheus support.
* [Grafana](https://grafana.com/) or other API consumers can be used to visualize the collected data.

## Other useful links

* [DiagnosticSource user guide](https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/DiagnosticSourceUsersGuide.md)
* [Logging using DiagnosticSource in ASP.NET Core](https://andrewlock.net/logging-using-diagnosticsource-in-asp-net-core/)
