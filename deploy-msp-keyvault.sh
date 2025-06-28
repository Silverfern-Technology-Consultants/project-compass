#!/bin/bash

# Deploy MSP Key Vault for OAuth token storage
# Usage: ./deploy-msp-keyvault.sh [organization-id] [environment] [resource-group]
# NOTE: This script is safe for GitHub - no secrets or sensitive data

set -e

# Parameters - customize these for your deployment
ORGANIZATION_ID=${1:-""}
ENVIRONMENT=${2:-"dev"}
RESOURCE_GROUP=${3:-"rg-compass-dev"}
UNIQUE_SUFFIX="cmp001"
LOCATION="Canada Central"

# Validation
if [ -z "$ORGANIZATION_ID" ]; then
  echo "❌ Error: Organization ID is required"
  echo "Usage: ./deploy-msp-keyvault.sh [organization-id] [environment] [resource-group]"
  echo "Example: ./deploy-msp-keyvault.sh testorg dev rg-compass-dev"
  exit 1
fi

# Verify Azure CLI is logged in
if ! az account show >/dev/null 2>&1; then
  echo "❌ Error: Please login to Azure CLI first"
  echo "Run: az login"
  exit 1
fi

# Create short organization identifier (max 8 chars) for Key Vault naming
ORG_SHORT=$(echo "$ORGANIZATION_ID" | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9]//g' | cut -c1-8)

# MSP Key Vault name - keeping under 24 char limit
MSP_KEYVAULT_NAME="kv-${ENVIRONMENT}-${ORG_SHORT}-${UNIQUE_SUFFIX}"

echo "🚀 Deploying MSP Key Vault for OAuth tokens"
echo "   Organization: $ORGANIZATION_ID (short: $ORG_SHORT)"
echo "   Key Vault: $MSP_KEYVAULT_NAME (${#MSP_KEYVAULT_NAME} chars)"
echo "   Environment: $ENVIRONMENT"
echo "   Resource Group: $RESOURCE_GROUP"

# Validate Key Vault name length
if [ ${#MSP_KEYVAULT_NAME} -gt 24 ]; then
  echo "❌ Error: Key Vault name too long (${#MSP_KEYVAULT_NAME} chars, max 24)"
  echo "💡 Try shorter organization ID or contact admin"
  exit 1
fi

echo ""

# Get the main Key Vault name from the infrastructure
echo "🔍 Getting main Key Vault and app service principal..."
MAIN_KEYVAULT_NAME=$(az keyvault list \
  --resource-group "$RESOURCE_GROUP" \
  --query "[?starts_with(name, 'kv-${ENVIRONMENT}-')].name" \
  --output tsv | head -1)

if [ -z "$MAIN_KEYVAULT_NAME" ]; then
  echo "❌ Main Key Vault not found in resource group $RESOURCE_GROUP"
  echo "💡 Make sure the main infrastructure is deployed first"
  exit 1
fi

echo "📝 Using main Key Vault: $MAIN_KEYVAULT_NAME"

# Try to get app service principal from main Key Vault, fallback to current user
COMPASS_APP_OBJECT_ID=$(az keyvault secret show \
  --vault-name "$MAIN_KEYVAULT_NAME" \
  --name "compass-app-object-id" \
  --query "value" \
  --output tsv 2>/dev/null || echo "")

if [ -z "$COMPASS_APP_OBJECT_ID" ]; then
  echo "⚠️  Compass app object ID not found in main Key Vault. Using current user..."
  COMPASS_APP_OBJECT_ID=$(az ad signed-in-user show --query id --output tsv)
  echo "📝 Using current user object ID: $COMPASS_APP_OBJECT_ID"
fi

# Check if Key Vault already exists
if az keyvault show --name "$MSP_KEYVAULT_NAME" --output none 2>/dev/null; then
  echo "✅ MSP Key Vault $MSP_KEYVAULT_NAME already exists"
else
  echo "🔨 Creating MSP Key Vault..."
  
  # Deploy using Bicep template
  az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --template-file "infrastructure/modules/msp-keyvault.bicep" \
    --parameters \
      environment="$ENVIRONMENT" \
      location="$LOCATION" \
      mspIdentifier="$ORG_SHORT" \
      uniqueSuffix="$UNIQUE_SUFFIX" \
      compassAppObjectId="$COMPASS_APP_OBJECT_ID" \
    --output table

  echo "✅ MSP Key Vault deployed successfully!"
fi

# Verify Key Vault access
echo "🔍 Testing Key Vault access..."
TEST_SECRET_NAME="deploy-test-$(date +%s)"
az keyvault secret set \
  --vault-name "$MSP_KEYVAULT_NAME" \
  --name "$TEST_SECRET_NAME" \
  --value "test-deployment-success" \
  --output none

az keyvault secret delete \
  --vault-name "$MSP_KEYVAULT_NAME" \
  --name "$TEST_SECRET_NAME" \
  --output none

echo "✅ MSP Key Vault access verified!"

# Output summary
echo ""
echo "🎯 MSP Key Vault Deployment Complete"
echo "===================================="
echo "   Name: $MSP_KEYVAULT_NAME"
echo "   URI: https://$MSP_KEYVAULT_NAME.vault.azure.net/"
echo "   Organization: $ORGANIZATION_ID"
echo "   Short ID: $ORG_SHORT"
echo "   Environment: $ENVIRONMENT"
echo ""
echo "🔧 Next Steps:"
echo "   1. Update OAuthService to use short org identifier"
echo "   2. Test OAuth flow with this organization"
echo "   3. Verify token storage in Key Vault"
echo ""