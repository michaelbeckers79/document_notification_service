# Multi-stage build Dockerfile for Document Notification Service
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the entire project
COPY . .

# Restore dependencies and build the application
RUN dotnet restore DocumentNotificationService/DocumentNotificationService.csproj
RUN dotnet publish DocumentNotificationService/DocumentNotificationService.csproj -c Release -o /app/publish --no-restore

# Final stage - runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app

# Create a non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Create logs directory and set permissions
RUN mkdir -p /app/logs && chown -R appuser:appuser /app/logs

# Copy the published application
COPY --from=build /app/publish .

# Copy health check script
COPY healthcheck.sh /app/healthcheck.sh

# Change ownership of the app directory
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Set the entry point
ENTRYPOINT ["dotnet", "DocumentNotificationService.dll"]