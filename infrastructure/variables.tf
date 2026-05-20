variable "subscription_id" {
  type        = string
  description = "Azure subscription ID."
  sensitive   = true
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

variable "container_image" {
  type        = string
  description = "Full container image reference to deploy."
  default     = "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
}

variable "db_login" {
  type        = string
  description = "DB Login."
  sensitive   = true
}

variable "db_password" {
  type        = string
  description = "DB Password."
  sensitive   = true
}