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

## Configuration

The application uses `appsettings.json` for configuration:

### DSX Service Settings
- **ServiceUrl**: DSX document handling service endpoint
- **DocumentTypes**: Array of document types to filter
- **PageSize**: Number of documents per page (for pagination)
- **Timeout**: Service call timeout

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

### ProcessedDocuments Table
- **DocumentId**: Unique document identifier from DSX
- **Name**: Document name/reference
- **DocumentDate**: Document date from metadata
- **PortfolioId**: Portfolio ID from metadata
- **ProcessedAt**: When the document was processed
- **MessageSent**: Whether RabbitMQ message was sent successfully
- **ErrorMessage**: Error details if processing failed

### LastQueryTimestamps Table
- **LastSuccessfulQuery**: Timestamp of last successful query
- **UpdatedAt**: When the timestamp was last updated

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

1. Clone the repository
2. Configure `appsettings.json` with your environment settings
3. Run database migrations: `dotnet run -- migrate --create`
4. Test with dry run: `dotnet run -- process --dry-run`

## Error Handling

- Failed documents are stored in the database with error details
- Email notifications are sent for critical errors
- Failed documents can be retried using the retry command
- Processing summaries are emailed after each run

## Logging

The application uses Microsoft.Extensions.Logging with console output. Log levels can be configured in `appsettings.json`.