#!/bin/bash

# Azure App Registration Configuration Script for Arkham Change Request
# Run this script after creating the App Registration in Azure Portal

echo "🔧 Configuring Arkham Change Request App Registration..."

# Variables - Update these with your values
APP_NAME="Arkham Change Request"
REDIRECT_URIS=(
    "https://localhost:7086/signin-oidc"
    "https://arkham-change-requests.azurewebsites.net/signin-oidc"
)
LOGOUT_URIS=(
    "https://localhost:7086/signout-callback-oidc"
    "https://arkham-change-requests.azurewebsites.net/signout-callback-oidc"
)

echo "📋 Please ensure you have the following information ready:"
echo "   - Application (client) ID from Azure Portal"
echo "   - Directory (tenant) ID from Azure Portal"
echo "   - Client Secret (if using confidential client)"
echo ""

# Get App Registration details
echo "🔍 Looking up App Registration..."
APP_ID=$(az ad app list --display-name "$APP_NAME" --query "[0].appId" -o tsv)

if [ -z "$APP_ID" ]; then
    echo "❌ App Registration '$APP_NAME' not found. Please create it in Azure Portal first."
    exit 1
fi

echo "✅ Found App Registration: $APP_ID"

# Configure redirect URIs
echo "🔗 Configuring redirect URIs..."
REDIRECT_JSON=$(printf '%s\n' "${REDIRECT_URIS[@]}" | jq -R . | jq -s .)
LOGOUT_JSON=$(printf '%s\n' "${LOGOUT_URIS[@]}" | jq -R . | jq -s .)

# Update the app registration
az ad app update --id $APP_ID \
    --web-redirect-uris $REDIRECT_JSON \
    --web-logout-url $LOGOUT_JSON \
    --enable-id-token-issuance true

echo "✅ Updated redirect URIs and enabled ID token issuance"

# Grant permissions
echo "🔐 Configuring API permissions..."
az ad app permission add --id $APP_ID --api 00000003-0000-0000-c000-000000000000 --api-permissions 37f7f235-527c-4136-accd-4a02d197296e=Scope
az ad app permission add --id $APP_ID --api 00000003-0000-0000-c000-000000000000 --api-permissions 14dad69e-099b-42c9-810b-d002981feec1=Scope
az ad app permission add --id $APP_ID --api 00000003-0000-0000-c000-000000000000 --api-permissions 64a6cdd6-aab1-4aaf-94b8-3cc8405e90d0=Scope

echo "✅ Added required Microsoft Graph permissions"

# Get tenant information
TENANT_ID=$(az account show --query tenantId -o tsv)
echo ""
echo "📝 Configuration Values:"
echo "   Application (client) ID: $APP_ID"
echo "   Directory (tenant) ID: $TENANT_ID"
echo ""
echo "🔄 Next steps:"
echo "1. Update your appsettings.json with these values"
echo "2. If using client secret, create one in Azure Portal > Certificates & secrets"
echo "3. Store sensitive values in Azure Key Vault for production"
echo "4. Grant admin consent for permissions if required"
echo ""
echo "📄 Update appsettings.json:"
echo '{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "arkhamit.onmicrosoft.com",
    "TenantId": "'$TENANT_ID'",
    "ClientId": "'$APP_ID'",
    "ClientSecret": "YOUR-CLIENT-SECRET-IF-NEEDED",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc"
  }
}'

echo ""
echo "✅ Configuration complete!"
