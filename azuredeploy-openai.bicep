@description('Location where all resources will be deployed. This value defaults to the **East US** region.')
@allowed([
  'australiaeast'
  'westeurope'
  'japaneast'
  'uksouth'
  'eastus'
  'eastus2'
  'southcentralus'
])
param location string = 'eastus'

@description('Name for Open AI Service')
@maxLength(15)
param openAiName string

@description('Specifies the SKU for the Azure OpenAI resource. Defaults to **S0**')
@allowed([
  'S0'
])
param openAiSku string = 'S0'


var openAiSettings = {
  name: openAiName
  sku: openAiSku
  maxConversationTokens: '5000'
  maxCompletionTokens: '2000'
  maxEmbeddingTokens: '8000'
  completionsModel: {
    name: 'gpt-35-turbo'
    version: '0613'
    deployment: {
      name: 'aips-completions'
    }
  }
  embeddingsModel: {
    name: 'text-embedding-ada-002'
    version: '2'
    deployment: {
      name: 'aips-embeddings'
    }
  }
}

resource openAiAccount 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: openAiSettings.name
  location: location
  sku: {
    name: openAiSettings.sku
  }
  kind: 'OpenAI'
  properties: {
    customSubDomainName: openAiSettings.name
    publicNetworkAccess: 'Enabled'
  }
}

resource openAiEmbeddingsModelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAiAccount
  name: openAiSettings.embeddingsModel.deployment.name
  properties: {
    model: {
      format: 'OpenAI'
      name: openAiSettings.embeddingsModel.name
      version: openAiSettings.embeddingsModel.version
    }
  }
}

resource openAiCompletionsModelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAiAccount
  name: openAiSettings.completionsModel.deployment.name
  properties: {
    model: {
      format: 'OpenAI'
      name: openAiSettings.completionsModel.name
      version: openAiSettings.completionsModel.version
    }
  }
  dependsOn: [
    openAiEmbeddingsModelDeployment
  ]
}