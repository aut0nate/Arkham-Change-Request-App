# Arkham Change Management Application

An end-to-end change management portal for a fictitious company named Arkham, with enterprise SSO (Auth0 SAML ↔ Azure Entra ID), approvals, audit trail, attachments, and a mobile-friendly dark UI. Built with ASP.NET Core 8 and deployed using Azure App Service, Azure SQL and Azure Storage.

> **Note**: This application was built with significant assistance from **OpenAI Codex**, demonstrating how AI tooling can accelerate Azure-focused learning and development.

## Project Purpose

This project serves as a comprehensive example of:

- **Azure Services Integration**: Entra ID, App Service, SQL Database, Key Vault, Blob Storage.
- **Enterprise Authentication**: Single sign-on with claims-based user management.
- **Secure Deployment Practices**: Infrastructure as code, secrets management, CI/CD.
- **Modern Web Application**: Responsive design, mobile optimization, security best practices.

The focus is on **Azure cloud architecture and DevOps practices** rather than custom web development.

## Key Features

- **Enterprise Authentication**: Auth0 enterprise SAML → Azure Entra ID integration with fine-grained group checks and claims mapping.
- **Professional UI**: Dark-themed interface matching enterprise standards with mobile-responsive design.
- **Comprehensive Workflow**: Change submission, auto-prefilled requestor data, attachments, approvals dashboard, status updates, and audit trail.
- **File Management**: Secure file upload via Azure Blob Storage (PDF, DOC, DOCX, images).
- **Persistent Storage**: Azure SQL Database with Entity Framework Core.
- **Mobile Optimised**: Touch-friendly interface for iPhone and mobile devices.
- **Real-time Validation**: Form validation with user-friendly error handling and future-dated scheduling guardrails.
- **Security First**: Azure Key Vault integration, HTTPS enforcement, anti-forgery protection, and Auth0 group-based authorization.
- **Monitoring**: Application Insights integration for telemetry and diagnostics.

## Architecture

- **Frontend**: ASP.NET Core 8.0 MVC with responsive CSS and mobile optimization.
- **Authentication**: Azure Entra ID via Auth0 group claims.
- **Backend**: .NET 8 with Entity Framework Core.
- **Database**: Azure SQL Database.
- **File Storage**: Azure Blob Storage with secure container access.
- **Security**: Azure Key Vault for secrets management.
- **Hosting**: Azure App Service with B1 tier.
- **Monitoring**: Application Insights for telemetry and diagnostics.

## Azure Resources

- **Resource Group**: `arkham-change-rg`
- **SQL Server**: `arkhamdb`
- **SQL Database**: `arkham-change`
- **Storage account**: `arkhamchange`
- **App Service**: `arkham-change`:
  - App Insights enabled.
  - Managed identity enabled with the following roles:
    - Key Vault Secrets User on `arkham-kv`.
    - Storage Blob Data Contributor on `arkhamchange`.
- **Key Vault**: `arkham-kv`:
  - `Auth0ClientSecret` containing the Auth0 client secret.
  - `SqlConnectionString` containing the ADO.net connection string, obtained via the `arkham-change` db.

### App Service Environment Variables

Configure the following environment variables, adjusting values according to your specific configuration.

For **Auth0** values, check [Auth0 Configuration](#auth0-configuration) for details.

| Name | Value / Notes |
| --- | --- |
| APPLICATIONINSIGHTS_CONNECTION_STRING | InstrumentationKey="";IngestionEndpoint="" |
| ApplicationInsightsAgent_EXTENSION_VERSION | ~3 |
| ASPNETCORE_HTTPS_PORT | 443 |
| Auth0__AllowedGroupIds__0 | <GROUP_ID> |
| Auth0__AllowedGroupIds__1 | <GROUP_ID> |
| Auth0__AllowedGroups__0 | <GROUP_NAME> |
| Auth0__AllowedGroups__1 | <GROUP_NAME> |
| Auth0__ApproverGroupIds__0 | <GROUP_ID> |
| Auth0__ApproverGroups__0 | <GROUP_NAME>  |
| Auth0__CallbackPath | /callback |
| Auth0__ClientId | <AUTH0_CLIENT_ID> |
| Auth0__ClientSecret | <KEYVAULT_URL> |
| Auth0__Connection | <AUTH0_CONNECTION> |
| Auth0__Domain | <AUTH0_DOMAIN> |
| Auth0__LogoutUrl | <AUTH0_LOGOUT_URL> |
| BlobSettings__AccountName | arkhamchange |
| BlobSettings__ContainerName | changerequests |
| KeyVaultSettings__VaultUrl | <KEYVAULT_URL> |
| SqlConnectionString | <KEYVAULT_URL> |
| XDT_MicrosoftApplicationInsights_Mode | Recommended |

### Custom Domain (Optional)

To configure a custom domain to use with your app, you will need to ensure that you own a domain name and have access to edit the DNS records.

Click on the Custom Domains blade on the App Service and click **"Add custom domain"**.

Select the following options:

- **Domain Provider**: All other domain services.
- **TLS/SSL Certificate**: App Service Manage certificate.
- **TLS/SSL Type**: SNI SSL.
- **Domain**: Enter the domain name.
- **Hostname Record Type**: CNAME.

Make note of the CNAME and TXT record. These records will need to be added in your DNS providers management console.

Once the DNS records have been added, click **"Validate"** and Once validation is complete, click **"Add"**.

To provision an SSL certificate, click **"Add Binding"** and click **"Validate"** to add a new certificate.

Allow some for the validation to complete and resolve the URL.

## Auth0 Configuration

This section describes how to create the Auth0 Application and the SAML Enterprise Connection required for this project.

### 1. Create an Auth0 Application (OIDC Relying Party)

1. In Auth0 Dashboard, go to **Applications → Applications**.
2. Click **Create Application**.
3. Name it "arkham-change-app".
4. Choose **Regular Web Application**.
5. After creation, configure:

   - **Allowed Callback URLs:**

     ```bash
     https://<domain>/callback
     ```

   - **Allowed Logout URLs:**

     ```bash
     https://<domain>/Account/SignedOut
     ```

6. The following values will be entered into your [Azure App Environment Variables](#app-service-environment-variables) or if [deploying locally](#local-development), your appsettings.Development.json file:

- **ISSUER_BASE_URL** (your Auth0 domain).
- **CLIENT_ID** (your Auth0 application client id).
- **SESSION_SECRET** (your Auth0 application secret).

### 2. Create the SAML Enterprise Connection in Auth0

1. Navigate to **Authentication → Enterprise → SAML**.
2. Click **Create Connection**.
3. Name it "arkham-change-app".
4. Under **Settings**, configure:

   - **Sign-In URL** → obtained from Azure (see Azure Entra Configuration below).
   - **Signing Certificate (Base64 CER)** →obtained from Azure (see Azure Entra Configuration below).

5. Under **Application Assignments**, ensure your newly created application is enabled.

## Azure Entra Configuration (Enterprise Application)

This section describes the full process for configuring an Azure Enterprise Application.

### 1. Create the Enterprise Application

In Azure:

1. Go to **Microsoft Entra ID**.
2. Select **Enterprise Applications**.
3. Click **New application**.
4. Choose **Create your own application**.
5. Choose **Integrate any other application you don't find…**.
6. Name it "Arkham Change (Auth0)".

### 2. Enable SAML-based Sign‑on

1. Under **Manage**, select **Single sign-on**.
2. Choose **SAML**.
3. Configure:

   - **Identifier (Entity ID):**

     ```bash
     urn:auth0:YOURTENANT:arkham-change-app
     ```

   - **Reply URL:**

     ```bash
     https://YOUR_AUTH0_DOMAIN/login/callback?connection=arkham-change-app
     ```

### 3. Create Application Groups in Azure

Create your security groups in Azure, for example:

- Arkham - Change Approvers
- Arkham App - Administrator
- Arkham App - Developer
- Arkham App - Report Generator
- Arkham App - Support Agent
- Arkham App - Viewer

Assign these groups to the Enterprise Application under:

Enterprise Application → Users and Groups → Add User/Group.

### 4. Configure the SAML Group Claim

In the Enterprise Application:

1. Go to **Single Sign-On**.
2. Click **Edit** on **Attribute and Claims**.
3. Click **Add a Group Claim**.
4. Choose **Groups assigned to the application**.
5. Choose **Group ID** as the **Source Attribute**.
6. Click Save.

Azure will now emit:

```bash
http://schemas.microsoft.com/ws/2008/06/identity/claims/groups
```

### 5. Configure Auth0 to Map the SAML Group Claim

In Auth0:

1. Go to **Authentication → Enterprise → SAML → arkham-change-app**.
2. Open **Mappings**.
3. Add:

```json
"groups": "http://schemas.microsoft.com/ws/2008/06/identity/claims/groups"
```

This maps Azure's SAML group IDs into the Auth0 user profile.

### 6. Add Group Claims to the ID Token (Auth0 Action)

In Auth0:

1. Go to **Actions → Triggers → Post Login**.
2. Click **Add Action → Create a Custom Action**.
3. Provide the following information:

   - **Name:** AddGroupsToIDToken.
   - **Trigger:** Login/Post Login.
   - **Runtime:** Node 22.
4. Click **Create**.
5. Remove any code in the code block and paste the following:

    ```js
    exports.onExecutePostLogin = async (event, api) => {
    const groups = event.user.groups;
    if (groups) {
        const groupsArray = Array.isArray(groups)
        ? groups
        : String(groups).split(',');
        api.idToken.setCustomClaim("https://arkham.live/groups", groupsArray);
    }
    };
    ```

6. Click **Deploy**.
7. Add this Action to your **Login Flow**:

## Deploying to Azure

```bash
# Publish app.zip
dotnet publish -c Release -o publish
Compress-Archive -Path publish\* -DestinationPath app.zip -Force

# Deploy web app
az webapp deploy --resource-group arkham-change-rg --name arkham-change --src-path app.zip --type zip

# Optional cleanup
Remove-Item publish -Recurse -Force; Remove-Item app.zip
```

## Local Development

To run the application locally follow the below steps:

### Prerequisites

- **.NET 8 SDK**
- **SQL Server**
  - A local instance of SQL Server (Express/Developer) edition.
  - A DB named `ArkhamChangeRequests`.
- **SQL Server Management Studio (SSMS)**
- **VS Code**
- **Azurite**:
  - Install by running `npm install -g azurite`.

### Run Azurite

```bash
azurite --location "C:\\Users\\<user>\\Dev\\Azurite" --silent --debug "C:\\Users\\<user>\\Dev\\Azurite"
```

### appsettings.Development.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=<LOCAL_IP>;Database=ArkhamChangeRequests;User Id=<DB_USER>;Password=<DB_PASSWORD>;Encrypt=False;TrustServerCertificate=True"
  },
  "Storage": {
    "ConnectionString": "UseDevelopmentStorage=true",
    "Container": "arkhamstorageaccount"
  },
  "Auth0": {
    "Domain": "<AUTH0_DOMAIN>",
    "ClientId": "<AUTH0_CLIENT_ID>",
    "ClientSecret": "<AUTH0_SECRET>",
    "Connection": "arkham-change-app",
    "CallbackPath": "/callback",
    "LogoutUrl": "http://localhost:5246",
    "AllowedGroups": [],
    "AllowedGroupIds": [],
    "ApproverGroups": [],
    "ApproverGroupIds": [],
    "PreferredNameClaimTypes": [ "http://schemas.microsoft.com/identity/claims/displayname" ]
  },
  "BlobSettings": {
    "AccountName": "",
    "ContainerName": "arkhamstorageaccount"
  },
  "Services": {
    "Catalog": [
      "Arkham Automate",
      "Arkham AI App Builder",
      "Arkham Consulting",
      "Arkham RPA",
      "Arkham Edge Computing",
      "Arkham Fraud Detect",
      "Other"
    ]
  }
}
```

### Dotnet Commands

```bash
dotnet restore
dotnet run
# open http://localhost:5246
```

## Troubleshooting

- **Access denied (groups)**: Confirm the user is in AllowedGroups/AllowedGroupIds; check Auth0/Entra claims; ensure env vars are set.
- **Name shows as email**: Ensure the SAML assertion includes a display name claim; PreferredNameClaimTypes configured.
- **SQL connectivity**: Check `SqlConnectionString` secret, SQL firewall “Allow Azure services” enabled, DB reachable.
- **Startup hangs during deploy**: Set `ASPNETCORE_HTTPS_PORT=443`; tail logs `az webapp log tail -g arkham-change-rg -n arkham-change`.
- **Blob access**: Managed identity needs Storage Blob Data Contributor on `arkhamchange`; container `changerequests` must exist.
