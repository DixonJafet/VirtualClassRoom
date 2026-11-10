metadata description = 'Main Bicep deployment orchestrator for ProfessorOAuth on Azure App Service.'

targetScope = 'resourceGroup'

@description('Environment name (e.g. dev, staging, prod).')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environmentName string = 'prod'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Base application name used to generate unique resource names.')
@minLength(3)
@maxLength(16)
param appName string = 'prof-oauth'

@description('App Service Plan pricing tier. B1 (Basic) or higher recommended for Always On.')
param appServicePlanSku string = 'B1'

@description('Optional custom issuer URL. If left empty, defaults to https://<webAppName>.azurewebsites.net/')
param customIssuerUrl string = ''

@description('Canonical resource URL for ProfessorAPI MCP.')
param resourceUrl string = 'https://classroomapi.runasp.net/mcp'

@description('Optional MySQL connection string to securely store in Key Vault and reference in App Service.')
@secure()
param mySqlConnectionString string = ''

// Unique resource names with deterministic hashes
var uniqueSuffix = uniqueString(resourceGroup().id, appName)
var sanitizedBaseName = toLower(appName)
var webAppName = '${sanitizedBaseName}-${environmentName}-${take(uniqueSuffix, 6)}'
var appServicePlanName = 'asp-${sanitizedBaseName}-${environmentName}'
var keyVaultName = 'kv-${take(sanitizedBaseName, 10)}-${environmentName}-${take(uniqueSuffix, 4)}'
var storageAccountName = 'st${take(replace(sanitizedBaseName, '-', ''), 10)}${environmentName}${take(uniqueSuffix, 4)}'
var logAnalyticsName = 'log-${sanitizedBaseName}-${environmentName}'
var appInsightsName = 'appi-${sanitizedBaseName}-${environmentName}'

var finalIssuerUrl = !empty(customIssuerUrl) ? customIssuerUrl : 'https://${webAppName}.azurewebsites.net/'

// 1. Diagnostics & Monitoring (Application Insights + Log Analytics)
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoringDeployment'
  params: {
    location: location
    logAnalyticsWorkspaceName: logAnalyticsName
    appInsightsName: appInsightsName
  }
}

// 2. Storage for ASP.NET Core Data Protection session cookies
module storage 'modules/storage.bicep' = {
  name: 'storageDeployment'
  params: {
    location: location
    storageAccountName: storageAccountName
    containerName: 'dataprotection'
  }
}

// 3. Azure Key Vault for certificates, keys, and DB credentials
module keyVault 'modules/keyvault.bicep' = {
  name: 'keyVaultDeployment'
  params: {
    location: location
    keyVaultName: keyVaultName
    signingCertificateName: 'openiddict-signing'
    encryptionCertificateName: 'openiddict-encryption'
    dataProtectionKeyName: 'dataprotection-key'
    mySqlConnectionString: mySqlConnectionString
  }
}

// 4. App Service Plan & Web App
var mySqlSettingValue = !empty(mySqlConnectionString) ? '@Microsoft.KeyVault(VaultName=${keyVault.outputs.keyVaultName};SecretName=${keyVault.outputs.mySqlSecretName})' : ''

module appService 'modules/appservice.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
    appServicePlanName: appServicePlanName
    webAppName: webAppName
    appServicePlanSku: appServicePlanSku
    issuerUrl: finalIssuerUrl
    resourceUrl: resourceUrl
    keyVaultUri: keyVault.outputs.keyVaultUri
    signingCertificateName: keyVault.outputs.signingCertificateName
    encryptionCertificateName: keyVault.outputs.encryptionCertificateName
    dataProtectionBlobContainerUri: storage.outputs.dataProtectionContainerUri
    dataProtectionKeyVaultKeyUri: keyVault.outputs.dataProtectionKeyUri
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    mySqlConnectionSetting: mySqlSettingValue
  }
}

// 5. Least-privilege RBAC role assignments for App Service Managed Identity
module roleAssignments 'modules/roleassignments.bicep' = {
  name: 'roleAssignmentsDeployment'
  params: {
    principalId: appService.outputs.webAppPrincipalId
    keyVaultName: keyVault.outputs.keyVaultName
    storageAccountName: storage.outputs.storageAccountName
  }
}

// Outputs for deployment validation and handoff (non-sensitive only)
output webAppName string = appService.outputs.webAppName
output publicHostName string = appService.outputs.defaultHostName
output canonicalIssuerUrl string = appService.outputs.canonicalIssuerUrl
output keyVaultName string = keyVault.outputs.keyVaultName
output keyVaultUri string = keyVault.outputs.keyVaultUri
output storageAccountName string = storage.outputs.storageAccountName
output dataProtectionContainerUri string = storage.outputs.dataProtectionContainerUri
output appInsightsName string = appInsightsName
