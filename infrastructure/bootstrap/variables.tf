variable "subscription_id" {
  type        = string
  description = "Azure subscription ID."
}

variable "location" {
  type        = string
  default     = "eastus2"
  description = "Azure region for all resources."
}

variable "environment" {
  type        = string
  default     = "production"
  description = "Deployment environment: dev, staging, or production."

  validation {
    condition     = contains(["dev", "staging", "production"], var.environment)
    error_message = "environment must be one of: dev, staging, production."
  }
}
