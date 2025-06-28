@description('Environment name (dev, staging, prod)')
param environment string

@description('Location for all resources')
param location string

@description('MSP organization identifier (e.g., cybermsp)')
param mspIdentifier string

@description('Unique suffix for resource names')
param uniqueSuffix string

@description('Silverfern main app service principal object ID for access')
param compassAppObjectId string

// MSP-specific Key Vault name following pattern: kv-{env}-{msp}-{suffix}
var mspKeyVaultName = 'kv-${environment}-${mspIdentifier}-${uniqueSuffix}'

// MSP-specific Key Vault for OAuth tokens
resource mspKeyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: mspKeyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: tenant().tenantId
    enabledForTemplateDeployment: false // MSP vaults don't need ARM template access
    enabledForDiskEncryption: false
    enabledForDeployment: false
    enableSoftDelete: false
    softDeleteRetentionInDays: 7 // Minimum for dev, increase for prod
    enablePurgeProtection: true // Allow for dev cleanup, enable for prod
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
    accessPolicies: [
      {
        tenantId: tenant().tenantId
        objectId: compassAppObjectId
        permissions: {
          secrets: ['get', 'list', 'set', 'delete']
        }
      }
    ]
  }
  
  tags: {
    Purpose: 'MSP-OAuth-Tokens'
    MSP: mspIdentifier
    Environment: environment
    CreatedBy: 'Silverfern-Compass'
  }
}

// Output for deployment tracking and access
output mspKeyVaultName string = mspKeyVault.name
output mspKeyVaultId string = mspKeyVault.id
output mspKeyVaultUri string = mspKeyVault.properties.vaultUri
output mspIdentifier string = mspIdentifier