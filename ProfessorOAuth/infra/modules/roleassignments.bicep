metadata description = 'Assigns least-privilege RBAC roles for ProfessorOAuth managed identity to Key Vault and Storage.'

@description('Principal ID of the App Service managed identity.')
param principalId string

@description('Name of the Key Vault resource.')
param keyVaultName string

@description('Name of the Storage Account resource.')
param storageAccountName string

// Built-in Azure RBAC Role IDs (verified via az role definition list)
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var keyVaultCryptoEncryptionUserRoleId = 'e147488a-f6f5-4113-8e2d-b22465e65bf6'
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

// Grant managed identity permission to read secrets (download PKCS#12 certificates and DB secret)
resource kvSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, principalId, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

// Grant managed identity permission to wrap/unwrap Data Protection keys
resource kvCryptoUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, principalId, keyVaultCryptoEncryptionUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCryptoEncryptionUserRoleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

// Grant managed identity permission to write/read Data Protection session keys in blob storage
resource storageDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, principalId, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

