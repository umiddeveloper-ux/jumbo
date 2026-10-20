FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Jumbo.Shared/Jumbo.Shared.csproj Jumbo.Shared/
COPY Jumbo.Api/Jumbo.Api.csproj Jumbo.Api/
RUN dotnet restore Jumbo.Api/Jumbo.Api.csproj

COPY Jumbo.Shared/ Jumbo.Shared/
COPY Jumbo.Api/ Jumbo.Api/
RUN dotnet publish Jumbo.Api/Jumbo.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Jumbo.Api.dll"]
