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

@description('Apps Prefix')
@maxLength(25)
param name string

@description('Specifies the SKU for the Azure App Service plan. Defaults to **B1**')
@allowed([
    'B1'
    'S1'
])
param appServiceSku string = 'B1'

var appServiceSettings = {
    plan: {
        name: '${name}-web-plan'
        sku: appServiceSku
    }
    chat: {
        name: '${name}-chat'
    }
    web: {
        name: '${name}-web'
    }
    api: {
        name: '${name}-api'
    }
}

var openAiSettings = {
    maxConversationTokens: '5000'
    maxCompletionTokens: '2000'
    maxEmbeddingTokens: '8000'
    completionsModel: {
        deployment: {
            name: 'aips-completions'
        }
    }
    embeddingsModel: {
        deployment: {
            name: 'aips-embeddings'
        }
    }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
    name: appServiceSettings.plan.name
    location: location
    sku: {
        name: appServiceSettings.plan.sku
    }
    kind: 'linux'
    properties: {
        reserved: true
    }
}

param linuxDotnetFxVersion string = 'DOTNETCORE|7.0' // The runtime stack of web app
param linuxNodeJsFxVersion string = 'node|18-lts'

resource appServiceWeb 'Microsoft.Web/sites@2022-09-01' = {
    name: appServiceSettings.web.name
    location: location
    kind: 'app'
    properties: {
        serverFarmId: appServicePlan.id
        httpsOnly: true
        siteConfig: {
            alwaysOn: true
            linuxFxVersion: linuxNodeJsFxVersion
        }
    }
    identity: {
        type: 'SystemAssigned'
    }
}

resource appServiceChat 'Microsoft.Web/sites@2022-09-01' = {
    name: appServiceSettings.chat.name
    location: location
    kind: 'app'
    properties: {
        serverFarmId: appServicePlan.id
        httpsOnly: true
        siteConfig: {
            alwaysOn: true
            linuxFxVersion: linuxDotnetFxVersion
        }
    }
    identity: {
        type: 'SystemAssigned'
    }
}

resource appServiceApi 'Microsoft.Web/sites@2022-09-01' = {
    name: appServiceSettings.api.name
    location: location
    kind: 'app'
    properties: {
        serverFarmId: appServicePlan.id
        httpsOnly: true
        siteConfig: {
            alwaysOn: true
            linuxFxVersion: linuxDotnetFxVersion
        }
    }
    identity: {
        type: 'SystemAssigned'
    }
}

resource appServiceApiSettings 'Microsoft.Web/sites/config@2022-09-01' = {
    parent: appServiceApi
    name: 'appsettings'
    kind: 'string'
    properties: {
        APPINSIGHTS_INSTRUMENTATIONKEY: appServiceApiInsights.properties.InstrumentationKey
        OPENAI__ENDPOINT: 'open_ai_endpoint'
        OPENAI__KEY: 'open_ai_key'
        OPENAI__EMBEDDINGSDEPLOYMENT: openAiSettings.embeddingsModel.deployment.name
        OPENAI__COMPLETIONSDEPLOYMENT: openAiSettings.completionsModel.deployment.name
        OPENAI__MAXCONVERSATIONTOKENS: openAiSettings.maxConversationTokens
        OPENAI__MAXCOMPLETIONTOKENS: openAiSettings.maxCompletionTokens
        OPENAI__MAXEMBEDDINGTOKENS: openAiSettings.maxEmbeddingTokens
        MONGODB__CONNECTION: 'mongodb_connection'
        MONGODB__DATABASENAME: 'retaildb'
        MONGODB__COLLECTIONNAMES: 'product,customer,vectors,completions,clothes'
        MONGODB__MAXVECTORSEARCHRESULTS: '20'
        MONGODB__VECTORINDEXTYPE: 'ivf'
        CHATAPI__APIKEY: 'api_key'
    }
}

resource appServiceChatSettings 'Microsoft.Web/sites/config@2022-09-01' = {
    parent: appServiceChat
    name: 'appsettings'
    kind: 'string'
    properties: {
        APPINSIGHTS_INSTRUMENTATIONKEY: appServiceChatInsights.properties.InstrumentationKey
        OPENAI__ENDPOINT: 'open_ai_endpoint'
        OPENAI__KEY: 'open_ai_key'
        OPENAI__EMBEDDINGSDEPLOYMENT: openAiSettings.embeddingsModel.deployment.name
        OPENAI__COMPLETIONSDEPLOYMENT: openAiSettings.completionsModel.deployment.name
        OPENAI__MAXCONVERSATIONTOKENS: openAiSettings.maxConversationTokens
        OPENAI__MAXCOMPLETIONTOKENS: openAiSettings.maxCompletionTokens
        OPENAI__MAXEMBEDDINGTOKENS: openAiSettings.maxEmbeddingTokens
        MONGODB__CONNECTION: ''
        MONGODB__DATABASENAME: 'retaildb'
        MONGODB__COLLECTIONNAMES: 'product,customer,vectors,completions,clothes'
        MONGODB__MAXVECTORSEARCHRESULTS: '20'
        MONGODB__VECTORINDEXTYPE: 'ivf'
    }
}

resource appServiceWebSettings 'Microsoft.Web/sites/config@2022-09-01' = {
    parent: appServiceWeb
    name: 'appsettings'
    kind: 'string'
    properties: {
        APPINSIGHTS_INSTRUMENTATIONKEY: appServiceWebInsights.properties.InstrumentationKey
        CHATAPI__APIURL: 'api_url'
        CHATAPI__APIKEY: 'api_key'
    }
}

resource appServiceWebDeployment 'Microsoft.Web/sites/sourcecontrols@2022-09-01' = {
    parent: appServiceWeb
    name: 'web'
    properties: {
        repoUrl: 'repo'
        branch: 'main'
        isManualIntegration: true
    }
    dependsOn: [
        appServiceWebSettings
    ]
}

resource appServiceApiDeployment 'Microsoft.Web/sites/sourcecontrols@2022-09-01' = {
    parent: appServiceApi
    name: 'web'
    properties: {
        repoUrl: 'repo'
        branch: 'main'
        isManualIntegration: true
    }
    dependsOn: [
        appServiceApiSettings
    ]
}

resource appServiceChatDeployment 'Microsoft.Web/sites/sourcecontrols@2022-09-01' = {
    parent: appServiceChat
    name: 'web'
    properties: {
        repoUrl: 'repo'
        branch: 'main'
        isManualIntegration: true
    }
    dependsOn: [
        appServiceChatSettings
    ]
}

resource appServiceWebInsights 'Microsoft.Insights/components@2020-02-02' = {
    name: appServiceWeb.name
    location: location
    kind: 'web'
    properties: {
        Application_Type: 'web'
    }
}

resource appServiceChatInsights 'Microsoft.Insights/components@2020-02-02' = {
    name: appServiceChat.name
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

output deployedUrl string = appServiceChat.properties.defaultHostName