# Use the official .NET SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY SideHustleTracking/SideHustleTracking.fsproj SideHustleTracking/
RUN dotnet restore SideHustleTracking/SideHustleTracking.fsproj

# Copy the data directory (needed for type providers at compile time)
COPY data/ data/

# Copy the rest of the source code
COPY SideHustleTracking/ SideHustleTracking/

# Build the application
WORKDIR /src/SideHustleTracking
RUN dotnet build SideHustleTracking.fsproj -c Release -o /app/build

# Publish the application
RUN dotnet publish SideHustleTracking.fsproj -c Release -o /app/publish

# Use the runtime image for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy the published application
COPY --from=build /app/publish .

# Create data directory for mounting
RUN mkdir -p /app/data /app/data/fx

# Expose the port (default ASP.NET port)
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "SideHustleTracking.dll"]