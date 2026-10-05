// =============================================================================
// PawConnect – Azure infrastructure as code (design doc sections 6.3, 7.3, 8.3)
//
// Creates: App Service plan + web app + a staging environment (a "staging" slot for
// blue-green deployments on S1/P0v3, or a second web app on the free F1 / cheap B1 plans), Azure Database for PostgreSQL Flexible Server (Burstable B1ms,
// 7-day backups), Key Vault for secrets (read by the web app's managed identity),
// Application Insights + Log Analytics, and the two alert rules from the design doc.
//
// Deploy (see docs/DEPLOYMENT.md):
//   az deployment group create -g rg-pawconnect -f infra/main.bicep \
//     -p appName=pawconnect-<yourname> postgresAdminPassword='<strong password>' \
//        adminEmail=<you@example.com> adminPassword='<strong password>' alertEmail=<you@example.com>
// =============================================================================

@description('Globally unique base name, e.g. pawconnect-st10285120. Used for the web app URL.')
@minLength(3)
@maxLength(40)
param appName string

@description('Azure region. South Africa North keeps data in-country (POPIA) and latency low.')
param location string = resourceGroup().location

@description('App Service plan SKU. S1/P0v3: deployment slots (blue-green swaps). B1: cheap, no slots. F1: free (60 CPU min/day, sleeps when idle, no slots).')
@allowed(['F1', 'B1', 'S1', 'P0v3'])
param appServiceSku string = 'S1'

@description('PostgreSQL administrator login name.')
param postgresAdminLogin string = 'pawadmin'

@description('PostgreSQL administrator password (stored only in Key Vault).')
@secure()
param postgresAdminPassword string

@description('Email for the first PawConnect administrator account (created on first start).')
param adminEmail string

@description('Password for the first PawConnect administrator account. Change it after first sign-in.')
@secure()
param adminPassword string

@description('Password for the demo accounts on STAGING only (published in the README for markers). Never used in production.')
@secure()
param demoPassword string

@description('Where alert emails go (the shelter\'s technical volunteer).')
param alertEmail string

var useSlots = appServiceSku == 'S1' || appServiceSku == 'P0v3'
var isFreeTier = appServiceSku == 'F1'
var stagingAppName = '${appName}-staging'
var suffix = uniqueString(resourceGroup().id)
var postgresServerName = '${appName}-pg-${take(suffix, 5)}'
var keyVaultName = take('kv-${replace(appName, '-', '')}${take(suffix, 6)}', 24)
var databaseName = 'pawconnect'
var stagingDatabaseName = 'pawconnect_staging'

// ---------- Monitoring ----------
resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${appName}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${appName}-insights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
  }
}

// ---------- Database ----------
resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2023-06-01-preview' = {
  name: postgresServerName
  location: location
  sku: { name: 'Standard_B1ms', tier: 'Burstable' }
  properties: {
    version: '16'
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    storage: { storageSizeGB: 32 }
    backup: { backupRetentionDays: 7, geoRedundantBackup: 'Disabled' } // NFR: daily backups, 7-day point-in-time restore
    highAvailability: { mode: 'Disabled' }
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01-preview' = {
  parent: postgres
  name: databaseName
}

resource stagingDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-06-01-preview' = {
  parent: postgres
  name: stagingDatabaseName
}

// Allows Azure services (the App Service) to reach the server. TLS is enforced by default.
// Rationale: a private VNet + private DNS zone would close the server to other Azure tenants too, but
// needs a paid plan and roughly doubles the monthly cost, so for the shelter's budget we rely on
// TLS + a strong Key Vault-held password + no public credentials (documented in docs/DEPLOYMENT.md).
resource allowAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-06-01-preview' = {
  parent: postgres
  name: 'AllowAzureServices'
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
}

// ---------- Secrets ----------
resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

var connectionTemplate = 'Host=${postgres.properties.fullyQualifiedDomainName};Port=5432;Username=${postgresAdminLogin};Password=${postgresAdminPassword};SSL Mode=Require;Trust Server Certificate=false;Database='

resource prodConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'ConnectionStrings--PawConnect'
  properties: { value: '${connectionTemplate}${databaseName}' }
}

resource stagingConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'ConnectionStrings--PawConnect-Staging'
  properties: { value: '${connectionTemplate}${stagingDatabaseName}' }
}

resource adminPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'Seed--AdminPassword'
  properties: { value: adminPassword }
}

// Separate from the admin password so sharing staging logins never exposes production.
resource demoPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'Seed--DemoPassword'
  properties: { value: demoPassword }
}

// ---------- Web app ----------
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${appName}-plan'
  location: location
  kind: 'linux'
  sku: { name: appServiceSku }
  properties: { reserved: true }
}

var commonSiteConfig = {
  linuxFxVersion: 'DOTNETCORE|8.0'
  alwaysOn: !isFreeTier // F1 does not support Always On (the app sleeps after ~20 min idle)
  ftpsState: 'Disabled'
  minTlsVersion: '1.2'
  http20Enabled: true
  healthCheckPath: '/health'
}

var commonSettings = [
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.ConnectionString }
  { name: 'ApplicationInsightsAgent_EXTENSION_VERSION', value: '~3' }
  { name: 'Database__MigrateOnStartup', value: 'true' }
]

// Production only: the first real administrator account.
var productionSettings = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
  { name: 'Seed__DemoData', value: 'false' }
  { name: 'Seed__AdminEmail', value: adminEmail }
  { name: 'Seed__AdminPassword', value: '@Microsoft.KeyVault(VaultName=${vault.name};SecretName=${adminPasswordSecret.name})' }
]

// Staging only: demo data with its own, shareable demo password.
var stagingSettings = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Staging' }
  { name: 'Seed__DemoData', value: 'true' }
  { name: 'Seed__DemoPassword', value: '@Microsoft.KeyVault(VaultName=${vault.name};SecretName=${demoPasswordSecret.name})' }
]

var stagingConnectionStrings = [
  {
    name: 'PawConnect'
    type: 'Custom'
    connectionString: '@Microsoft.KeyVault(VaultName=${vault.name};SecretName=${stagingConnectionSecret.name})'
  }
]

resource site 'Microsoft.Web/sites@2023-12-01' = {
  name: appName
  location: location
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true // HTTP is redirected to HTTPS
    siteConfig: union(commonSiteConfig, {
      appSettings: union(commonSettings, productionSettings)
      connectionStrings: [
        {
          name: 'PawConnect'
          type: 'Custom'
          connectionString: '@Microsoft.KeyVault(VaultName=${vault.name};SecretName=${prodConnectionSecret.name})'
        }
      ]
    })
  }
}

// Settings that must stay with a slot when it is swapped, so production never gets staging's
// database, demo data or demo password, and staging never gets the production admin password.
resource stickySettings 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: site
  name: 'slotConfigNames'
  properties: {
    appSettingNames: ['ASPNETCORE_ENVIRONMENT', 'Seed__DemoData', 'Seed__DemoPassword', 'Seed__AdminEmail', 'Seed__AdminPassword']
    connectionStringNames: ['PawConnect']
  }
}

resource stagingSlot 'Microsoft.Web/sites/slots@2023-12-01' = if (useSlots) {
  parent: site
  name: 'staging'
  location: location
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: union(commonSiteConfig, {
      appSettings: union(commonSettings, stagingSettings)
      connectionStrings: stagingConnectionStrings
    })
  }
}

// F1 / B1 plans have no slots, so staging is a second web app on the same plan (no extra cost).
resource stagingSite 'Microsoft.Web/sites@2023-12-01' = if (!useSlots) {
  name: stagingAppName
  location: location
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: union(commonSiteConfig, {
      appSettings: union(commonSettings, stagingSettings)
      connectionStrings: stagingConnectionStrings
    })
  }
}

// Managed identities may read secrets; nothing else can (no credentials in source control).
var keyVaultSecretsUser = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')

resource siteCanReadSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, site.id, 'kv-secrets-user')
  scope: vault
  properties: {
    roleDefinitionId: keyVaultSecretsUser
    principalId: site.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource slotCanReadSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (useSlots) {
  name: guid(vault.id, '${site.id}/staging', 'kv-secrets-user')
  scope: vault
  properties: {
    roleDefinitionId: keyVaultSecretsUser
    principalId: useSlots ? stagingSlot!.identity.principalId : ''
    principalType: 'ServicePrincipal'
  }
}

resource stagingSiteCanReadSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!useSlots) {
  name: guid(vault.id, stagingAppName, 'kv-secrets-user')
  scope: vault
  properties: {
    roleDefinitionId: keyVaultSecretsUser
    principalId: !useSlots ? stagingSite!.identity.principalId : ''
    principalType: 'ServicePrincipal'
  }
}

// ---------- Alerts (design doc 8.3) ----------
resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: '${appName}-alerts'
  location: 'global'
  properties: {
    groupShortName: 'pawalerts'
    enabled: true
    emailReceivers: [{ name: 'Technical volunteer', emailAddress: alertEmail, useCommonAlertSchema: true }]
  }
}

resource errorRateAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${appName}-http-5xx'
  location: 'global'
  properties: {
    description: 'Server errors detected over 5 minutes (error-rate alert).'
    severity: 2
    enabled: true
    scopes: [site.id]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        { name: 'Http5xx', criterionType: 'StaticThresholdCriterion', metricName: 'Http5xx', operator: 'GreaterThan', threshold: 5, timeAggregation: 'Total' }
      ]
    }
    actions: [{ actionGroupId: actionGroup.id }]
  }
}

resource latencyAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${appName}-slow-responses'
  location: 'global'
  properties: {
    description: 'Average response time above 1 second over 5 minutes.'
    severity: 3
    enabled: true
    scopes: [site.id]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        { name: 'ResponseTime', criterionType: 'StaticThresholdCriterion', metricName: 'HttpResponseTime', operator: 'GreaterThan', threshold: 1, timeAggregation: 'Average' }
      ]
    }
    actions: [{ actionGroupId: actionGroup.id }]
  }
}

output webAppUrl string = 'https://${site.properties.defaultHostName}'
output stagingUrl string = 'https://${appName}-staging.azurewebsites.net' // the slot and the separate app share this URL pattern
output stagingWebAppName string = useSlots ? '' : stagingAppName
output keyVaultName string = vault.name
output postgresServer string = postgres.properties.fullyQualifiedDomainName
