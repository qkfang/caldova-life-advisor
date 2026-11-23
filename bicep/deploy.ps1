
az group create --name 'rg-life-advice-guru' --location 'eastus2'

az deployment group create --name 'life-advice-guru-deploy' --resource-group 'rg-life-advice-guru' --template-file './main.bicep' --parameters './main.bicepparam'
