@description('Base name for resources')
param baseName string

@description('Location for all resources')
param location string

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: 'st${replace(baseName, '-', '')}' // Storage names can't have dashes
  location: location
  sku: {
    name: 'Standard_LRS' // Locally redundant, cheapest option
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false // Security best practice
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowSharedKeyAccess: true
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// Blob containers for different types of content
resource assessmentReportsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  name: '${storageAccount.name}/default/assessment-reports'
  properties: {
    publicAccess: 'None'
    metadata: {
      description: 'Assessment reports in PDF and Excel format'
    }
  }
}

resource customerDataContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  name: '${storageAccount.name}/default/customer-data'
  properties: {
    publicAccess: 'None'
    metadata: {
      description: 'Customer-specific assessment data and configurations'
    }
  }
}

resource templatesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  name: '${storageAccount.name}/default/templates'
  properties: {
    publicAccess: 'None'
    metadata: {
      description: 'Report templates and naming convention standards'
    }
  }
}

// Output for other modules
output storageAccountName string = storageAccount.name
output storageAccountKey string = storageAccount.listKeys().keys[0].value
output storageConnectionString string = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
output assessmentReportsContainerName string = 'assessment-reports'
output customerDataContainerName string = 'customer-data'
output templatesContainerName string = 'templates'