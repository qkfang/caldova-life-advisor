@description('Name of the Foundry (AI Services) account')
param name string

@description('Azure region')
param location string

@description('Resource tags')
param tags object = {}

@description('Azure AI Search endpoint to connect to the Foundry project (optional)')
param aiSearchEndpoint string = ''

@description('Azure AI Search resource ID to connect to the Foundry project (optional)')
param aiSearchResourceId string = ''

@description('Application Insights component (same resource group) connected to the Foundry project for agent tracing')
param appInsightsName string

resource foundry 'Microsoft.CognitiveServices/accounts@2025-10-01-preview' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  sku: {
    name: 'S0'
  }
  kind: 'AIServices'
  properties: {
    allowProjectManagement: true
    customSubDomainName: name
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: foundry
  name: '${name}-project'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {}
}

resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  parent: foundry
  name: 'gpt-5.4'
  dependsOn: [
    project
  ]
  // DataZoneStandard has its own quota; the subscription's GlobalStandard gpt-5.4 quota in
  // eastus2 is almost fully used by other workloads. 300 = 300K tokens a minute: the Team
  // Leader group chat and the research runs (MCP tool output) hit 429s at 100K.
  sku: {
    name: 'DataZoneStandard'
    capacity: 300
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-5.4'
      version: '2026-03-05'
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
    raiPolicyName: 'Microsoft.DefaultV2'
  }
}

// Azure AI Search connections — exposed to the Foundry account and to the
// project so the AzureAISearchTool wired up on each GuruAgent can resolve
// the connection by id. Mirrors the quant-agent foundry.bicep.
resource aiSearchConnection 'Microsoft.CognitiveServices/accounts/connections@2025-10-01-preview' = if (aiSearchEndpoint != '') {
  parent: foundry
  name: 'ai-search-connection'
  dependsOn: [
    modelDeployment
  ]
  properties: {
    authType: 'AAD'
    category: 'CognitiveSearch'
    target: aiSearchEndpoint
    metadata: {
      type: 'azure_ai_search'
      ResourceId: aiSearchResourceId
      useWorkspaceManagedIdentity: 'false'
    }
  }
}

resource aiSearchProjectConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-10-01-preview' = if (aiSearchEndpoint != '') {
  parent: project
  name: 'ai-search-connection-project'
  dependsOn: [
    aiSearchConnection
  ]
  properties: {
    authType: 'AAD'
    category: 'CognitiveSearch'
    target: aiSearchEndpoint
    metadata: {
      type: 'azure_ai_search'
      ResourceId: aiSearchResourceId
      useWorkspaceManagedIdentity: 'false'
    }
  }
}

// Application Insights connected to the project: Foundry turns on server-side
// tracing for every agent in the project (the Traces tab in the portal). Same
// shape as the azd azure.ai.projects monitoring connection; the connection
// string is resolved inside ARM and never output. Web grounding needs no
// connection: the agents' web search tool is powered by Web IQ.
resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource appInsightsProjectConnection 'Microsoft.CognitiveServices/accounts/projects/connections@2025-10-01-preview' = {
  parent: project
  name: '${name}-appinsights'
  dependsOn: [
    aiSearchProjectConnection
  ]
  properties: {
    category: 'AppInsights'
    authType: 'ApiKey'
    target: appInsights.id
    credentials: {
      key: appInsights.properties.ConnectionString
    }
    metadata: {
      ApiType: 'Azure'
      ResourceId: appInsights.id
    }
  }
}

output accountName string = foundry.name
output accountEndpoint string = foundry.properties.endpoint
output projectName string = project.name
output projectEndpoint string = project.properties.endpoints['AI Foundry API']
output deploymentName string = modelDeployment.name
output principalId string = foundry.identity.principalId
output projectPrincipalId string = project.identity.principalId
output appInsightsConnectionName string = appInsightsProjectConnection.name
output aiSearchConnectionId string = aiSearchEndpoint != '' ? aiSearchConnection.id : ''
output aiSearchProjectConnectionId string = aiSearchEndpoint != '' ? aiSearchProjectConnection.id : ''
