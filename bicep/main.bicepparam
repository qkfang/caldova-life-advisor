using 'main.bicep'

param baseName = 'caldovacl'
param location = 'eastus2'
param appServiceLocation = 'westus3'
param principals = [
  {
    id: '4b74544b-02c6-4e4f-b936-732c9c3fff65'
    principalType: 'User'
  }
]
