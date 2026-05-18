## DevOps Review — 2026-05-18

### Summary

The infrastructure has a solid foundation: remote state is backed by Azure Blob Storage with deletion locks, secrets are stored in Key Vault with a vault-reference pattern through App Configuration, OIDC is used for Azure authentication, and production jobs are gated by an environment approval. The highest-risk finding is that the Container App has no managed identity and no RBAC grants to read from App Configuration or Key Vault — meaning the deployed application likely cannot retrieve its own database connection string at runtime. Two other high-priority gaps are the absence of any ingress configuration on the Container App (leaving HTTP access undefined) and the complete absence of observability (the Log Analytics workspace is commented out). Quick wins are adding NuGet package caching to the CI pipeline and pinning all action versions to commit SHAs.

---

### Findings

| # | Severity | Category | Resource | Finding | Recommendation |
|---|----------|----------|----------|---------|----------------|
| 1 | **High** | Security | `azurerm_container_app` | No managed identity is configured on the Container App, and no RBAC role assignments or access policies exist to grant it access to App Configuration or Key Vault. The application depends on App Configuration (with a Key Vault vault reference) for the DB connection string, so the app cannot read its own config at runtime. | Add `identity { type = "SystemAssigned" }` to the Container App. Assign it the `App Configuration Data Reader` built-in role on the App Configuration instance and the `Key Vault Secrets User` role on the Key Vault using `azurerm_role_assignment`. |
| 2 | **High** | Security | `azurerm_container_app` | No `ingress` block is defined. Without it, external HTTP access, the target port, and traffic weights are unspecified — the API will not be reachable via a stable FQDN. | Add an explicit `ingress` block: `external_enabled = true`, `target_port = <app port>`, and a `traffic_weight` block with `percentage = 100` and `latest_revision = true`. |
| 3 | **High** | Observability | `azurerm_container_app_environment` / `azurerm_key_vault` | The `azurerm_log_analytics_workspace` resource is commented out with a note saying "local application." The Container App environment's `log_analytics_workspace_id` is also commented out. Neither the Container App nor Key Vault has an `azurerm_monitor_diagnostic_setting`. There are no alerting rules. | Uncomment and create the Log Analytics workspace. Attach it to `azurerm_container_app_environment` via `log_analytics_workspace_id`. Add `azurerm_monitor_diagnostic_setting` resources for Key Vault and the Container App environment to forward audit and platform logs. |
| 4 | **Medium** | Security | `azurerm_key_vault_access_policy.terraform_sp` | The Terraform service principal is granted `Purge` permission on the Key Vault. In production, `Purge` allows permanently destroying soft-deleted secrets with no recovery path — it should not be a routine operational permission. | Remove `Purge` from the standing access policy. If needed for teardown, add it explicitly in a CI job scoped to a destroy workflow, or grant it only to a break-glass identity. |
| 5 | **Medium** | Terraform | `azurerm_key_vault`, `azurerm_container_app` | Neither critical production resource has a `lifecycle { prevent_destroy = true }` block. A `terraform apply` with a config change that triggers resource replacement (e.g., a Key Vault name change) would silently destroy and recreate the resource. | Add `lifecycle { prevent_destroy = true }` to `azurerm_key_vault.app_config_key_vault` and `azurerm_container_app.container_app`. |
| 6 | **Medium** | Terraform | `infrastructure/main.tf` | `azurerm` provider is pinned to `= 4.1.0`, which was released approximately 18 months before today (May 2026). Staying this far behind means missing security patches, bug fixes, and support for new resource arguments. | Upgrade to the latest stable `azurerm` 4.x release. Run `terraform init -upgrade`, review the changelog for breaking changes, update the lock file, and re-run `terraform plan` to validate. |
| 7 | **Medium** | CI/CD | All workflow actions | Every action in the pipeline is referenced by a floating major tag (`@v3`, `@v4`, `@v5`, `@v6`, `@v7`). Floating tags can be silently overwritten by the action's author, enabling supply-chain injection without a diff in your workflow file. | Pin every action to a full commit SHA. Example: `actions/checkout@<sha>` instead of `@v6`. Use a tool like Dependabot or `pin-github-action` to automate SHA pinning and receive PRs when new versions are released. |
| 8 | **Medium** | CI/CD | `build`, `tests` jobs | Both jobs run `dotnet restore` independently without caching NuGet packages. On every push, packages are re-downloaded from scratch, adding unnecessary minutes to every pipeline run. | Add an `actions/cache` step before `dotnet restore` in both jobs, keyed on a hash of `**/packages.lock.json` or `**/*.csproj` files, caching `~/.nuget/packages`. |
| 9 | **Low** | Terraform | `azurerm_container_app_environment` | Resource is named `"production"` — no project prefix. This name is too generic for a shared subscription and does not follow the `<project>-<env>` convention used by other resources. | Rename to `timezone-management-production` to be consistent with `timezone-management-api` and make the resource identifiable in the Azure portal. |
| 10 | **Low** | CI/CD | `deploy` job | `AZURE_RESOURCE_GROUP` and `CONTAINER_APP_NAME` are non-sensitive deployment targets but are stored as `secrets.*`. Secrets are masked in logs, making debugging harder, and their use is conceptually misleading. | Move non-sensitive environment config to `vars.*` (`${{ vars.AZURE_RESOURCE_GROUP }}` and `${{ vars.CONTAINER_APP_NAME }}`). Reserve `secrets.*` for credentials only. |
| 11 | **Low** | CI/CD | `actions/download-artifact@v7` | `upload-artifact` and `download-artifact` are both at `v7` (consistent — no protocol mismatch). However, `download-artifact` v8 is now available with additional features and bug fixes. | Upgrade both `actions/upload-artifact` and `actions/download-artifact` together to `v8` (they must match to avoid protocol issues). |

---

### Positive Observations

- **Remote state is properly bootstrapped** — Terraform state is stored in Azure Blob Storage with both the resource group and storage account protected by `CanNotDelete` management locks. A separate bootstrap module handles the one-time setup cleanly.
- **Sensitive variable is marked correctly** — `db_connection_string_value` is declared `sensitive = true`, preventing it from appearing in plan/apply output.
- **Secrets management pattern is sound** — The DB connection string flows: `tfvar → Key Vault secret → App Configuration vault key reference`, avoiding any plaintext storage in config files or state.
- **Key Vault hardening is in place** — `purge_protection_enabled = true` and `soft_delete_retention_days = 7` are set, protecting against accidental and malicious permanent deletion.
- **OIDC authentication used** — The pipeline uses `azure/login` with `client-id`/`tenant-id`/`subscription-id` (federated credentials), avoiding long-lived client secrets entirely.
- **Production environment gates applied** — Both `terraformApply` and `deploy` jobs declare `environment: production`, enabling branch protection rules and manual approval gates.
- **Cross-job outputs are correctly wired** — The `pushImageToDockerHub` job declares a `outputs.image` output, and the `deploy` job correctly consumes it via `needs.pushImageToDockerHub.outputs.image`.
- **Terraform plan artifact is properly scoped** — The plan is uploaded from `terraformPlan` and downloaded in `terraformApply`, ensuring the applied plan is exactly what was reviewed.
- **Cost allocation tags are consistent** — All resources apply the `local.tags` block with `environment`, `project`, `owner`, and `managed_by` keys.
- **Variable validation is present** — The `environment` variable uses a `contains()` validation rule in both `main` and `bootstrap` modules to catch invalid values at plan time.
- **Storage account is hardened** — `min_tls_version = "TLS1_2"`, `https_traffic_only_enabled = true`, blob versioning, and a 7-day delete retention policy are all set on the tfstate storage account.

---

### Next Steps

Address findings in this order:

1. **Add managed identity and RBAC to the Container App** (Finding #1) — This is a correctness issue: the app cannot read its database connection string without it. Add `identity { type = "SystemAssigned" }` and two `azurerm_role_assignment` resources.
2. **Add an ingress block to the Container App** (Finding #2) — Without ingress configuration, the API has no defined external access. Add the `ingress` block with `external_enabled = true` and the correct `target_port`.
3. **Enable observability** (Finding #3) — Uncomment the Log Analytics workspace, attach it to the Container App environment, and add diagnostic settings for Key Vault. Even basic platform logs will dramatically improve incident response.
4. **Remove `Purge` from the routine KV access policy** (Finding #4) — Low-effort, immediate security improvement.
5. **Add `lifecycle { prevent_destroy = true }` to Key Vault and Container App** (Finding #5) — One-line change per resource; prevents a misconfigured apply from silently destroying production resources.
6. **Upgrade `azurerm` provider from 4.1.0** (Finding #6) — Run `terraform init -upgrade` and test; this picks up 18 months of patches.
7. **Pin all GitHub Actions to commit SHAs** (Finding #7) — Use Dependabot's `github-actions` ecosystem to automate ongoing maintenance.
8. **Add NuGet package caching** (Finding #8) — Add `actions/cache` to `build` and `tests` jobs; expect a meaningful reduction in pipeline duration after the first warm run.
9. **Rename Container App environment** (Finding #9) and **move non-sensitive vars from secrets to vars** (Finding #10) — Low-effort cleanup.
10. **Upgrade artifact actions to v8** (Finding #11) — Update both `upload-artifact` and `download-artifact` together.
