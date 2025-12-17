FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR ./

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish "src/SmartReader.WebDemo/SmartReader.WebDemo.csproj" -c Release -o published

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR ./

COPY --from=build-env /published .

# For added security, you can opt out of the diagnostic pipeline. When you opt-out this allows the container to run as read-only
ENV DOTNET_EnableDiagnostics=0

# Entrypoint
CMD ASPNETCORE_URLS=http://*:${PORT:-5000} dotnet SmartReader.WebDemo.dll