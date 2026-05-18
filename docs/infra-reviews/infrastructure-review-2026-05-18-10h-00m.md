## DevOps Review — 2026-05-18

### Summary

The main infrastructure module and CI/CD pipeline have several **blocking defects** that prevent the pipeline from ever completing a deploy. Most critically, the `terraformPlan` and `terraformApply` jobs are accidentally nested inside the `pushImageToDockerHub` job's step block — they are invisible to GitHub Actions and will never execute. The pipeline also references a non-existent Terraform version (`1.15.2`), mismatched artifact action versions, a missing artifact (`static-out`), and a cross-job step output reference that silently resolves to an empty string. On the Terraform side, the Container App is still using a Microsoft quickstart placeholder image, Key Vault and App Configuration resources are missing tags and diagnostic settings, and the legacy access-policy model is used instead of Key Vault RBAC. Quick wins: fix job indentation, correct the Terraform version, align artifact action versions, and add the missing `setup-dotnet` step.

### Findings

| # | Severity | Category | Resource | Finding | Recommendation |
|---|----------|----------|----------|---------|----------------|
| 1 | Critical | CI/CD Structure | `jobs.pushImageToDockerHub` | `terraformPlan` and `terraformApply` are indented as YAML keys under `pushImageToDockerHub` (4-space indent inside the job), not as top-level peers under `jobs:`. GitHub Actions silently ignores them — they never run. | De-indent both job blocks to 2 spaces so they sit at the same level as `build`, `tests`, `pushImageToDockerHub`, and `deploy` under `jobs:`. |
| 2 | Critical | CI/CD Pipeline | `hashicorp/setup-terraform` | `terraform_version: "1.15.2"` — Terraform's versioning is `1.MINOR.PATCH` with minor ≤ 9 as of late 2025. Version `1.15.2` does not exist; the action will fail to download. | Use a real version that satisfies `>= 1.5.2`, e.g. `"1.9.8"`. Align with the highest stable 1.x release available. |
| 3 | Critical | CI/CD Pipeline | `actions/upload-artifact@v7` vs `actions/download-artifact@v8` | Artifact action versions are mismatched (upload v7, download v8). Additionally, as of mid-2025 the latest published versions were v4; v7/v8 do not exist, meaning both steps will fail at action resolution. | Pin both to the same existing version — `actions/upload-artifact@v4` and `actions/download-artifact@v4` — or use commit SHAs. |
| 4 | High | CI/CD Pipeline | `jobs.deploy` | `imageToDeploy` references `steps.datetime.outputs.now` — a step output from the `pushImageToDockerHub` job. Cross-job step outputs are illegal; this expression evaluates to an empty string, producing an invalid image tag like `user/timezone-management-api:`. | Promote the output from `pushImageToDockerHub` to a job-level `output:` (`datetime: ${{ steps.datetime.outputs.now }}`), then consume it in `deploy` via `needs.pushImageToDockerHub.outputs.datetime`. |
| 5 | High | CI/CD Pipeline | `jobs.terraformApply` | Downloads an artifact named `static-out` — but no job in the pipeline ever uploads it. This step will always fail with "artifact not found". | Remove the `Download build artifact` step and the subsequent Static Web Apps deploy step entirely; they appear to be copy-paste from a different project. |
| 6 | High | CI/CD Pipeline | `jobs.tests` | `dotnet test` runs without a `actions/setup-dotnet` step. The `tests` job relies on whatever .NET version the runner image happens to have pre-installed — which may not match the SDK version required by the project. | Add `actions/setup-dotnet@v4` with `dotnet-version: 10.0.x` (matching the `build` job). |
| 7 | High | CI/CD Pipeline | `jobs.terraformApply` | `terraform output -raw web_app_api_key` — no output named `web_app_api_key` is defined in any `.tf` file. This command will return a non-zero exit code and fail the job. | Remove this step; it is leftover from a different project. The Azure Container Apps deploy action does not require a static web app API token. |
| 8 | High | CI/CD Pipeline | `jobs.build` / `jobs.tests` | `dotnet restore` is called in `build` and `dotnet test` (which implicitly restores) runs in `tests`, with no NuGet package cache. Every run re-downloads all packages, wasting runner minutes and making builds sensitive to NuGet feed availability. | Add `actions/cache@v4` keyed on `**/packages.lock.json` before the restore step in both jobs. |
| 9 | High | Terraform | `azurerm_container_app` | The container image is hard-coded to `mcr.microsoft.com/k8se/quickstart:latest` — a Microsoft placeholder that runs a sample app. The actual API is never deployed via Terraform; the image is also pinned to `:latest`, which is non-deterministic. | Replace with a variable or locals reference pointing to the tagged image built and pushed in the `pushImageToDockerHub` job (e.g., `var.container_image`). Terraform apply should receive the specific image tag produced by the pipeline. |
| 10 | High | Terraform | `terraform.required_version` | `>= 1.5.2` has no upper bound — the same issue flagged in the bootstrap review (#3 in the 2026-05-16 report). A future Terraform 2.x would satisfy this constraint but may introduce breaking changes. | Change to `>= 1.5.2, < 2.0.0`. |
| 11 | Medium | CI/CD Pipeline | All `uses:` references | Every action is pinned to a floating major-version tag (`@v3`, `@v4`, `@v5`, `@v6`). Floating tags can be silently force-pushed by the action author to point at any commit, including malicious changes. | Pin to commit SHAs (e.g., `actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af68` for v4). Use a tool like Dependabot or `pin-github-action` to automate this. |
| 12 | Medium | CI/CD Pipeline | `actions/checkout@v6` | As of mid-2025, `actions/checkout` latest was `v4`. `v6` may not exist; if it does, it is unverified. | Verify the exact latest release and pin to its commit SHA. |
| 13 | Medium | Terraform | `azurerm_app_configuration`, `azurerm_key_vault`, `azurerm_container_app_environment` | None of these three resources have a `tags` attribute. Cost allocation and resource attribution are broken for these resources. | Add `tags = local.tags` to all three resource blocks. |
| 14 | Medium | Observability | `azurerm_key_vault`, `azurerm_app_configuration` | No `azurerm_monitor_diagnostic_setting` on either resource. Secret access, configuration reads, and audit events are invisible. | Add diagnostic settings forwarding `AuditEvent` logs (Key Vault) and `HttpRequest` logs (App Config) to a Log Analytics Workspace. The workspace block in `main.tf` is already commented-out; uncomment it first. |
| 15 | Medium | Security | `azurerm_key_vault.app_config_key_vault` | Using the legacy `azurerm_key_vault_access_policy` model instead of `enable_rbac_authorization = true`. Access policies are harder to audit and do not integrate with Azure PIM for JIT access. | Set `enable_rbac_authorization = true` and replace the policy resource with `azurerm_role_assignment` using the `Key Vault Secrets Officer` built-in role. |
| 16 | Medium | Security | `azurerm_key_vault_access_policy.terraform_sp` | The Terraform service principal is granted `Purge` permission. This allows permanent deletion of secrets bypassing soft-delete, which is a dangerous capability for a CI runner identity. | Remove `Purge` from the access policy (or role assignment). Only break-glass human operators should have purge capability. |
| 17 | Medium | Terraform | `azurerm_container_app_environment` | `name = "production"` is hardcoded as a string literal instead of using `var.environment`. Running `terraform apply -var="environment=staging"` would create a second environment still named `"production"`. | Change to `name = var.environment` or a computed name like `"timezone-${var.environment}"`. |
| 18 | Low | Security | `azurerm_key_vault`, `azurerm_app_configuration` | No `azurerm_management_lock` on either resource. The RG-level lock prevents group deletion but not deletion of individual resources within it. | Add `CanNotDelete` locks on the Key Vault and App Configuration, mirroring the pattern in the bootstrap module. |
| 19 | Low | Terraform | `azurerm_key_vault.app_config_key_vault` | Key Vault name is the hardcoded literal `"appconfigkeyvault"` — globally unique in Azure, and collisions will silently fail. Impossible to deploy a second environment without renaming. | Include the environment in the name: `"timezone-${var.environment}-kv"`. |
| 20 | Low | Azure Compliance | `azurerm_resource_group.raffa_lab_rg` (bootstrap) | `RaffaLabRG` does not embed the project or environment. Already flagged in the 2026-05-16 bootstrap review (#7). | Parameterize: `"RaffaLab${title(var.environment)}RG"`. |
| 21 | Low | CI/CD Pipeline | Top-level `permissions:` | `actions: read` is declared but no step in any job reads workflow artifacts via the Actions API. | Remove `actions: read` unless a specific step requires it — prefer the narrowest permission set. |

### Positive Observations

- **Remote state backend is fully configured** — Azure Storage backend with container and key specified; bootstrap module provisions and locks the backend resources.
- **Provider version is exactly pinned** (`= 4.1.0`) and verified by the lock file hash — no drift risk.
- **Secrets hygiene in the pipeline is correct** — `DOCKERHUB_TOKEN`, `AZURE_CLIENT_ID/TENANT_ID/SUBSCRIPTION_ID` are all `secrets.*`; non-sensitive config like `DOCKERHUB_USERNAME` correctly uses `vars.*`.
- **OIDC authentication** — the pipeline uses `azure/login` with federated identity (`id-token: write`), avoiding long-lived service principal secrets.
- **`db_connection_string_value` is `sensitive = true`** — the connection string is never echoed in `terraform plan` output.
- **Secrets stored in Key Vault, referenced by App Config** — the connection string is not inlined in App Configuration; it is stored as a Key Vault vault-reference key, which is the correct pattern.
- **Key Vault purge protection is enabled** — prevents soft-deleted vaults from being permanently destroyed within the retention window.
- **`environment` variable has a validation block** — invalid environment names are caught at plan time, not silently deployed.
- **Cost-allocation tags are defined in a single `locals` block** — consistent across all tagged resources and easy to update centrally.
- **`environment: production`** is set on the `deploy` job — approval gates and branch protection rules can be enforced.
- **Bootstrap storage account**: TLS 1.2, HTTPS-only, private container, blob versioning, and delete retention — all present and correct.

### Next Steps

1. **[Critical]** Fix YAML indentation — move `terraformPlan` and `terraformApply` to top-level jobs under `jobs:`.
2. **[Critical]** Replace `terraform_version: "1.15.2"` with a real version (e.g. `"1.9.8"`).
3. **[Critical]** Align artifact action versions — use `actions/upload-artifact@v4` and `actions/download-artifact@v4`.
4. **[High]** Fix cross-job image tag reference in `deploy` — wire `datetime` through job outputs.
5. **[High]** Remove orphaned `static-out` download step and the Static Web Apps deploy step.
6. **[High]** Add `actions/setup-dotnet@v4` to the `tests` job.
7. **[High]** Remove `terraform output -raw web_app_api_key` step (undefined output).
8. **[High]** Replace the placeholder container image with a parameterized image reference using the tag produced by `pushImageToDockerHub`.
9. **[High]** Add NuGet package caching with `actions/cache@v4` in `build` and `tests`.
10. **[Medium]** Pin all actions to commit SHAs; enable Dependabot for `github-actions` to automate updates.
11. **[Medium]** Add `tags = local.tags` to `azurerm_app_configuration`, `azurerm_key_vault`, and `azurerm_container_app_environment`.
12. **[Medium]** Switch Key Vault to RBAC mode (`enable_rbac_authorization = true`) and remove the `Purge` permission.
13. **[Medium]** Uncomment the Log Analytics Workspace and add diagnostic settings to Key Vault and App Configuration.
14. **[Low]** Add `azurerm_management_lock` (CanNotDelete) to Key Vault and App Configuration.
15. **[Low]** Parameterize Key Vault and App Configuration names to include the environment suffix.
