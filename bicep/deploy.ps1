
az group create --name 'rg-caldova-life' --location 'westus'

az deployment group create --name 'life-advice-guru-deploy' --resource-group 'rg-caldova-life' --template-file './main.bicep' --parameters './main.bicepparam'
