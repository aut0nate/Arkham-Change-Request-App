#!/bin/bash

# Azure Key Vault Secret Management Script
# This script helps manage secrets in Azure Key Vault

KEY_VAULT_NAME="arkham-ops-kv"

echo "🔐 Azure Key Vault Secret Management"
echo "Key Vault: $KEY_VAULT_NAME"
echo ""

# Function to list all secrets
list_secrets() {
    echo "📋 Listing all secrets in Key Vault..."
    az keyvault secret list --vault-name $KEY_VAULT_NAME --query "[].name" --output table
}

# Function to get a secret value
get_secret() {
    if [ -z "$1" ]; then
        echo "❌ Please provide a secret name"
        echo "Usage: $0 get <secret-name>"
        exit 1
    fi
    
    echo "🔍 Getting secret: $1"
    az keyvault secret show --vault-name $KEY_VAULT_NAME --name "$1" --query "value" --output tsv
}

# Function to set a secret
set_secret() {
    if [ -z "$1" ] || [ -z "$2" ]; then
        echo "❌ Please provide secret name and value"
        echo "Usage: $0 set <secret-name> <secret-value>"
        exit 1
    fi
    
    echo "💾 Setting secret: $1"
    az keyvault secret set --vault-name $KEY_VAULT_NAME --name "$1" --value "$2"
}

# Function to delete a secret
delete_secret() {
    if [ -z "$1" ]; then
        echo "❌ Please provide a secret name"
        echo "Usage: $0 delete <secret-name>"
        exit 1
    fi
    
    echo "🗑️  Deleting secret: $1"
    read -p "Are you sure you want to delete '$1'? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        az keyvault secret delete --vault-name $KEY_VAULT_NAME --name "$1"
        echo "✅ Secret deleted successfully"
    else
        echo "❌ Operation cancelled"
    fi
}

# Function to backup all secrets
backup_secrets() {
    echo "💾 Backing up all secrets..."
    timestamp=$(date +%Y%m%d_%H%M%S)
    backup_file="keyvault_backup_$timestamp.json"
    
    az keyvault secret list --vault-name $KEY_VAULT_NAME --include-values --query "[].{name:name, value:value}" > "$backup_file"
    echo "✅ Secrets backed up to: $backup_file"
    echo "⚠️  Warning: This file contains sensitive data. Store securely and delete when no longer needed."
}

# Function to show current Key Vault permissions
show_permissions() {
    echo "🔑 Current Key Vault access policies and role assignments..."
    echo ""
    echo "Role Assignments:"
    az role assignment list --scope "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/arkham-ops-management/providers/Microsoft.KeyVault/vaults/$KEY_VAULT_NAME" --output table
}

# Main script logic
case "$1" in
    "list"|"ls")
        list_secrets
        ;;
    "get")
        get_secret "$2"
        ;;
    "set")
        set_secret "$2" "$3"
        ;;
    "delete"|"del")
        delete_secret "$2"
        ;;
    "backup")
        backup_secrets
        ;;
    "permissions"|"perms")
        show_permissions
        ;;
    "help"|"--help"|"-h"|"")
        echo "Usage: $0 <command> [arguments]"
        echo ""
        echo "Commands:"
        echo "  list                    List all secret names"
        echo "  get <name>             Get secret value"
        echo "  set <name> <value>     Set secret value"
        echo "  delete <name>          Delete a secret"
        echo "  backup                 Backup all secrets to JSON file"
        echo "  permissions            Show Key Vault permissions"
        echo "  help                   Show this help message"
        echo ""
        echo "Examples:"
        echo "  $0 list"
        echo "  $0 get SqlConnectionString"
        echo "  $0 set NewSecret 'secret-value'"
        echo "  $0 delete OldSecret"
        ;;
    *)
        echo "❌ Unknown command: $1"
        echo "Use '$0 help' for usage information"
        exit 1
        ;;
esac
