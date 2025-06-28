#!/bin/bash

# Test OAuth Configuration and Infrastructure
# Verifies all components are ready for OAuth delegation

set -e

ENVIRONMENT="dev"
RESOURCE_GROUP="rg-compass-dev"
TEST_ORG_ID="testorg"

echo "🧪 Testing OAuth Configuration and Infrastructure"
echo "=================================================="
echo ""

# Step 1: Verify main infrastructure
echo "📋 Step 1: Verify Main Infrastructure"
echo "-------------------------------------"

MAIN_KEYVAULT=$(az keyvault list \
  --resource-group "$RESOURCE_GROUP" \
  --query "[?starts_with(name, 'kv-${ENVIRONMENT}-')].name" \
  --output tsv | head -1)

if [ -z "$MAIN_KEYVAULT" ]; then
  echo "❌ Main Key Vault not found"
  exit 1
fi

echo "✅ Main Key Vault found: $MAIN_KEYVAULT"

# Check OAuth secrets in main Key Vault
echo "🔍 Checking OAuth secrets..."
OAUTH_SECRETS=("oauth-client-id" "oauth-client-secret" "oauth-tenant-id")

for secret in "${OAUTH_SECRETS[@]}"; do
  if az keyvault secret show --vault-name "$MAIN_KEYVAULT" --name "$secret" --output none 2>/dev/null; then
    echo "✅ Secret exists: $secret"
  else
    echo "❌ Missing secret: $secret"
    exit 1
  fi
done

# Step 2: Test MSP Key Vault deployment
echo ""
echo "📦 Step 2: Test MSP Key Vault Deployment"
echo "----------------------------------------"

# Use short org ID for Key Vault naming (8 chars max, alphanumeric only)
ORG_SHORT=$(echo "$TEST_ORG_ID" | tr '[:upper:]' '[:lower:]' | sed 's/[^a-z0-9]//g' | cut -c1-8)
MSP_KEYVAULT_NAME="kv-${ENVIRONMENT}-${ORG_SHORT}-cmp001"
echo "Testing deployment of: $MSP_KEYVAULT_NAME"

# Deploy test MSP Key Vault
./deploy-msp-keyvault.sh "$TEST_ORG_ID" "$ENVIRONMENT" "$RESOURCE_GROUP"

if az keyvault show --name "$MSP_KEYVAULT_NAME" --output none 2>/dev/null; then
  echo "✅ MSP Key Vault deployment successful"
else
  echo "❌ MSP Key Vault deployment failed"
  exit 1
fi

# Step 3: Test Key Vault access patterns
echo ""
echo "🔐 Step 3: Test Key Vault Access"
echo "--------------------------------"

# Test storing a mock OAuth token
TEST_CLIENT_ID="test-client-12345"
TEST_SECRET_NAME="client-${TEST_CLIENT_ID}-oauth-tokens"
TEST_TOKEN_DATA='{"accessToken":"test-token","refreshToken":"test-refresh","expiresAt":"2024-12-31T23:59:59Z","scope":"test-scope","storedAt":"2024-01-01T00:00:00Z","clientId":"'$TEST_CLIENT_ID'","clientName":"Test Client"}'

echo "📝 Storing test OAuth token..."
az keyvault secret set \
  --vault-name "$MSP_KEYVAULT_NAME" \
  --name "$TEST_SECRET_NAME" \
  --value "$TEST_TOKEN_DATA" \
  --output none

echo "🔍 Retrieving test OAuth token..."
RETRIEVED_TOKEN=$(az keyvault secret show \
  --vault-name "$MSP_KEYVAULT_NAME" \
  --name "$TEST_SECRET_NAME" \
  --query "value" \
  --output tsv)

if [ "$RETRIEVED_TOKEN" = "$TEST_TOKEN_DATA" ]; then
  echo "✅ Token storage and retrieval working"
else
  echo "❌ Token storage/retrieval failed"
  exit 1
fi

# Clean up test token
az keyvault secret delete \
  --vault-name "$MSP_KEYVAULT_NAME" \
  --name "$TEST_SECRET_NAME" \
  --output none

# Step 4: Test OAuth configuration values
echo ""
echo "⚙️  Step 4: Test OAuth Configuration"
echo "-----------------------------------"

# Check if backend is running
if curl -ks https://localhost:7163/health > /dev/null 2>&1; then
  echo "✅ Backend API is running"
  
  # Test OAuth configuration endpoint (if it exists)
  echo "🔍 Testing OAuth configuration..."
  # This would require a test endpoint in the API
  echo "⚠️  Manual verification needed: OAuth config in appsettings.json"
else
  echo "⚠️  Backend API not running - start with 'dotnet run'"
fi

# Step 5: Clean up test resources
echo ""
echo "🧹 Step 5: Cleanup Test Resources"
echo "---------------------------------"

echo "Removing test MSP Key Vault..."
az keyvault delete --name "$MSP_KEYVAULT_NAME" --output none
az keyvault purge --name "$MSP_KEYVAULT_NAME" --output none 2>/dev/null || true

echo "✅ Test cleanup complete"

# Summary
echo ""
echo "📊 OAuth Configuration Test Results"
echo "===================================="
echo "✅ Main Key Vault accessible"
echo "✅ OAuth secrets present"
echo "✅ MSP Key Vault deployment working"
echo "✅ Token storage/retrieval working"
echo "⚠️  Backend API verification (manual)"
echo ""
echo "🎯 Ready for OAuth delegation testing!"
echo ""
echo "🔧 Next steps:"
echo "   1. Start backend API: cd Compass/Compass.Api && dotnet run"
echo "   2. Test OAuth initiation via API"
echo "   3. Complete OAuth consent flow"
echo "   4. Verify token storage in production MSP Key Vault"
echo ""