metadata description = 'Deploys an Azure Key Vault with RBAC, purge protection, OpenIddict certificates, and Data Protection key.'

@description('Azure region for Key Vault.')
param location string

@description('Name of the Key Vault (3-24 alphanumeric characters or dashes).')
param keyVaultName string

@description('Name of the OpenIddict signing certificate.')
param signingCertificateName string = 'openiddict-signing'

@description('Name of the OpenIddict encryption certificate.')
param encryptionCertificateName string = 'openiddict-encryption'

@description('Name of the Data Protection wrapping key.')
param dataProtectionKeyName string = 'dataprotection-key'

@description('Optional MySQL connection string to store securely as a Key Vault secret.')
@secure()
param mySqlConnectionString string = ''

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    enablePurgeProtection: true
    softDeleteRetentionInDays: 90
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

// NOTE: Microsoft.KeyVault/vaults/certificates is NOT reliably supported via ARM/Bicep.
// Certificates are created post-deployment via Azure CLI (az keyvault certificate create).
// See the deployment runbook / README for the CLI commands.

// RSA key used to protect ASP.NET Core Data Protection keys at rest
resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: keyVault
  name: dataProtectionKeyName
  properties: {
    kty: 'RSA'
    keySize: 2048
    keyOps: [
      'encrypt'
      'decrypt'
      'wrapKey'
      'unwrapKey'
    ]
  }
}

// Optional secret for MySQL connection string
resource mySqlSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(mySqlConnectionString)) {
  parent: keyVault
  name: 'mysql-connection-string'
  properties: {
    value: mySqlConnectionString
    contentType: 'text/plain'
  }
}

output keyVaultId string = keyVault.id
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output signingCertificateName string = signingCertificateName
output encryptionCertificateName string = encryptionCertificateName
// dataProtectionKeyUri emitted without version so it always resolves to the latest key version
output dataProtectionKeyUri string = dataProtectionKey.properties.keyUri
output mySqlSecretName string = 'mysql-connection-string'
