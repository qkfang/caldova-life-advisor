
az group create --name 'rg-caldova-life' --location 'eastus2'

az deployment group create --name 'caldova-life-deploy' --resource-group 'rg-caldova-life' --template-file './main.bicep' --parameters './main.bicepparam'
