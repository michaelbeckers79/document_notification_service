# Docker and Kubernetes Deployment Guide

This guide explains how to deploy the Document Notification Service using Docker and Kubernetes.

## Docker Support

### Building the Docker Image

```bash
# Build the image
docker build -t document-notification-service:latest .

# Tag for your registry
docker tag document-notification-service:latest your-registry/document-notification-service:latest

# Push to registry
docker push your-registry/document-notification-service:latest
```

### Running with Docker

```bash
# Run a one-time process command
docker run --rm \
  -e Database__ConnectionString="Server=sqlserver;Database=DocumentNotificationDB;User Id=sa;Password=YourPassword;TrustServerCertificate=true;" \
  -e RabbitMQ__UserName="your-user" \
  -e RabbitMQ__Password="your-password" \
  document-notification-service:latest \
  process --dry-run

# Run migration
docker run --rm \
  -e Database__ConnectionString="Server=sqlserver;Database=DocumentNotificationDB;User Id=sa;Password=YourPassword;TrustServerCertificate=true;" \
  document-notification-service:latest \
  migrate --create

# Check status
docker run --rm \
  -e Database__ConnectionString="Server=sqlserver;Database=DocumentNotificationDB;User Id=sa;Password=YourPassword;TrustServerCertificate=true;" \
  document-notification-service:latest \
  status --limit 20
```

### Docker Compose Example

```yaml
version: '3.8'
services:
  document-notification:
    image: document-notification-service:latest
    environment:
      - Database__ConnectionString=Server=sqlserver;Database=DocumentNotificationDB;User Id=sa;Password=YourPassword;TrustServerCertificate=true;
      - RabbitMQ__UserName=guest
      - RabbitMQ__Password=guest
      - RabbitMQ__HostName=rabbitmq
    depends_on:
      - sqlserver
      - rabbitmq
    command: ["process", "--force"]

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrong@Passw0rd
    ports:
      - "1433:1433"

  rabbitmq:
    image: rabbitmq:3-management
    ports:
      - "5672:5672"
      - "15672:15672"
```

## Kubernetes Support

### Quick Start

1. **Update configuration**: Edit `k8s/secret.yaml` with your actual credentials
2. **Deploy all resources**:
   ```bash
   kubectl apply -k k8s/
   ```
3. **Check status**:
   ```bash
   kubectl get pods,jobs,cronjobs
   ```

### Configuration Management

#### Secrets
Update the secrets in `k8s/secret.yaml`:

```bash
# Create secrets with actual values
kubectl create secret generic document-notification-secrets \
  --from-literal=Database__ConnectionString="Server=sqlserver;Database=DocumentNotificationDB;User Id=sa;Password=YourPassword;TrustServerCertificate=true;" \
  --from-literal=DSXService__Username="your-dsx-user" \
  --from-literal=DSXService__Password="your-dsx-password" \
  --from-literal=RabbitMQ__UserName="your-rabbitmq-user" \
  --from-literal=RabbitMQ__Password="your-rabbitmq-password"
```

#### ConfigMaps
Modify `k8s/configmap.yaml` to update non-sensitive configuration like:
- Service URLs
- RabbitMQ exchange settings
- Email configuration
- Document types to process

### Scheduled Jobs

The service includes several CronJob examples:

#### Main Processing Job
- **File**: `k8s/cronjob-process.yaml`
- **Schedule**: Every 30 minutes
- **Command**: `process --force`

#### Daily Processing
- **File**: `k8s/cronjob-examples.yaml`
- **Schedule**: Daily at 6 AM
- **Command**: `process --force`

#### Retry Failed Documents
- **File**: `k8s/cronjob-examples.yaml`
- **Schedule**: Daily at 8 PM
- **Command**: `retry --all`

#### Weekly Status Report
- **File**: `k8s/cronjob-examples.yaml`
- **Schedule**: Every Sunday at 9 AM
- **Command**: `status --limit 50`

### Manual Operations

#### Run Database Migration
```bash
kubectl apply -f k8s/job-migrate.yaml
```

#### Manual Document Processing
```bash
kubectl create job manual-process --from=cronjob/document-notification-process
```

#### Check Processing Status
```bash
kubectl exec -it deployment/document-notification-service -- dotnet DocumentNotificationService.dll status --limit 20
```

#### Retry Failed Documents
```bash
kubectl exec -it deployment/document-notification-service -- dotnet DocumentNotificationService.dll retry --all
```

### Health Checks

The service includes a health command for Kubernetes probes:

```bash
# Test health check
kubectl exec -it deployment/document-notification-service -- dotnet DocumentNotificationService.dll health
```

Add health probes to your deployment:

```yaml
containers:
- name: document-notification
  # ... other config
  livenessProbe:
    exec:
      command:
      - dotnet
      - DocumentNotificationService.dll
      - health
    initialDelaySeconds: 30
    periodSeconds: 60
  readinessProbe:
    exec:
      command:
      - dotnet
      - DocumentNotificationService.dll
      - health
    initialDelaySeconds: 10
    periodSeconds: 30
```

### Monitoring and Logging

#### View Logs
```bash
# View current job logs
kubectl logs job/document-notification-migrate

# View CronJob logs
kubectl logs -l app=document-notification-service

# Follow deployment logs
kubectl logs -f deployment/document-notification-service
```

#### Monitor Jobs
```bash
# List all jobs and their status
kubectl get jobs

# List CronJobs and their schedules
kubectl get cronjobs

# View job details
kubectl describe job document-notification-migrate
```

### Customization

#### Using Kustomize
The `k8s/kustomization.yaml` file allows easy customization:

```bash
# Deploy with custom namespace
kubectl apply -k k8s/ -n production

# Deploy with custom image
cd k8s/
kustomize edit set image document-notification-service=your-registry/document-notification-service:v1.2.3
kubectl apply -k .
```

#### Custom Schedules
Modify the CronJob schedules in the YAML files:
- `0 6 * * *` - Daily at 6 AM
- `*/30 * * * *` - Every 30 minutes
- `0 20 * * *` - Daily at 8 PM
- `0 9 * * 0` - Every Sunday at 9 AM

### Troubleshooting

#### Common Issues

1. **Image Pull Errors**: Update the image name in `kustomization.yaml`
2. **Configuration Errors**: Verify secrets and configmaps are correctly formatted
3. **Database Connection**: Ensure the database is accessible from the cluster
4. **RabbitMQ Connection**: Verify RabbitMQ settings and credentials

#### Debug Commands
```bash
# Check pod status
kubectl get pods -l app=document-notification-service

# View pod details
kubectl describe pod <pod-name>

# Get logs
kubectl logs <pod-name>

# Execute commands in running pod
kubectl exec -it <pod-name> -- /bin/bash
```

### Security Considerations

1. **Non-root User**: The Docker image runs as a non-root user
2. **Secrets Management**: Use Kubernetes secrets for sensitive data
3. **Resource Limits**: Set appropriate CPU and memory limits
4. **Network Policies**: Consider implementing network policies for additional security

### Scaling

The service is designed as a scheduled job processor and doesn't require multiple replicas. However, you can:

1. **Adjust CronJob concurrency**:
   ```yaml
   spec:
     concurrencyPolicy: Forbid  # Prevent concurrent runs
     # OR
     concurrencyPolicy: Allow   # Allow concurrent runs
   ```

2. **Scale the deployment** for manual operations:
   ```bash
   kubectl scale deployment document-notification-service --replicas=3
   ```

3. **Use multiple CronJobs** with different schedules for load distribution