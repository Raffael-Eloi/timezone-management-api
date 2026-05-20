locals {
  tags = {
    environment = var.environment
    project     = "timezone-management"
    owner       = "raffael-eloi"
    managed_by  = "terraform"
  }

  db_connection_string = "Host=${azurerm_postgresql_flexible_server.timezonemanagementserver.fqdn};Database=timezonemanagementdb;Username=${var.db_login};Password='${var.db_password}';SslMode=Require;"
}
terraform {
  required_version = ">= 1.5.2"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.73.0",
    }
  }

  backend "azurerm" {
    resource_group_name  = "RaffaLabRG"
    storage_account_name = "raffalabstorageaccount"
    container_name       = "raffalab-tfstate"
    key                  = "timezonemanagementapi.tfstate"
  }

}

provider "azurerm" {
  resource_provider_registrations = "all"
  subscription_id                 = var.subscription_id
  features {}
}

data "azurerm_resource_group" "raffa_lab_rg" {
  name = "RaffaLabRG"
}


resource "azurerm_app_configuration" "appconf" {
  name                = "timezone-management-app-config"
  resource_group_name = data.azurerm_resource_group.raffa_lab_rg.name
  location            = data.azurerm_resource_group.raffa_lab_rg.location
  tags                = local.tags

  identity {
    type = "SystemAssigned"
  }
}

resource "azurerm_key_vault_access_policy" "app_configuration" {
  key_vault_id = azurerm_key_vault.app_config_key_vault.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = azurerm_app_configuration.appconf.identity[0].principal_id

  secret_permissions = [
    "Get",
    "List",
  ]
}

data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "app_config_key_vault" {
  name                       = "tz-management-kv"
  location                   = data.azurerm_resource_group.raffa_lab_rg.location
  resource_group_name        = data.azurerm_resource_group.raffa_lab_rg.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 7
  purge_protection_enabled   = true
  tags                       = local.tags

  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_key_vault_secret" "db_connection_string" {
  name         = "dbconnectionstring"
  key_vault_id = azurerm_key_vault.app_config_key_vault.id
  value        = local.db_connection_string

  depends_on = [azurerm_key_vault_access_policy.terraform_sp]
}

resource "azurerm_app_configuration_key" "db_connection_string" {
  configuration_store_id = azurerm_app_configuration.appconf.id
  key                    = "ConnectionStrings:Postgres"
  type                   = "kv"
  value                  = local.db_connection_string

  depends_on = [
    azurerm_key_vault_access_policy.app_configuration,
    azurerm_role_assignment.terraform_sp_appconfig_owner,
  ]

  lifecycle {
    ignore_changes = [configuration_store_id]
  }
}

resource "azurerm_key_vault_access_policy" "terraform_sp" {
  key_vault_id = azurerm_key_vault.app_config_key_vault.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = data.azurerm_client_config.current.object_id

  secret_permissions = [
    "Get",
    "Set",
    "Delete",
    "List",
    "Recover",
  ]
}

resource "azurerm_log_analytics_workspace" "log_analytic" {
  name                = "timezone-management-log-analytic"
  location            = data.azurerm_resource_group.raffa_lab_rg.location
  resource_group_name = data.azurerm_resource_group.raffa_lab_rg.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

resource "azurerm_container_app_environment" "container_app_environment" {
  name                       = "production"
  location                   = data.azurerm_resource_group.raffa_lab_rg.location
  resource_group_name        = data.azurerm_resource_group.raffa_lab_rg.name
  tags                       = local.tags
  log_analytics_workspace_id = azurerm_log_analytics_workspace.log_analytic.id
}

resource "azurerm_container_app" "container_app" {
  name                         = "timezone-management-api"
  container_app_environment_id = azurerm_container_app_environment.container_app_environment.id
  resource_group_name          = data.azurerm_resource_group.raffa_lab_rg.name
  revision_mode                = "Single"
  tags                         = local.tags

  lifecycle {
    prevent_destroy = true
    ignore_changes  = [template]
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  identity {
    type = "SystemAssigned"
  }

  template {
    container {
      name   = "timezone-management-api"
      image  = var.container_image
      cpu    = 0.25
      memory = "0.5Gi"

      startup_probe {
        transport               = "TCP"
        port                    = 8080
        initial_delay           = 10
        interval_seconds        = 5
        failure_count_threshold = 3
      }

      env {
        name  = "AZURE_APPCONFIGURATION_ENDPOINT"
        value = azurerm_app_configuration.appconf.endpoint
      }
    }
  }
}

resource "azurerm_key_vault_access_policy" "container_app" {
  key_vault_id = azurerm_key_vault.app_config_key_vault.id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = azurerm_container_app.container_app.identity[0].principal_id

  secret_permissions = [
    "Get",
    "List",
  ]
}

resource "azurerm_role_assignment" "container_app_appconfig_reader" {
  scope                = azurerm_app_configuration.appconf.id
  role_definition_name = "App Configuration Data Reader"
  principal_id         = azurerm_container_app.container_app.identity[0].principal_id
}

resource "azurerm_role_assignment" "terraform_sp_appconfig_owner" {
  scope                = azurerm_app_configuration.appconf.id
  role_definition_name = "App Configuration Data Owner"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "azurerm_postgresql_flexible_server" "timezonemanagementserver" {
  name                   = "timezonemanagementserver"
  resource_group_name    = data.azurerm_resource_group.raffa_lab_rg.name
  location               = data.azurerm_resource_group.raffa_lab_rg.location
  version                = "16"
  administrator_login    = var.db_login
  administrator_password = var.db_password
  storage_mb             = 32768
  sku_name               = "B_Standard_B1ms"
  tags                   = local.tags
  # Required to prevent zone drift on subsequent applies.
  zone = "1"

  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_postgresql_flexible_server_database" "timezonemanagementdb" {
  name      = "timezonemanagementdb"
  server_id = azurerm_postgresql_flexible_server.timezonemanagementserver.id
  collation = "en_US.utf8"
  charset   = "UTF8"

  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "allow_azure_services" {
  name             = "allow-azure-services"
  server_id        = azurerm_postgresql_flexible_server.timezonemanagementserver.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}