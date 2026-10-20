FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
RUN apt-get update && apt-get install -y --no-install-recommends python3 python-is-python3 \
    && rm -rf /var/lib/apt/lists/*
RUN dotnet workload install wasm-tools

COPY Jumbo.Shared/ Jumbo.Shared/
COPY Jumbo.ApiClient/ Jumbo.ApiClient/
COPY JumboWeb/ JumboWeb/

# A split restore-then-publish-with-stub-csproj (the usual Docker layer-cache trick)
# silently drops the Server-side blazor.web.js/blazor.server.js static web assets for
# this combined Server+WebAssembly project — restore must see the real project files.
RUN dotnet publish JumboWeb/JumboWeb/JumboWeb.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "JumboWeb.dll"]
