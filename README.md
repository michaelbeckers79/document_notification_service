# Document Notification Service

A .NET Core console application that processes documents from the DSX document store and sends notifications via RabbitMQ.

## Features

- **DSX Integration**: Queries DSX document store API with time-based filtering
- **Document Type Filtering**: Configurable document types to process
- **RabbitMQ Messaging**: Sends notifications with configurable headers and SSL support
- **Database Tracking**: Stores processed documents in SQL Server database
- **Email Notifications**: Sends error and summary emails
- **Command Line Interface**: Multiple actions with advanced options
- **Pagination Support**: Handles large result sets efficiently
- **Error Handling**: Comprehensive error handling and retry capabilities

## Commands

### Process Documents
```bash
dotnet run -- process [options]
```
Options:
- `--dry-run, -d` : Run without making changes
- `--force, -f` : Force processing even if no new documents
- `--since, -s` : Override last query timestamp (ISO 8601 format)

### Check Status
```bash
dotnet run -- status [options]
```
Options:
- `--limit, -l` : Number of recent documents to show (default: 10)

### Retry Failed Documents
```bash
dotnet run -- retry [options]
```
Options:
- `--document-id, -d` : Retry specific document by ID
- `--all, -a` : Retry all failed documents

### Database Migration
```bash
dotnet run -- migrate [options]
```
Options:
- `--create, -c` : Create the database if it doesn't exist

### Health Check
```bash
dotnet run -- health [options]
```
Options:
- `--timeout, -t` : Health check timeout in seconds (default: 30)

This command is designed for Kubernetes health probes and Docker health checks.

## Configuration

The application uses `appsettings.json` for configuration:

### DSX Service Settings
- **ServiceUrl**: DSX document handling service endpoint
- **DocumentTypes**: Array of document types to filter
- **PageSize**: Number of documents per page (for pagination)
- **Timeout**: Service call timeout
- **Username**: Username for basic HTTP authentication (optional)
- **Password**: Password for basic HTTP authentication (optional)

### Database Settings
- **ConnectionString**: SQL Server connection string

### RabbitMQ Settings
- **HostName**: RabbitMQ server hostname
- **Port**: RabbitMQ server port
- **Exchange**: Exchange name for publishing messages
- **UseSsl**: Enable SSL/TLS connection
- **SslSettings**: SSL certificate configuration
- **Headers**: Configurable message headers ($tenantid, $operation, $application)

### Email Settings
- **SmtpServer**: SMTP server for notifications
- **Recipients**: List of email addresses for error notifications

## Database Schema

The database uses snake_case naming convention for tables and columns.

### processed_documents Table
- **document_id**: Unique document identifier from DSX
- **name**: Document name/reference
- **document_date**: Document date from metadata
- **portfolio_id**: Portfolio ID from metadata
- **processed_at**: When the document was processed
- **message_sent**: Whether RabbitMQ message was sent successfully
- **error_message**: Error details if processing failed

### last_query_timestamps Table
- **last_successful_query**: Timestamp of last successful query
- **updated_at**: When the timestamp was last updated

## Message Format

The RabbitMQ message follows the format defined in `RabbitMQ Message.xml`:

```xml
<Communication xmlns="http://www.objectway.com/comm/request/communicationrequest">
    <Type>DOCUMENT_NOTIFICATION</Type>
    <EntityID>{{Portfolio ID}}</EntityID>
    <ExternalData></ExternalData>
</Communication>
```

## Installation

### Standard Installation

1. Clone the repository
2. Configure `appsettings.json` with your environment settings
3. Run database migrations: `dotnet run -- migrate --create`
4. Test with dry run: `dotnet run -- process --dry-run`

### Docker Installation

The application can be containerized using Docker for deployment in Kubernetes or other container platforms.

```bash
# Build Docker image
docker build -t document-notification-service:latest .

# Run with Docker
docker run --rm \
  -e Database__ConnectionString="your-connection-string" \
  -e RabbitMQ__UserName="your-user" \
  -e RabbitMQ__Password="your-password" \
  document-notification-service:latest \
  process --dry-run
```

### Kubernetes Deployment

The service includes full Kubernetes support with:
- ConfigMaps for configuration management
- Secrets for sensitive data
- CronJobs for scheduled processing
- Jobs for database migrations
- Example manifests for different schedules

See [DOCKER_KUBERNETES.md](DOCKER_KUBERNETES.md) for detailed deployment instructions.

Quick deployment:
```bash
# Update secrets in k8s/secret.yaml with your values
kubectl apply -k k8s/
```

## Error Handling

- Failed documents are stored in the database with error details
- Email notifications are sent for critical errors
- Failed documents can be retried using the retry command
- Processing summaries are emailed after each run

## Logging

The application uses Serilog for advanced logging with both console and file output:

- **Console Logging**: Structured logs with timestamps, levels, and context
- **File Logging**: Daily rotating files stored in `logs/document-notification-{date}.txt`
- **Retention**: Log files are kept for 31 days
- **Configuration**: Log levels and sinks can be configured in `appsettings.json` under the `Serilog` section