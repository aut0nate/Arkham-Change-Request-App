# Arkham Change Management Application

A change management application for a fictitious company named Arkham. Built with .NET, Auth0 enterprise SAML federation (backed by Azure Entra ID), mobile-responsive design, and secure cloud hosting on Microsoft Azure.

> **Note**: This application was developed as a learning project focused on Azure services integration, deployment, and management rather than web development. It was built with significant assistance from **OpenAI Codex**, demonstrating how AI tooling can accelerate Azure-focused learning and development.

## Project Purpose

This project serves as a comprehensive example of:
- **Azure Services Integration**: Entra ID, App Service, SQL Database, Key Vault, Blob Storage
- **Enterprise Authentication**: Single sign-on with claims-based user management
- **Secure Deployment Practices**: Infrastructure as code, secrets management, CI/CD
- **Modern Web Application**: Responsive design, mobile optimization, security best practices

The focus is on **Azure cloud architecture and DevOps practices** rather than custom web development.

## Features

- **Enterprise Authentication**: Auth0 enterprise SAML → Azure Entra ID integration with fine-grained group checks and claims mapping
- **Professional UI**: Dark-themed interface matching enterprise standards with mobile-responsive design
- **Comprehensive Workflow**: Change submission, auto-prefilled requestor data, attachments, approvals dashboard, status updates, and audit trail
- **File Management**: Secure file upload via Azure Blob Storage (PDF, DOC, DOCX, images)
- **Persistent Storage**: Azure SQL Database with Entity Framework Core
- **Mobile Optimized**: Touch-friendly interface for iPhone and mobile devices
- **Real-time Validation**: Form validation with user-friendly error handling and future-dated scheduling guardrails
- **Security First**: Azure Key Vault integration, HTTPS enforcement, anti-forgery protection, and Auth0 group-based authorization
- **Monitoring**: Application Insights integration for telemetry and diagnostics

## Architecture

- **Frontend**: ASP.NET Core 8.0 MVC with responsive CSS and mobile optimization
- **Authentication**: Azure Entra ID with App Service Easy Auth
- **Backend**: .NET 8 with Entity Framework Core
- **Database**: Azure SQL Database
- **File Storage**: Azure Blob Storage with secure container access
- **Security**: Azure Key Vault for secrets management
- **Hosting**: Azure App Service with B1 tier
- **Monitoring**: Application Insights for telemetry and diagnostics

## Local Development Setup

### Prerequisites

- .NET 8 SDK
- SQL Server LocalDB or SQL Server Express
- Azure Storage Emulator (optional for local development)
- Visual Studio 2022 or VS Code

### Getting Started

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd ArkhamChangeRequest
   ```

2. **Install dependencies**
   ```bash
   dotnet restore
   ```

3. **Configure local settings**
   
   Copy `.env.example` to `.env` and update the values for Auth0, Azure, and SQL secrets. Populate `appsettings.json` / `appsettings.Development.json` with non-sensitive defaults (these files are ignored by Git so you can store local secrets safely):
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ArkhamChangeRequestDb;Trusted_Connection=true;MultipleActiveResultSets=true",
       "AzureStorage": "UseDevelopmentStorage=true"
     },
     "AzureAd": {
       "Instance": "https://login.microsoftonline.com/",
       "Domain": "[YOUR_DOMAIN].onmicrosoft.com",
       "TenantId": "[YOUR_TENANT_ID]",
       "ClientId": "[YOUR_CLIENT_ID]",
       "CallbackPath": "/signin-oidc"
     }
   }
   ```

4. **Create and apply database migrations**
   ```bash
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

5. **Run the application**
   ```bash
   dotnet run
   ```

6. **Access the application**
   - Open your browser and navigate to `https://localhost:5001`

## Complete Azure Deployment Guide

### Quick Start Prerequisites
- Azure CLI installed and authenticated (`az login`)
- Azure subscription with Contributor access
- .NET 8 SDK for local development
- Access to Azure Entra ID tenant

### Step-by-Step Deployment

#### 1. Initial Azure Setup
```bash
# Set deployment variables
RESOURCE_GROUP="arkham-change-request-rg"
LOCATION="uksouth"
APP_NAME="arkham-change-request-app"
SQL_SERVER="arkham-sql-server-$(date +%s)"
STORAGE_ACCOUNT="arkhamstorage$(date +%s)"
KEYVAULT_NAME="arkham-kv-$(date +%s)"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION
```

#### 2. Create Core Infrastructure
```bash
# Storage Account
az storage account create \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_LRS

# SQL Server & Database
az sql server create \
  --name $SQL_SERVER \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --admin-user arkhamadmin \
  --admin-password "YourSecurePassword"

az sql db create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name ArkhamChangeRequestDb \
  --service-objective Basic

# Allow Azure services through SQL firewall
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Key Vault
az keyvault create \
  --name $KEYVAULT_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION
```

#### 3. Create App Service
```bash
# App Service Plan
az appservice plan create \
  --name arkham-app-plan \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku B1

# Web App
az webapp create \
  --resource-group $RESOURCE_GROUP \
  --plan arkham-app-plan \
  --name $APP_NAME \
  --runtime "DOTNETCORE:8.0"

# Enable managed identity
az webapp identity assign \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME
```

#### 4. Configure Azure Entra ID
1. **Create App Registration** in Azure Portal:
   - Navigate to Entra ID → App registrations → New registration
   - Name: "Arkham Change Request"
   - Redirect URI: `https://$APP_NAME.azurewebsites.net/.auth/login/aad/callback`
   - Note the Application (client) ID and Directory (tenant) ID

2. **Configure Easy Auth**:
```bash
# Enable authentication
az webapp auth update \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --enabled true \
  --action LoginWithAzureActiveDirectory

# Configure Azure AD provider
az webapp auth microsoft update \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --client-id "YOUR_CLIENT_ID" \
  --tenant-id "YOUR_TENANT_ID"
```

#### 5. Configure Application Settings
```bash
# Get connection strings
SQL_CONNECTION=$(az sql db show-connection-string \
  --client ado.net \
  --server $SQL_SERVER \
  --name ArkhamChangeRequestDb | \
  sed 's/<username>/arkhamadmin/g' | \
  sed 's/<password>/YourSecurePassword/g')

STORAGE_CONNECTION=$(az storage account show-connection-string \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --query connectionString -o tsv)

# Configure app settings
az webapp config appsettings set \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --settings \
    "ConnectionStrings__DefaultConnection=$SQL_CONNECTION" \
    "ConnectionStrings__AzureStorage=$STORAGE_CONNECTION" \
    "KeyVaultSettings__VaultUrl=https://$KEYVAULT_NAME.vault.azure.net/" \
    "AzureAd__TenantId=YOUR_TENANT_ID" \
    "AzureAd__ClientId=YOUR_CLIENT_ID"
```

#### 6. Deploy Application Code
```bash
# Clone and build
git clone <your-repo-url>
cd ArkhamChangeRequest
dotnet publish -c Release -o ./publish

# Deploy
cd publish && zip -r ../app.zip . && cd ..
az webapp deploy \
  --resource-group $RESOURCE_GROUP \
  --name $APP_NAME \
  --src-path app.zip \
  --type zip

# Clean up
rm app.zip && rm -rf publish/
```

#### 7. Initialize Database
```bash
# Apply migrations (use actual connection string)
dotnet ef database update --connection "$SQL_CONNECTION"
```

### Post-Deployment Configuration
1. **Test Authentication**: Visit `https://$APP_NAME.azurewebsites.net`
2. **Verify User Display**: Ensure full name appears (not email)
3. **Test Mobile**: Check responsive design on mobile devices
4. **Monitor Logs**: Use `az webapp log tail` for real-time monitoring

### Environment Variables Reference
Copy `.env.example` to `.env` and update with your values (never commit this file):
```bash
AZURE_AD_TENANT_ID=your-tenant-id
AUTH0_DOMAIN=arkham.uk.auth0.com
AUTH0_CLIENT_ID=your-auth0-client-id
AUTH0_CLIENT_SECRET=your-auth0-client-secret
RESOURCE_GROUP=arkham-change-request-rg
APP_SERVICE_NAME=arkham-change-request-app
```

## Configuration

### Environment Variables

The application uses the following configuration structure (mirrored between `appsettings.json`, `appsettings.Development.json`, and secure values injected via `.env` or Azure App Service configuration):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Your SQL Server connection string",
    "AzureStorage": "Your Azure Storage connection string"
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "your-domain.onmicrosoft.com",
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id"
  },
  "KeyVaultSettings": {
    "VaultUrl": "https://your-keyvault.vault.azure.net/"
  },
  "ApplicationInsights": {
    "ConnectionString": "your-app-insights-connection-string"
  }
}
```

### Key Vault Integration

Sensitive configuration values are stored in Azure Key Vault:
- Database connection strings
- Storage account keys
- Application secrets

## Mobile Responsive Design

The application features comprehensive mobile optimization:

- **Responsive Navigation**: Adapts to mobile screens with touch-friendly controls
- **Mobile Forms**: Single-column layout with appropriate input sizing
- **Touch Optimization**: 16px font-size inputs to prevent iOS zoom
- **User Interface**: Dropdown menus optimized for touch devices
- **Viewport Configuration**: Proper scaling for all device types

### Supported Breakpoints
- **Desktop**: 1200px and above
- **Tablet**: 768px - 1199px
- **Mobile**: 480px - 767px
- **Small Mobile**: Under 480px
- **Landscape**: Orientation-specific optimizations

## File Upload Configuration

The application supports secure file uploads with the following specifications:

- **Supported Types**: PDF, DOC, DOCX, PNG, JPG, JPEG
- **Maximum Size**: 10MB per file
- **Storage**: Azure Blob Storage with secure container access
- **Security**: File type validation and virus scanning capabilities

## Security Implementation

### Authentication Flow
1. User accesses application
2. Redirected to Auth0 hosted login (enterprise SAML handshake with Azure Entra ID / Microsoft login)
3. Auth0 returns ID/Access tokens to ASP.NET Core
4. Claims are enriched (groups, display name) and requestor / approver details auto-populate forms
5. Session managed with ASP.NET Core cookies; logout triggers Auth0 + Microsoft sign-out

### Data Protection
- **In Transit**: HTTPS enforcement with TLS 1.2+
- **At Rest**: Azure SQL encryption and secure blob storage
- **Secrets**: Azure Key Vault with RBAC access control
- **Application**: Anti-forgery tokens and input validation

## Database Schema

### Core Tables

#### ChangeRequest
- **Id**: Primary key (int, identity)
- **RequestorName**: User's full name resolved from Auth0 / Entra ID claims
- **RequestorEmail**: User's email from Auth0 / Entra ID
- **ChangeTitle**: Brief description of the change
- **ChangeDescription**: Detailed change description
- **AuthorizationServiceAffected**: Services impacted
- **ProposedImplementationDate**: Planned implementation date
- **ChangeType**: Enum (Normal, Emergency, Standard, Major)
- **Priority**: Enum (Low, Medium, High, Critical)
- **RiskAssessment**: Risk analysis description
- **BackoutPlan**: Rollback procedure
- **CreatedDate**: Automatic timestamp
- **Status**: Current change status (New, Approved, Rejected, On-Hold, Complete, Abandoned)

#### ChangeRequestAttachment
- **Id**: Primary key (int, identity)
- **ChangeRequestId**: Foreign key to ChangeRequest
- **FileName**: Original file name
- **BlobUrl**: Azure Storage blob URL
- **ContentType**: MIME type
- **FileSize**: File size in bytes
- **UploadedDate**: Upload timestamp

## API Endpoints

### Change Request Management
- `GET /ChangeRequest/Create` - New change request form (auto-fills requestor details)
- `POST /ChangeRequest/Create` - Submit new change request
- `GET /ChangeRequest/Details/{id}` - View change request, attachments, and audit history
- `GET /ChangeRequest/Edit/{id}` - Edit request (permitted while status is New)
- `GET /ChangeRequest/MyRequests` - List user's submissions
- `GET /ChangeRequest/Approvals` - Approver dashboard showing all pending work
- `POST /ChangeRequest/Approve/{id}` - Approve a request (Change Approvers only)
- `POST /ChangeRequest/Reject/{id}` - Reject with reason (Change Approvers only)
- `GET /ChangeRequest/UpdateStatus/{id}` - Update Approved requests to Complete / On-Hold / Abandoned
- `GET /ChangeRequest/AuditTrail/{id}` - View audit trail timeline

### Authentication
- `GET /Account/Login` - Initiate Auth0 / Entra ID login
- `POST /Account/Logout` - Sign out user and end Auth0 session
- `GET /Account/AccessDenied` - Friendly modal explaining missing group memberships
- `GET /Account/SignedOut` - Confirmation page with re-login option

## Monitoring and Diagnostics

### Application Insights Integration
- **Performance**: Response times and throughput metrics
- **Errors**: Exception tracking and error rates
- **Dependencies**: Database and external service calls
- **Custom Events**: Business logic tracking
- **User Analytics**: Usage patterns and feature adoption

### Logging Configuration
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.ApplicationInsights": "Information"
    }
  }
}
```

### Health Checks
The application includes health check endpoints for:
- Database connectivity
- Azure Storage availability
- Key Vault accessibility

## Troubleshooting Common Issues

### Authentication Problems
- **"invalid_request: returnTo parameter..."**: Ensure Auth0 application's Allowed Logout URLs include `https://localhost:5246/Account/SignedOut` (or your deployed domain).
- **Group-based Access Denied**: Confirm the user belongs to one of the configured Arkham groups. Use the Auth0 logs / Entra group membership to verify claim emission.
- **Email shows instead of full name**: Ensure the SAML assertion includes `http://schemas.microsoft.com/identity/claims/displayname`, `given_name`, or `family_name`.
- **Authentication loop**: Clear browser cache, verify Auth0 domain, client ID/secret, and callback URL configured in `appsettings.*.json` / `.env`.

### Database Issues
- **Connection timeout**: Verify SQL Server firewall allows Azure services (IP range 0.0.0.0-0.0.0.0)
- **Login failed**: Check connection string username/password, ensure SQL authentication is enabled
- **Database not found**: Verify database name matches, apply migrations if needed

### Deployment Problems
- **Build errors**: Ensure .NET 8 SDK installed, check for missing NuGet packages
- **App won't start**: Check Application Insights logs in Azure Portal, verify app settings
- **File upload fails**: Confirm storage account connection string, check blob container exists

### Quick Debugging Commands
```bash
# Check app logs
az webapp log tail --resource-group $RESOURCE_GROUP --name $APP_NAME

# Verify app settings
az webapp config appsettings list --resource-group $RESOURCE_GROUP --name $APP_NAME

# Test database connection
az sql db show --resource-group $RESOURCE_GROUP --server $SQL_SERVER --name ArkhamChangeRequestDb

# Check authentication status
az webapp auth show --resource-group $RESOURCE_GROUP --name $APP_NAME
```

## Contributing & Development

### Contributing Guidelines
This project welcomes contributions focused on Azure integration and deployment improvements. Since this is primarily a learning project for Azure services:

**Preferred Contributions:**
- Azure configuration improvements
- Security enhancements
- Deployment automation
- Documentation updates
- Performance optimizations

**Development Setup:**
1. Fork the repository
2. Copy `.env.example` to `.env` and configure
3. Set up local SQL Server or use Azure SQL Database
4. Run `dotnet restore && dotnet ef database update`
5. Test locally with `dotnet run`

## Learning Outcomes

This project demonstrates practical experience with:

### Azure Services Integration
- **Auth0 + Azure Entra ID**: Enterprise authentication, group claim evaluation, and SAML federation
- **Azure App Service**: Web application hosting with managed identity and deployment slots
- **Azure SQL Database**: Managed database with Entity Framework Core
- **Azure Key Vault**: Secure secrets and configuration management
- **Azure Blob Storage**: File upload and storage solutions
- **Application Insights**: Monitoring and telemetry

### DevOps & Deployment
- **Infrastructure as Code**: Azure CLI automation scripts
- **CI/CD Concepts**: GitHub Actions workflow examples
- **Security Best Practices**: Managed identities, HTTPS enforcement
- **Configuration Management**: Environment-specific settings
- **Monitoring & Diagnostics**: Application health and performance tracking

### Modern Web Development
- **Responsive Design**: Mobile-first CSS with multiple breakpoints
- **Authentication UX**: Claims-based user experience
- **Form Handling**: Validation, file uploads, error management
- **Security Implementation**: Anti-forgery tokens, input sanitization

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support & Resources

### Documentation & Help
- **Azure Documentation**: [Azure App Service](https://docs.microsoft.com/azure/app-service/), [Entra ID](https://docs.microsoft.com/azure/active-directory/)
- **ASP.NET Core**: [Official Documentation](https://docs.microsoft.com/aspnet/core/)
- **Entity Framework**: [EF Core Documentation](https://docs.microsoft.com/ef/core/)

### Support Channels
- **Issues**: GitHub Issues for bugs and feature requests
- **Discussions**: GitHub Discussions for questions and ideas
- **Azure Support**: Azure Portal support for infrastructure issues
