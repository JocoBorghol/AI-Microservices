// =============================================================================
// ISA – AI Content Assistant
// Infrastructure-as-Code för privat Azure-prenumeration
//
// Skapar en komplett, skoloberoende miljö i rg-isa-production:
//   • Log Analytics Workspace
//   • Container App Environment (egen, inte skolmiljön)
//   • Storage Account + File Share (SQLite-volym för Service A)
//   • Azure Container Registry  (Managed Identity – ingen admin-användare)
//   • Azure Key Vault            (RBAC-modell)
//   • Container App: ca-intelligent-sales-api     (extern ingress)
//   • Container App: ca-intelligent-content-engine (intern ingress)
//   • Rolluppdelningar: AcrPull + Key Vault Secrets User för båda appar
// =============================================================================

// ── Parametrar ────────────────────────────────────────────────────────────────

@description('Azure-region för alla resurser.')
param location string = 'swedencentral'

@description('Suffix som används för att skilja miljöer åt (prod, staging).')
param environmentSuffix string = 'prod'

@description('Object ID för GitHub Actions Service Principal (får Key Vault Secrets Officer).')
@minLength(36)
@maxLength(36)
param githubActionsPrincipalId string

// ── Namnvariabler (unika per resursgrupp) ─────────────────────────────────────

var suffix        = uniqueString(resourceGroup().id)
var acrName       = 'acrisaprod${suffix}'                  // max 50 tecken, globalt unikt
var kvName        = 'kv-isa-${take(suffix, 10)}'           // 3-24 tecken, globalt unikt
var storageName   = 'stisaprod${take(suffix, 13)}'         // max 24 tecken, globalt unikt
var logWsName     = 'log-isa-${environmentSuffix}'
var caEnvName     = 'env-isa-${environmentSuffix}'
var serviceAName  = 'ca-intelligent-sales-api'
var serviceBName  = 'ca-intelligent-content-engine'

// ── Inbyggda roll-ID:n (ändras aldrig i Azure) ───────────────────────────────

var acrPullRoleId           = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ef3-4680-a075-32614d47b0d4')
var kvSecretsUserRoleId     = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
var kvSecretsOfficerRoleId  = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7')

// =============================================================================
// RESURS 1: Log Analytics Workspace (krävs av Container App Environment)
// =============================================================================

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logWsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// =============================================================================
// RESURS 2: Container App Environment (egen – INTE skolmiljön)
// =============================================================================

resource containerAppEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: caEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// =============================================================================
// RESURS 3: Storage Account + File Share (SQLite-persistent volym för Service A)
// =============================================================================

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
  }
}

resource fileShareService 'Microsoft.Storage/storageAccounts/fileServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource sqliteFileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-01-01' = {
  parent: fileShareService
  name: 'sqliteshare'
  properties: {
    shareQuota: 1    // 1 GiB – räcker för SQLite
    enabledProtocols: 'SMB'
  }
}

// Registrera File Share med Container App Environment
resource envStorage 'Microsoft.App/managedEnvironments/storages@2023-05-01' = {
  parent: containerAppEnv
  name: 'sqliteshare'
  properties: {
    azureFile: {
      accountName: storageAccount.name
      accountKey: storageAccount.listKeys().keys[0].value
      shareName: sqliteFileShare.name
      accessMode: 'ReadWrite'
    }
  }
}

// =============================================================================
// RESURS 4: Azure Container Registry
// adminUserEnabled = false → Managed Identity används för autentisering (säkrare)
// =============================================================================

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

// =============================================================================
// RESURS 5: Azure Key Vault (RBAC-modell, inte access policies)
// Hemligheter laddas upp av GitHub Actions-pipelinen efter infra-deployment.
// =============================================================================

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: kvName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true       // Modern RBAC – inga access policies
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enabledForTemplateDeployment: false
  }
}

// =============================================================================
// RESURS 6: Container App – Service A (ca-intelligent-sales-api)
//
// Konfigureras med placeholder-image och registries-block för ACR via MI.
// Faktiska hemligheter och KV-referenser sätts av GitHub Actions (deploy.yml)
// efter att infrastrukturen är skapad och rollerna tilldelade.
// =============================================================================

resource containerAppServiceA 'Microsoft.App/containerApps@2023-05-01' = {
  name: serviceAName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
    }
    template: {
      containers: [
        {
          name: 'sales-assistant-api'
          // Placeholder-image – mcr.microsoft.com/dotnet/samples:aspnetapp lyssnar på port 8080 vilket matchar targetPort
          image: 'mcr.microsoft.com/dotnet/samples:aspnetapp'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          volumeMounts: [
            {
              volumeName: 'sqlite-volume'
              mountPath: '/app/Data'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
      volumes: [
        {
          name: 'sqlite-volume'
          storageType: 'AzureFile'
          storageName: 'sqliteshare'
        }
      ]
    }
  }
  dependsOn: [
    envStorage    // File Share måste vara registrerat innan appen skapas
  ]
}

// =============================================================================
// RESURS 7: Container App – Service B (ca-intelligent-content-engine)
//
// Intern ingress – nås av Service A via http://ca-intelligent-content-engine
// =============================================================================

resource containerAppServiceB 'Microsoft.App/containerApps@2023-05-01' = {
  name: serviceBName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: false           // Intern – inte nåbar utifrån internet
        targetPort: 8080
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
    }
    template: {
      containers: [
        {
          name: 'content-engine'
          image: 'mcr.microsoft.com/dotnet/samples:aspnetapp'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 2
      }
    }
  }
}

// =============================================================================
// ROLLUPPDELNINGAR – Service A Managed Identity
// =============================================================================

// AcrPull: Service A får hämta images från ACR
resource acrPullAssignmentServiceA 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, containerAppServiceA.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: containerAppServiceA.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Key Vault Secrets User: Service A får läsa hemligheter ur Key Vault
resource kvSecretsUserAssignmentServiceA 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, containerAppServiceA.id, kvSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: kvSecretsUserRoleId
    principalId: containerAppServiceA.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// =============================================================================
// ROLLUPPDELNINGAR – Service B Managed Identity
// =============================================================================

// AcrPull: Service B får hämta images från ACR
resource acrPullAssignmentServiceB 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, containerAppServiceB.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: containerAppServiceB.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Key Vault Secrets User: Service B får läsa hemligheter ur Key Vault
resource kvSecretsUserAssignmentServiceB 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, containerAppServiceB.id, kvSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: kvSecretsUserRoleId
    principalId: containerAppServiceB.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Key Vault Secrets Officer: GitHub Actions SP får ladda upp/uppdatera hemligheter
// (används av deploy.yml steg "Upload secrets to Key Vault")
resource kvSecretsOfficerAssignmentGitHub 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, githubActionsPrincipalId, kvSecretsOfficerRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: kvSecretsOfficerRoleId
    principalId: githubActionsPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// =============================================================================
// OUTPUTS – används av GitHub Actions deploy.yml
// =============================================================================

@description('Inloggningsserver för ACR (används vid docker push).')
output acrLoginServer string = acr.properties.loginServer

@description('Resursnamn för ACR (används vid az acr login).')
output acrName string = acr.name

@description('URI till Key Vault (används vid az keyvault secret set).')
output keyVaultUri string = keyVault.properties.vaultUri

@description('Resursnamn för Key Vault.')
output keyVaultName string = keyVault.name

@description('Publik FQDN för Service A (din backend-API).')
output serviceAFqdn string = containerAppServiceA.properties.configuration.ingress.fqdn

@description('Namn på Container App Environment.')
output containerAppEnvName string = containerAppEnv.name
