FROM microsoft/dotnet:2.1.4-runtime AS base
WORKDIR /app
EXPOSE 80

FROM microsoft/dotnet:2.1.402-sdk AS build
ARG configuration
WORKDIR /src
COPY . .
RUN dotnet restore sampleapp.sln && \
    dotnet build sampleapp.sln --configuration ${configuration:-Release} && \
    dotnet test sampleapp.sln --configuration ${configuration:-Release} --no-build --no-restore -v normal

FROM build AS publish
RUN dotnet publish ./src/Match.Service/Match.Service.csproj --configuration ${configuration:-Release} --output /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
HEALTHCHECK --start-period=10s --interval=5s --timeout=3s --retries=3 CMD curl --silent --fail http://localhost:80/healthz || exit 1
ENTRYPOINT ["dotnet", "Match.Service"]
