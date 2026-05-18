locals {
  tags = {
    environment = var.environment
    project     = "portfolio"
    owner       = "raffael-eloi"
    managed_by  = "terraform"
  }
}
terraform {
  required_version = ">= 1.5.2"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "=4.1.0",
    }
  }
}

provider "azurerm" {
  resource_provider_registrations = "none"
  subscription_id                 = var.subscription_id
  features {}
}

resource "azurerm_resource_group" "raffa_lab_rg" {
  name     = "RaffaLabRG"
  location = var.location
  tags     = local.tags
}

resource "azurerm_management_lock" "rg_lock" {
  name       = "rg-cannot-delete"
  scope      = azurerm_resource_group.raffa_lab_rg.id
  lock_level = "CanNotDelete"
  notes      = "Prevents accidental deletion of the timezone management resource group."
}

resource "azurerm_resource_provider_registration" "storage" {
  name = "Microsoft.Storage"
}

resource "azurerm_storage_account" "storage_account" {
  name                       = "raffalabstorageaccount"
  resource_group_name        = azurerm_resource_group.raffa_lab_rg.name
  location                   = azurerm_resource_group.raffa_lab_rg.location
  account_tier               = "Standard"
  account_replication_type   = "LRS"
  tags                       = local.tags
  min_tls_version            = "TLS1_2"
  https_traffic_only_enabled = true

  blob_properties {
    versioning_enabled = true
    delete_retention_policy {
      days = 7
    }
  }
}

resource "azurerm_management_lock" "sa_lock" {
  name       = "sa-cannot-delete"
  scope      = azurerm_storage_account.storage_account.id
  lock_level = "CanNotDelete"
  notes      = "Prevents accidental deletion of the timezone management resource group."
}

resource "azurerm_storage_container" "storage_container" {
  name                  = "raffalab-tfstate"
  storage_account_name  = azurerm_storage_account.storage_account.name
  container_access_type = "private"
}
