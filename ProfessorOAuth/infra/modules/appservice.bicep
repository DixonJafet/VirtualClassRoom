metadata description = 'Deploys an Azure App Service Plan and Web App for ProfessorOAuth with managed identity and secure configuration.'

@description('Azure region for the App Service.')
param location string

@description('Name of the App Service Plan.')
param appServicePlanName string

@description('Name of the App Service Web App.')
param webAppName string

@description('App Service Plan pricing tier. Prefer B1 (Basic) or higher for Always On production capability.')
@allowed([
  'B1'
  'B2'
  'B3'
  'S1'
  'S2'
  'P0v3'
  'P1v3'
])
param appServicePlanSku string = 'B1'

@description('Canonical public issuer URL (e.g. https://<app-name>.azurewebsites.net/).')
param issuerUrl string

@description('Canonical resource URL for ProfessorAPI MCP.')
param resourceUrl string = 'https://classroomapi.runasp.net/mcp'

@description('Key Vault URI.')
param keyVaultUri string

@description('Name of the OpenIddict signing certificate in Key Vault.')
param signingCertificateName string = 'openiddict-signing'

@description('Name of the OpenIddict encryption certificate in Key Vault.')
param encryptionCertificateName string = 'openiddict-encryption'

@description('Data Protection Blob container URI.')
param dataProtectionBlobContainerUri string

@description('Data Protection Key Vault Key URI.')
param dataProtectionKeyVaultKeyUri string

@description('Application Insights connection string.')
param appInsightsConnectionString string

@description('Optional MySQL connection string or Key Vault reference (@Microsoft.KeyVault(...)).')
@secure()
param mySqlConnectionSetting string = ''

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  properties: {
    reserved: true // Linux
  }
  sku: {
    name: appServicePlanSku
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      healthCheckPath: '/health'
      appSettings: [
        {
          name: 'McpOAuth__Issuer'
          value: issuerUrl
        }
        {
          name: 'McpOAuth__ResourceUrl'
          value: resourceUrl
        }
        {
          name: 'KeyVault__VaultUri'
          value: keyVaultUri
        }
        {
          name: 'KeyVault__SigningCertificateName'
          value: signingCertificateName
        }
        {
          name: 'KeyVault__EncryptionCertificateName'
          value: encryptionCertificateName
        }
        {
          name: 'DataProtection__BlobContainerUri'
          value: dataProtectionBlobContainerUri
        }
        {
          name: 'DataProtection__KeyVaultKeyUri'
          value: dataProtectionKeyVaultKeyUri
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'ConnectionStrings__MySQLConnection'
          value: mySqlConnectionSetting
        }
      ]
    }
  }
}

output webAppId string = webApp.id
output webAppName string = webApp.name
output webAppPrincipalId string = webApp.identity.principalId
output defaultHostName string = webApp.properties.defaultHostName
output canonicalIssuerUrl string = 'https://${webApp.properties.defaultHostName}/'

