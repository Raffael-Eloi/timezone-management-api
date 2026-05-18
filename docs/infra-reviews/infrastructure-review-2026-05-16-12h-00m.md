## DevOps Review — 2026-05-16

### Summary

The bootstrap module is well-structured for a portfolio project: provider and resource locks are in place, tags are comprehensive, TLS/HTTPS are enforced, and blob versioning protects against state corruption. The highest-risk gap is the storage account accepting traffic from **all public networks** — since Terraform state can hold sensitive values (connection strings, secrets), unrestricted public access is a meaningful attack surface. Quick wins are adding `network_rules`, explicitly setting `allow_nested_items_to_be_public = false`, and tightening the Terraform version upper bound.

### Findings

| # | Severity | Category | Resource | Finding | Recommendation |
|---|----------|----------|----------|---------|----------------|
| 1 | Critical | Security | `azurerm_storage_account.storage_account` | No `network_rules` block — the state backend accepts connections from all public networks. Terraform state may contain sensitive values. | Add `network_rules { default_action = "Deny"; ip_rules = [...] }` to allowlist only trusted CIDRs (CI runners, developer egress IPs), or front with a private endpoint. |
| 2 | High | Security | `azurerm_storage_account.storage_account` | `allow_nested_items_to_be_public` is not explicitly set to `false` at the account level. Container-level private access alone is insufficient defence-in-depth. | Add `allow_nested_items_to_be_public = false` to the storage account block. |
| 3 | High | Terraform BP | `terraform {}` | Version constraint `>= 1.5.2` has no upper bound — a hypothetical Terraform 2.x could introduce breaking changes and still satisfy this constraint. | Change to `>= 1.5.2, < 2.0.0` to explicitly scope to the 1.x series. |
| 4 | High | Terraform BP | N/A | No `backend {}` block is configured — the bootstrap module's own state is local. If this machine is lost, the state is gone, making it impossible to manage these resources via Terraform without import. | Expected for a bootstrap pattern, but document the procedure: after `apply`, migrate the bootstrap state into the newly created container, or at minimum commit a scrubbed state backup. |
| 5 | Medium | Terraform BP | `azurerm_storage_account.storage_account`, `azurerm_storage_container.storage_container` | No `lifecycle { prevent_destroy = true }` on the state backend resources. Management locks prevent Azure-side deletion but do not stop `terraform destroy`. | Add `lifecycle { prevent_destroy = true }` to both the storage account and the container. |
| 6 | Medium | Observability | `azurerm_storage_account.storage_account` | No `azurerm_monitor_diagnostic_setting` configured. Unauthorized access attempts to the state backend are invisible. | Add a diagnostic setting to forward `StorageRead`, `StorageWrite`, and `StorageDelete` logs to a Log Analytics Workspace (or at minimum to a storage sink). |
| 7 | Medium | Azure Compliance | `azurerm_resource_group.raffa_lab_rg` | Resource Group name `RaffaLabRG` does not embed the environment. Running this module for a second environment would require a rename or a fork. | Parameterize: `name = "RaffaLab${title(var.environment)}RG"` to produce `RaffaLabProductionRG`, `RaffaLabDevRG`, etc. |
| 8 | Medium | Security | `azurerm_storage_account.storage_account` | No customer-managed key (CMK) configured. State is encrypted with Microsoft-managed keys. For production workloads holding infrastructure secrets, CMK is the compliance baseline. | Add `azurerm_key_vault` + `azurerm_storage_account_customer_managed_key`. Lower priority if this is a personal portfolio with no compliance obligations. |
| 9 | Low | Terraform BP | `azurerm_management_lock.sa_lock` | `notes` says "Prevents accidental deletion of the **portfolio resource group**" — copy-paste error; this lock is on the storage account. | Fix notes to: `"Prevents accidental deletion of the portfolio storage account."` |
| 10 | Low | Cost | `azurerm_storage_account.storage_account` | `account_replication_type = "LRS"` — adequate for a dev/portfolio state backend but offers no zone or region redundancy. If the state file is the only recovery path for production infra, a regional failure destroys it. | Upgrade to `ZRS` (same-region, zone-resilient) for production. `GRS` only if cross-region DR is a requirement. |

### Positive Observations

- Provider pinned to an exact version (`= 4.1.0`) via both `required_providers` and the lock file — no unexpected drift.
- Management locks on both the Resource Group and Storage Account — protects against accidental Azure-side deletion.
- `https_traffic_only_enabled = true` and `min_tls_version = "TLS1_2"` enforce secure transport consistently.
- Blob versioning enabled with a 7-day soft-delete retention window — protects against accidental state overwrites.
- Comprehensive cost-allocation tags (`environment`, `project`, `owner`, `managed_by`) applied uniformly via a `locals` block.
- Storage account location references `azurerm_resource_group.raffa_lab_rg.location` rather than the variable directly — guarantees co-location.
- `environment` variable has a `validation` block with an explicit allowlist, preventing typos from silently creating mis-tagged environments.
- `azurerm_resource_provider_registration` for `Microsoft.Storage` ensures the provider is registered before the account is created — avoids a common first-deploy failure.
- Container access type is `private` — no public blob reads permitted at the container level.

### Next Steps

1. **[Critical]** Add `network_rules` to the storage account — restrict public access to known CIDR ranges or switch to private endpoint.
2. **[High]** Set `allow_nested_items_to_be_public = false` explicitly on the storage account.
3. **[High]** Tighten Terraform version constraint to `>= 1.5.2, < 2.0.0`.
4. **[High]** Document (or automate) the bootstrap state migration procedure so the local state file is not the single point of recovery.
5. **[Medium]** Add `lifecycle { prevent_destroy = true }` to the storage account and container.
6. **[Medium]** Add `azurerm_monitor_diagnostic_setting` on the storage account for blob audit logs.
7. **[Medium]** Parameterize the resource group name to include the environment.
8. **[Low]** Fix the copy-paste typo in `sa_lock.notes`.
9. **[Low]** Consider upgrading replication to `ZRS` for the production state backend.
