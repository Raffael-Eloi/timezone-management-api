
### Summary

The infrastructure has a solid foundation: remote Terraform state in Azure Blob Storage, management locks on critical resources, TLS enforcement, OIDC-based pipeline authentication, `sensitive = true` on secret variables, and consistent cost-allocation tags. However, the CI/CD pipeline contains **five critical bugs that prevent it from working correctly**: a non-existent Terraform version (`1.15.2`), a wrong job output reference that causes all downstream jobs to receive an empty image tag, an invalid cross-job `needs` dependency in the `deploy` job, a major-version mismatch between upload/download artifact actions, and a missing required Terraform variable in the plan command. On the infrastructure side, the Container App has no Managed Identity configured (blocking secure runtime access to App Configuration and Key Vault), and the Key Vault relies on the legacy access-policy model with overly broad permissions granted to the CI/CD service principal.

---

### Findings

| # | Severity | Category | Resource | Finding | Recommendation |
|---|----------|----------|----------|---------|----------------|
| 1 | Critical | CI/CD Pipeline | `pushImageToDockerHub` job outputs | `image: ${{ steps.datetime.outputs.full }}` references a non-existent output. The `datetime` step only sets `now`; `full` is produced by the `image_tag` step. All downstream jobs (`terraformPlan`, `terraformApply`, `deploy`) receive an empty image tag, causing silent bad deploys. | Change to `image: ${{ steps.image_tag.outputs.full }}` |
| 2 | Critical | CI/CD Pipeline | `deploy` job | Consumes `${{ needs.pushImageToDockerHub.outputs.image }}` but `pushImageToDockerHub` is not listed in `deploy.needs` (only `terraformApply` is). GitHub Actions returns empty string for undeclared `needs` references — the Container Apps deploy action receives a blank image name. | Change to `needs: [terraformApply, pushImageToDockerHub]` |
| 3 | Critical | CI/CD Pipeline | `terraformPlan` / `terraformApply` jobs | `terraform_version: "1.15.2"` does not exist. The Terraform 1.x stable line currently ends at `1.12.x`; `1.15.2` is not a published release. `hashicorp/setup-terraform` will fail to download this version, blocking every deployment. | Use a real version that satisfies `required_version = ">= 1.5.2"`, e.g. `terraform_version: "1.12.2"` |
| 4 | Critical | CI/CD Pipeline | `upload-artifact@v7` / `download-artifact@v8` | Major-version mismatch between upload (`v7`) and download (`v8`). These versions do not exist — the latest published pair is `v4` for both. Even if they existed, different major versions use incompatible artifact protocols; the tfplan artifact would fail to download, breaking `terraform apply`. | Align both to `actions/upload-artifact@v4` and `actions/download-artifact@v4` |
| 5 | Critical | CI/CD Pipeline | `terraformPlan` job | `terraform plan` passes only `subscription_id` and `container_image` vars. `db_connection_string_value` is a required variable with no default and `sensitive = true`; Terraform will error out non-interactively in CI without it. | Add `env: TF_VAR_db_connection_string_value: ${{ secrets.DB_CONNECTION_STRING }}` to the plan step and store the secret in the repository/environment |
| 6 | High | Security | `azurerm_container_app` | No `identity` block is configured. The app reads the database connection string from Azure App Configuration (backed by Key Vault) but has no Managed Identity — requiring out-of-band credential management at runtime. | Add `identity { type = "SystemAssigned" }` to the container app resource and grant the resulting MSI the `App Configuration Data Reader` role on the App Configuration store |
| 7 | High | Security | `azurerm_key_vault` | Uses the legacy `azurerm_key_vault_access_policy` resource model. The access policy grants the CI/CD service principal `Get`, `Set`, `Delete`, `List`, `Recover`, and `Purge` — significantly broader than the minimum required for deploying a secret. | Enable `enable_rbac_authorization = true` on the Key Vault and replace the access policy resource with `azurerm_role_assignment` using `Key Vault Secrets Officer` for the CI/CD SP and `Key Vault Secrets User` for the Container App MSI |
| 8 | High | CI/CD Pipeline | All `uses:` references | Every action is pinned to a floating major-version tag (`@v3`, `@v4`, `@v5`, `@v6`, `@v7`). Floating tags can be silently force-pushed to point at any commit — a known supply-chain attack vector. GitHub's own OIDC security hardening guide pins actions to commit SHAs. | Pin all third-party actions to commit SHAs (e.g. `actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4`). Enable Dependabot for `github-actions` to automate SHA updates. |
| 9 | High | Observability | `azurerm_container_app`, `azurerm_key_vault`, `azurerm_app_configuration` | The Log Analytics workspace and all `azurerm_monitor_diagnostic_setting` resources are commented out. No logs or metrics are being collected — secret access, configuration reads, and application errors are completely invisible. | Uncomment `azurerm_log_analytics_workspace`, wire its ID into `azurerm_container_app_environment`, and add `azurerm_monitor_diagnostic_setting` to the Key Vault (`AuditEvent`) and App Configuration (`HttpRequest`) |
| 10 | Medium | CI/CD Pipeline | `build` and `tests` jobs | Neither job uses `actions/cache` for NuGet packages. Both jobs independently trigger `dotnet restore`, downloading all packages from scratch on every run. | Add `actions/cache@v4` before `dotnet restore` in each job, keyed on a hash of `**/*.csproj` files |
| 11 | Medium | Terraform | `azurerm_key_vault`, `azurerm_app_configuration`, `azurerm_container_app_environment` | No `lifecycle { prevent_destroy = true }` on production resources. A mistaken plan or `terraform destroy` could delete the Key Vault despite soft-delete — purge still requires manual intervention. | Add `lifecycle { prevent_destroy = true }` to `azurerm_key_vault`, `azurerm_app_configuration`, and `azurerm_container_app_environment` |
| 12 | Medium | CI/CD Pipeline | `build` and `tests` jobs | The `build` job compiles the solution but uploads no artifact. The `tests` job re-checks out fresh code and re-compiles from scratch, duplicating the compile step on every run. | Upload compiled output from `build` as an artifact and download it in `tests`, or merge both into a single job |
| 13 | Medium | Security | `azurerm_container_app` | No `ingress` block is defined. Without explicit ingress configuration, external reachability and allowed origins are implicit and cannot be audited from the Terraform config. | Add an `ingress` block specifying `external_enabled`, `target_port`, `allow_insecure_connections = false`, and a `traffic_weight` block |
| 14 | Medium | Terraform | `azurerm_container_app_environment` | `name = "production"` is a hardcoded string literal rather than `var.environment`. Running apply with a different environment would create a second environment still named `"production"`. | Change to `name = var.environment` or `"timezone-${var.environment}"` |
| 15 | Low | Terraform | `infrastructure/bootstrap/required_main.tf` | The `project` tag is hardcoded as `"portfolio"` instead of `"timezone-management"`. Cost allocation reports will misattribute bootstrap resources to a different project. | Change `project = "portfolio"` to `project = "timezone-management"` |
| 16 | Low | Security | `azurerm_key_vault`, `azurerm_app_configuration` | No `azurerm_management_lock` on either resource. The resource-group-level lock prevents group deletion but not individual-resource deletion within the group. | Add `CanNotDelete` locks on Key Vault and App Configuration, matching the pattern used in the bootstrap module |
| 17 | Low | Terraform | `azurerm_key_vault` | Name `"appconfigkeyvault"` is globally unique in Azure and hardcoded. A second environment deployment would collide or silently fail. | Include the environment: `"timezone-${var.environment}-kv"` |
| 18 | Low | Observability | All resources | No `azurerm_monitor_metric_alert` or `azurerm_monitor_action_group` defined. There is no alerting on Container App HTTP error rates or Key Vault availability. | Add at minimum a metric alert on Container App HTTP 5xx responses |
| 19 | Low | CI/CD Pipeline | Top-level `permissions:` block | `actions: read` is declared at workflow level but no step in any job reads workflow artifacts via the Actions API. | Remove `actions: read` — prefer the narrowest permission set |

---

### Positive Observations

- **Remote Terraform state** is correctly configured against a private Azure Blob Storage container — no local state files.
- **Management locks** (`CanNotDelete`) are applied to both the Resource Group and the Terraform Storage Account in the bootstrap module, preventing accidental deletion of shared infrastructure.
- **Storage Account** is hardened: `min_tls_version = "TLS1_2"`, `https_traffic_only_enabled = true`, blob versioning enabled, and 7-day soft-delete — strong baseline for state storage.
- **`db_connection_string_value` is `sensitive = true`** — the connection string never appears in `terraform plan` output or CI logs.
- **`environment` variable has a `validation` block** in both modules — invalid values are caught at plan time.
- **Key Vault has `purge_protection_enabled = true`** — prevents permanent secret destruction during the soft-delete retention window.
- **OIDC-based Azure login** in the pipeline — no long-lived service principal client secrets are stored in GitHub.
- **`environment: production`** is declared on both `terraformApply` and `deploy` — approval gates and branch protection rules can be enforced through GitHub Environments.
- **Cost-allocation tags** (`environment`, `project`, `owner`, `managed_by`) are defined in a single `locals` block and applied consistently across all tagged resources.
- **Secrets flow correctly**: DB connection string is stored in Key Vault, referenced by App Configuration as a vault key — not inlined or injected as plain-text environment variables.
- **Bootstrap module is cleanly separated** from the application infrastructure with its own provider and state, which is the correct pattern for managing shared/foundational resources.
- **Provider version is exactly pinned** (`= 4.1.0`) and verified by the lock file hash — no drift risk from upstream provider changes.

---

### Next Steps

1. **[Critical] Fix the wrong job output reference** — `steps.datetime.outputs.full` → `steps.image_tag.outputs.full` in `pushImageToDockerHub.outputs`. Nothing deploys the correct image until this is fixed.
2. **[Critical] Add `pushImageToDockerHub` to `deploy.needs`** — the `deploy` job must declare this dependency to legally consume its output.
3. **[Critical] Replace `terraform_version: "1.15.2"`** with a valid version such as `"1.12.2"` in both `terraformPlan` and `terraformApply`.
4. **[Critical] Align artifact action versions** — set both to `actions/upload-artifact@v4` and `actions/download-artifact@v4`.
5. **[Critical] Add `db_connection_string_value` to the pipeline** — expose it as `TF_VAR_db_connection_string_value` from a GitHub secret in the `terraformPlan` step.
6. **[High] Add Managed Identity to the Container App** and grant it `App Configuration Data Reader` RBAC — required for the app to authenticate to App Configuration at runtime.
7. **[High] Migrate Key Vault to RBAC** — set `enable_rbac_authorization = true`, replace the access policy with scoped role assignments, and remove the `Purge` permission from the CI/CD SP.
8. **[High] Pin all GitHub Actions to commit SHAs** and enable Dependabot for `github-actions`.
9. **[High] Uncomment the Log Analytics workspace** and add diagnostic settings to Key Vault and App Configuration.
10. **[Medium] Add NuGet caching** with `actions/cache@v4` in the `build` and `tests` jobs.
11. **[Medium] Add `lifecycle { prevent_destroy = true }`** to Key Vault, App Configuration, and Container App Environment.
12. **[Medium] Add the `ingress` block** to `azurerm_container_app` to make traffic policy explicit.
13. **[Medium] Parameterize the Container App Environment name** — replace `"production"` literal with `var.environment`.
14. **[Low] Fix the `project` tag** in `infrastructure/bootstrap/required_main.tf` from `"portfolio"` to `"timezone-management"`.
15. **[Low] Add `azurerm_management_lock`** (CanNotDelete) to Key Vault and App Configuration.
