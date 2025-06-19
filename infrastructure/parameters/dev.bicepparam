using '../main.bicep'

param environment = 'dev'
param location = 'Canada Central'  // Changed from 'Canada East' due to deployment limitation
param uniqueSuffix = 'compass001'
param keyVaultAdminObjectId = '1d86e71f-66b4-4bf6-822e-9c860abeb349'