import {
  to = azurerm_resource_provider_registration.key_vault
  id = "/subscriptions/${var.subscription_id}/providers/Microsoft.KeyVault"
}

import {
  to = azurerm_resource_provider_registration.app_configuration
  id = "/subscriptions/${var.subscription_id}/providers/Microsoft.AppConfiguration"
}

import {
  to = azurerm_resource_provider_registration.container_apps
  id = "/subscriptions/${var.subscription_id}/providers/Microsoft.App"
}

import {
  to = azurerm_resource_provider_registration.operational_insights
  id = "/subscriptions/${var.subscription_id}/providers/Microsoft.OperationalInsights"
}

import {
  to = azurerm_log_analytics_workspace.log_analytic
  id = "/subscriptions/${var.subscription_id}/resourceGroups/RaffaLabRG/providers/Microsoft.OperationalInsights/workspaces/timezone-management-log-analytic"
}
