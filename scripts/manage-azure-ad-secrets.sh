#!/bin/bash

# Azure AD Secrets Management Script for Key Vault
# This script helps manage Azure AD configuration secrets in Azure Key Vault

VAULT_NAME="arkham-ops-kv"
RESOURCE_GROUP="arkham-change-request-rg"
APP_SERVICE_NAME="arkham-change-requests"

echo "🔐 Azure AD Key Vault Secrets Management"
echo "=========================================="

# Function to set secrets
set_secrets() {
    echo "📝 Setting Azure AD secrets in Key Vault..."
    
    read -p "Enter Azure AD Tenant ID: " TENANT_ID
    read -p "Enter Azure AD Client ID: " CLIENT_ID
    read -s -p "Enter Azure AD Client Secret: " CLIENT_SECRET
    echo ""
    
    # Set secrets in Key Vault
    az keyvault secret set --vault-name "$VAULT_NAME" --name "AzureAd--TenantId" --value "$TENANT_ID"
    az keyvault secret set --vault-name "$VAULT_NAME" --name "AzureAd--ClientId" --value "$CLIENT_ID"
    az keyvault secret set --vault-name "$VAULT_NAME" --name "AzureAd--ClientSecret" --value "$CLIENT_SECRET"
    
    echo "✅ Secrets set successfully!"
}

# Function to get secrets
get_secrets() {
    echo "📋 Retrieving Azure AD secrets from Key Vault..."
    
    TENANT_ID=$(az keyvault secret show --vault-name "$VAULT_NAME" --name "AzureAd--TenantId" --query "value" -o tsv 2>/dev/null)
    CLIENT_ID=$(az keyvault secret show --vault-name "$VAULT_NAME" --name "AzureAd--ClientId" --query "value" -o tsv 2>/dev/null)
    
    echo "Tenant ID: ${TENANT_ID:-'Not set'}"
    echo "Client ID: ${CLIENT_ID:-'Not set'}"
    echo "Client Secret: $([ -n "$(az keyvault secret show --vault-name "$VAULT_NAME" --name "AzureAd--ClientSecret" --query "value" -o tsv 2>/dev/null)" ] && echo "✅ Set" || echo "❌ Not set")"
}

# Function to configure App Service
configure_app_service() {
    echo "⚙️  Configuring App Service to use Key Vault references..."
    
    az webapp config appsettings set \
        --resource-group "$RESOURCE_GROUP" \
        --name "$APP_SERVICE_NAME" \
        --settings \
        "AzureAd__TenantId=@Microsoft.KeyVault(VaultName=$VAULT_NAME;SecretName=AzureAd--TenantId)" \
        "AzureAd__ClientId=@Microsoft.KeyVault(VaultName=$VAULT_NAME;SecretName=AzureAd--ClientId)" \
        "AzureAd__ClientSecret=@Microsoft.KeyVault(VaultName=$VAULT_NAME;SecretName=AzureAd--ClientSecret)"
    
    echo "✅ App Service configured with Key Vault references!"
}

# Function to test access
test_access() {
    echo "🧪 Testing Key Vault access..."
    
    if az keyvault secret show --vault-name "$VAULT_NAME" --name "AzureAd--TenantId" --query "value" -o tsv >/dev/null 2>&1; then
        echo "✅ Key Vault access successful"
        get_secrets
    else
        echo "❌ Cannot access Key Vault. Check your permissions."
        echo "   Ensure you have Key Vault Secrets User role or similar."
    fi
}

# Function to rotate client secret
rotate_secret() {
    echo "🔄 Rotating Azure AD Client Secret..."
    echo "⚠️  This will require updating the secret in Azure Portal first!"
    echo ""
    
    read -p "Have you created a new client secret in Azure Portal? (y/N): " CONFIRM
    if [[ $CONFIRM =~ ^[Yy]$ ]]; then
        read -s -p "Enter new client secret: " NEW_SECRET
        echo ""
        
        # Set new secret
        az keyvault secret set --vault-name "$VAULT_NAME" --name "AzureAd--ClientSecret" --value "$NEW_SECRET"
        echo "✅ Client secret rotated successfully!"
        echo "🔄 App Service will automatically pick up the new secret."
    else
        echo "❌ Please create a new client secret in Azure Portal first."
    fi
}

# Main menu
case "$1" in
    "set")
        set_secrets
        ;;
    "get")
        get_secrets
        ;;
    "configure-app")
        configure_app_service
        ;;
    "test")
        test_access
        ;;
    "rotate")
        rotate_secret
        ;;
    *)
        echo "Usage: $0 {set|get|configure-app|test|rotate}"
        echo ""
        echo "Commands:"
        echo "  set           - Set Azure AD secrets in Key Vault"
        echo "  get           - Display current Azure AD secrets"
        echo "  configure-app - Configure App Service with Key Vault references"
        echo "  test          - Test Key Vault access"
        echo "  rotate        - Rotate the client secret"
        echo ""
        echo "Examples:"
        echo "  $0 get                    # Show current secrets"
        echo "  $0 test                   # Test Key Vault access"
        echo "  $0 configure-app          # Configure production App Service"
        exit 1
        ;;
esac
