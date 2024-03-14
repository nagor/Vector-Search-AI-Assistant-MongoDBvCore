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

@description('''
Unique name for the deployed services below. Max length 15 characters, alphanumeric only:
- Azure Cosmos DB for MongoDB vCore
- Azure OpenAI Service
- Azure App Service
- Azure Functions

The name defaults to a unique string generated from the resource group identifier.
''')
@maxLength(15)
param name string// = uniqueString(resourceGroup().id)

@description('Specifies the SKU for the Azure App Service plan. Defaults to **B1**')
@allowed([
  'B1'
  'S1'
])
param appServiceSku string = 'B1'

@description('Specifies the SKU for the Azure OpenAI resource. Defaults to **S0**')
@allowed([
  'S0'
])

// TODO: open AI is not requested, what about service plan?
param openAiSku string = 'S0'

@description('MongoDB vCore user Name. No dashes.')
param mongoDbUserName string

@description('MongoDB vCore password. 8-256 characters, 3 of the following: lower case, upper case, numeric, symbol.')
@minLength(8)
@maxLength(256)
@secure()
param mongoDbPassword string


@description('Git repository URL for the application source. This defaults to the [`nagor/Vector-Search-Ai-Assistant`](https://github.com/nagor/Vector-Search-AI-Assistant-MongoDBvCore.git) repository.')
param appGitRepository string = 'https://github.com/nagor/Vector-Search-AI-Assistant-MongoDBvCore.git'

@description('Git repository branch for the application source. This defaults to the [**main** branch of the `nagor/Vector-Search-Ai-Assistant-MongoDBvCore`](https://github.com/nagor/Vector-Search-AI-Assistant-MongoDBvCore/tree/main) repository.')
param appGetRepositoryBranch string = 'newdeployment'

// https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/models#gpt-35-models
// https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/models#embeddings-models
var openAiSettings = {
  name: '${name}-openai'
  sku: openAiSku
  maxConversationTokens: '5000'
  maxCompletionTokens: '2000'
  maxEmbeddingTokens: '8000'
  completionsModel: {
    name: 'gpt-35-turbo'
    version: '0125'
    deployment: {
      name: 'completions'
    }
  }
  embeddingsModel: {
    name: 'text-embedding-3-small'
    deployment: {
      name: 'embeddings'
    }
  }
}

/*
var deployedRegion = {
  'East US': {
    armName: toLower('eastus')
  }
  'South Central US': {
    armName: toLower('southcentralus')
  }
  'West Europe': {
    armName: toLower('westeurope')
  }
}
*/

var mongovCoreSettings = {
  mongoClusterName: '${name}-mongo'
  mongoClusterLogin: mongoDbUserName
  mongoClusterPassword: mongoDbPassword
}

// TODO: win B1 $54, linux B1 $12
// https://azure.microsoft.com/en-us/pricing/details/app-service/windows/
var appServiceSettings = {
  plan: {
    name: '${name}-web-plan'
    sku: appServiceSku
  }
  web: {
    name: '${name}-web'
    git: {
      repo: appGitRepository
      branch: appGetRepositoryBranch
    }
  }
  api: {
    name: '${name}-api'
  }
  function: {
    name: '${name}-function'
    git: {
      repo: appGitRepository
      branch: appGetRepositoryBranch
    }
  }
}

resource mongoCluster 'Microsoft.DocumentDB/mongoClusters@2023-03-01-preview' = {
  name: mongovCoreSettings.mongoClusterName
  location: location
  properties: {
    administratorLogin: mongovCoreSettings.mongoClusterLogin
    administratorLoginPassword: mongovCoreSettings.mongoClusterPassword
    serverVersion: '5.0'
    nodeGroupSpecs: [
      {
        kind: 'Shard'
        sku: 'M30'
        diskSizeGB: 128
        enableHa: false
        nodeCount: 1
      }
    ]
  }
}

resource mongoFirewallRulesAllowAzure 'Microsoft.DocumentDB/mongoClusters/firewallRules@2023-03-01-preview' = {
  parent: mongoCluster
  name: 'allowAzure'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource mongoFirewallRulesAllowAll 'Microsoft.DocumentDB/mongoClusters/firewallRules@2023-03-01-preview' = {
  parent: mongoCluster
  name: 'allowAll'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '255.255.255.255'
  }
}

/* TODO: try w/o open AI
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
      //version: openAiSettings.embeddingsModel.version
    }
    scaleSettings: {
      scaleType: 'Standard'
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
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
    scaleSettings: {
      scaleType: 'Standard'
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
  }
  dependsOn: [
    openAiEmbeddingsModelDeployment
  ]
}
 */

// https://github.com/Azure/azure-quickstart-templates/blob/master/quickstarts/microsoft.web/app-service-docs-linux/main.bicep
// https://learn.microsoft.com/en-us/azure/templates/microsoft.web/2022-03-01/serverfarms?pivots=deployment-language-bicep#bicep-resource-definition
resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: appServiceSettings.plan.name
  location: location
  sku: {
    name: appServiceSettings.plan.sku
  }
  // default is windows
  //kind: 'linux'
  //properties: {
  //   reserved: true
  //}
}

//param linuxFxVersion string  =  'DOTNET|7.0' // The runtime stack of web app

resource appServiceWeb 'Microsoft.Web/sites@2022-03-01' = {
  name: appServiceSettings.web.name
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      //linuxFxVersion:  linuxFxVersion
    }
  }
}

resource appServiceApi 'Microsoft.Web/sites@2022-03-01' = {
  name: appServiceSettings.api.name
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      //linuxFxVersion:  linuxFxVersion
    }
  }
}

/* No need storage, used by functions
resource storageAccount 'Microsoft.Storage/storageAccounts@2021-09-01' = {
  name: '${name}fnstorage'
  location: location
  kind: 'Storage'
  sku: {
    name: 'Standard_LRS'
  }
}
*/

/* No need functions
resource appServiceFunction 'Microsoft.Web/sites@2022-03-01' = {
  name: appServiceSettings.function.name
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
    }
  }
  dependsOn: [
    storageAccount
  ]
}
*/

resource appServiceWebSettings 'Microsoft.Web/sites/config@2022-03-01' = {
  parent: appServiceWeb
  name: 'appsettings'
  kind: 'string'
  properties: {
    APPINSIGHTS_INSTRUMENTATIONKEY: appServiceWebInsights.properties.InstrumentationKey
    // TODO: open AI endpoint
    //OPENAI__ENDPOINT: openAiAccount.properties.endpoint
    //OPENAI__KEY: openAiAccount.listKeys().key1
    //OPENAI__EMBEDDINGSDEPLOYMENT: openAiEmbeddingsModelDeployment.name
    //OPENAI__COMPLETIONSDEPLOYMENT: openAiCompletionsModelDeployment.name
    OPENAI__MAXCONVERSATIONTOKENS: openAiSettings.maxConversationTokens
    OPENAI__MAXCOMPLETIONTOKENS: openAiSettings.maxCompletionTokens
    OPENAI__MAXEMBEDDINGTOKENS: openAiSettings.maxEmbeddingTokens
    MONGODB__CONNECTION: 'mongodb+srv://${mongovCoreSettings.mongoClusterLogin}:${mongovCoreSettings.mongoClusterPassword}@${mongovCoreSettings.mongoClusterName}.mongocluster.cosmos.azure.com/?tls=true&authMechanism=SCRAM-SHA-256&retrywrites=false&maxIdleTimeMS=120000'
    MONGODB__DATABASENAME: 'retaildb'
    MONGODB__COLLECTIONNAMES: 'product,customer,vectors,completions,clothes'
    MONGODB__MAXVECTORSEARCHRESULTS: '20'
    MONGODB__VECTORINDEXTYPE: 'ivf'
  }
}

resource appServiceApiSettings 'Microsoft.Web/sites/config@2022-03-01' = {
  parent: appServiceApi
  name: 'appsettings'
  kind: 'string'
  properties: {
    APPINSIGHTS_INSTRUMENTATIONKEY: appServiceApiInsights.properties.InstrumentationKey
    // TODO: open AI endpoint
    //OPENAI__ENDPOINT: openAiAccount.properties.endpoint
    //OPENAI__KEY: openAiAccount.listKeys().key1
    //OPENAI__EMBEDDINGSDEPLOYMENT: openAiEmbeddingsModelDeployment.name
    //OPENAI__COMPLETIONSDEPLOYMENT: openAiCompletionsModelDeployment.name
    OPENAI__MAXCONVERSATIONTOKENS: openAiSettings.maxConversationTokens
    OPENAI__MAXCOMPLETIONTOKENS: openAiSettings.maxCompletionTokens
    OPENAI__MAXEMBEDDINGTOKENS: openAiSettings.maxEmbeddingTokens
    MONGODB__CONNECTION: 'mongodb+srv://${mongovCoreSettings.mongoClusterLogin}:${mongovCoreSettings.mongoClusterPassword}@${mongovCoreSettings.mongoClusterName}.mongocluster.cosmos.azure.com/?tls=true&authMechanism=SCRAM-SHA-256&retrywrites=false&maxIdleTimeMS=120000'
    MONGODB__DATABASENAME: 'retaildb'
    MONGODB__COLLECTIONNAMES: 'product,customer,vectors,completions,clothes'
    MONGODB__MAXVECTORSEARCHRESULTS: '20'
    MONGODB__VECTORINDEXTYPE: 'ivf'
  }
}

/* Function AppSettings, no need
resource appServiceFunctionSettings 'Microsoft.Web/sites/config@2022-03-01' = {
  parent: appServiceFunction
  name: 'appsettings'
  kind: 'string'
  properties: {
    AzureWebJobsStorage: 'DefaultEndpointsProtocol=https;AccountName=${name}fnstorage;EndpointSuffix=core.windows.net;AccountKey=${storageAccount.listKeys().keys[0].value}'
    APPLICATIONINSIGHTS_CONNECTION_STRING: appServiceFunctionsInsights.properties.ConnectionString
    FUNCTIONS_EXTENSION_VERSION: '~4'
    FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
    OPENAI__ENDPOINT: openAiAccount.properties.endpoint
    OPENAI__KEY: openAiAccount.listKeys().key1
    OPENAI__EMBEDDINGSDEPLOYMENT: openAiEmbeddingsModelDeployment.name
    OPENAI__COMPLETIONSDEPLOYMENT: openAiCompletionsModelDeployment.name
    OPENAI__MAXCONVERSATIONTOKENS: openAiSettings.maxConversationTokens
    OPENAI__MAXCOMPLETIONTOKENS: openAiSettings.maxCompletionTokens
    OPENAI__MAXEMBEDDINGTOKENS: openAiSettings.maxEmbeddingTokens
    MONGODB__CONNECTION: 'mongodb+srv://${mongovCoreSettings.mongoClusterLogin}:${mongovCoreSettings.mongoClusterPassword}@${mongovCoreSettings.mongoClusterName}.mongocluster.cosmos.azure.com/?tls=true&authMechanism=SCRAM-SHA-256&retrywrites=false&maxIdleTimeMS=120000'
    MONGODB__DATABASENAME: 'retaildb'
    MONGODB__COLLECTIONNAMES: 'product,customer,vectors,completions,clothes'
    MONGODB__MAXVECTORSEARCHRESULTS: '20'
    MONGODB__VECTORINDEXTYPE: 'ivf'
  }
}
*/

resource appServiceWebDeployment 'Microsoft.Web/sites/sourcecontrols@2021-03-01' = {
  parent: appServiceWeb
  name: 'web'
  properties: {
    repoUrl: appServiceSettings.web.git.repo
    branch: appServiceSettings.web.git.branch
    isManualIntegration: true
  }
  dependsOn: [
    appServiceWebSettings
  ]
}

// TODO: API deployment, manual for now

/* Functions deployment, no need
resource appServiceFunctionsDeployment 'Microsoft.Web/sites/sourcecontrols@2021-03-01' = {
  parent: appServiceFunction
  name: 'web'
  properties: {
    repoUrl: appServiceSettings.web.git.repo
    branch: appServiceSettings.web.git.branch
    isManualIntegration: true
  }
  dependsOn: [
    appServiceFunctionSettings
  ]
}
*/

/* No need function Insights
resource appServiceFunctionsInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appServiceFunction.name
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}
*/

resource appServiceWebInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appServiceWeb.name
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

resource appServiceApiInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appServiceApi.name
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

output deployedUrl string = appServiceWeb.properties.defaultHostName