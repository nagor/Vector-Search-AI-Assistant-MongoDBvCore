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
param name string = 'aips-tmp'

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
  web: {
    name: '${name}-web'
  }
  api: {
    name: '${name}-api'
  }
}

// win B1 $54, linux B1 $12 per month
// https://azure.microsoft.com/en-us/pricing/details/app-service/windows/
// https://github.com/Azure/azure-quickstart-templates/blob/master/quickstarts/microsoft.web/app-service-docs-linux/main.bicep
// https://learn.microsoft.com/en-us/azure/templates/microsoft.web/2022-03-01/serverfarms?pivots=deployment-language-bicep#bicep-resource-definition
// https://learn.microsoft.com/en-us/azure/app-service/provision-resource-bicep
resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
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

param linuxFxVersion string  =  'DOTNETCORE|7.0' // The runtime stack of web app

resource appServiceWeb 'Microsoft.Web/sites@2022-03-01' = {
  name: appServiceSettings.web.name
  location: location
  kind: 'app'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      linuxFxVersion:  linuxFxVersion
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}

resource appServiceApi 'Microsoft.Web/sites@2022-03-01' = {
  name: appServiceSettings.api.name
  location: location
  kind: 'app'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      alwaysOn: true
      linuxFxVersion:  linuxFxVersion
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}

output deployedUrl string = appServiceWeb.properties.defaultHostName