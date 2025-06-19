@description('Environment name (dev, staging, prod)')
param environment string = 'dev'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Unique suffix for resource names')
param uniqueSuffix string = uniqueString(resourceGroup().id)

@description('Object ID of the user/service principal that will manage Key Vault')
param keyVaultAdminObjectId string

@description('SQL Server administrator password (will be stored in Key Vault)')
@secure()
param adminPassword string

// Resource naming convention
var baseName = 'compass-${environment}-${uniqueSuffix}'

// Key Vault for secret management (deployed first)
module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault-deployment'
  params: {
    baseName: baseName
    location: location
    environment: environment  // Add this line
    keyVaultAdminObjectId: keyVaultAdminObjectId
    sqlAdminPassword: adminPassword
  }
}

// SQL Database for assessment data
module database 'modules/database.bicep' = {
  name: 'database-deployment'
  dependsOn: [keyVault]
  params: {
    baseName: baseName
    location: location
    adminPassword: adminPassword // Still needed for initial deployment
  }
}

// Storage for reports and logs
module storage 'modules/storage.bicep' = {
  name: 'storage-deployment'
  params: {
    baseName: baseName
    location: location
  }
}

// Container Apps for API
module webapp 'modules/webapp.bicep' = {
  name: 'webapp-deployment'
  dependsOn: [database, storage, keyVault]
  params: {
    baseName: baseName
    location: location
    environment: environment
    // Use Key Vault reference for secrets
    databaseConnectionString: 'Server=tcp:${database.outputs.sqlServerFqdn},1433;Initial Catalog=${database.outputs.databaseName};Authentication=Active Directory Default;'
    storageConnectionString: storage.outputs.storageConnectionString
    keyVaultName: keyVault.outputs.keyVaultName
  }
}

// Outputs
output webAppUrl string = webapp.outputs.webAppUrl
output storageAccountName string = storage.outputs.storageAccountName
output sqlServerName string = database.outputs.sqlServerName
output databaseName string = database.outputs.databaseName
output keyVaultName string = keyVault.outputs.keyVaultName