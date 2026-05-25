param location string = 'italynorth'

// Namn på din befintliga miljö och dess resursgrupp
param existingEnvName string = 'env-joco-inventory'
param existingEnvRg string = 'rg-joco-dev'

// Namn på ditt nya ACR och dina nya appar
param acrName string = 'acrisastudent${uniqueString(resourceGroup().id)}'
param serviceAName string = 'ca-intelligent-sales-api'
param serviceBName string = 'ca-intelligent-content-engine'

// Vi hämtar din EXISTERANDE miljö så att vi kan lägga till appar i den
resource containerAppEnv 'Microsoft.App/managedEnvironments@2023-05-01' existing = {
  name: existingEnvName
  scope: resourceGroup(existingEnvRg)
}

// RESURS 1: Azure Container Registry (ACR)
resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

// RESURS 2: Service A (IntelligentSalesAssistantAPI)
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
      }
    }
    template: {
      containers: [
        {
          name: 'sales-assistant-api'
          image: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
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

// RESURS 3: Service B (IntelligentSalesAssistant.ContentEngine)
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
        external: false
        targetPort: 8080
        allowInsecure: false
      }
    }
    template: {
      containers: [
        {
          name: 'content-engine'
          image: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
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

var acrPullRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ef3-4680-a075-32614d47b0d4')

resource acrPullAssignmentServiceA 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, containerAppServiceA.id, acrPullRoleDefinitionId)
  scope: acr
  properties: {
    roleDefinitionId: acrPullRoleDefinitionId
    principalId: containerAppServiceA.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullAssignmentServiceB 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, containerAppServiceB.id, acrPullRoleDefinitionId)
  scope: acr
  properties: {
    roleDefinitionId: acrPullRoleDefinitionId
    principalId: containerAppServiceB.identity.principalId
    principalType: 'ServicePrincipal'
  }
}