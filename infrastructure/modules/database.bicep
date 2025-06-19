@description('Base name for resources')
param baseName string

@description('Location for all resources')
param location string

@description('SQL Server administrator login')
param adminLogin string = 'compassadmin'

@description('SQL Server administrator password')
@secure()
param adminPassword string

// SQL Server
resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: 'sql-${baseName}'
  location: location
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
  
  resource firewallRule 'firewallRules@2022-05-01-preview' = {
    name: 'AllowAzureServices'
    properties: {
      startIpAddress: '0.0.0.0'
      endIpAddress: '0.0.0.0'
    }
  }
}

// SQL Database (Serverless for cost optimization)
resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: 'compass-assessments'
  location: location
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 34359738368 // 32GB
    catalogCollation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: false
    readScale: 'Disabled'
    autoPauseDelay: 60 // Auto-pause after 1 hour of inactivity
    minCapacity: json('0.5') // Can scale down to 0.5 vCores
    requestedBackupStorageRedundancy: 'Local'
  }
}

// Outputs (no secrets exposed)
output sqlServerName string = sqlServer.name
output databaseName string = sqlDatabase.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName