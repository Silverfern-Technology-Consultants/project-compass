@description('Base name for resources')
param baseName string

@description('Location for all resources')
param location string

@description('Environment name (dev, staging, prod)')
param environment string

@description('Object ID of the user/service principal that will manage secrets')
param keyVaultAdminObjectId string

@description('SQL admin password to store securely')
@secure()
param sqlAdminPassword string

// Simple Key Vault name
var keyVaultName = 'kv-${environment}-${take(uniqueString(resourceGroup().id), 8)}'

// Key Vault for storing secrets
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: tenant().tenantId
    enabledForTemplateDeployment: true
    accessPolicies: [
      {
        tenantId: tenant().tenantId
        objectId: keyVaultAdminObjectId
        permissions: {
          secrets: ['get', 'list', 'set', 'delete']
        }
      }
    ]
    publicNetworkAccess: 'Enabled'
  }
}

// Outputs (no secret URI for now)
output keyVaultName string = keyVault.name
output keyVaultId string = keyVault.id