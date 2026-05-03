# Authorization CSV exports

Use these files in Excel or Google Sheets (Import → CSV UTF-8).

| File | Contents |
|------|-------------|
| `authorization_policies.csv` | Each ASP.NET policy name and how it evaluates |
| `permission_keys.csv` | Each `PermissionKeys.*` string and whether it appears in admin-all vs faculty-legacy JWT fallback |
| `jwt_permission_resolution.csv` | How JWT `permission` claims are built from roles vs application roles |
| `data_layer_rules.csv` | Faculty/staff scoping that happens inside controllers (beyond policies) |
| `authorization_endpoints.csv` | Routes with class/action policies and short notes |

Human-readable narrative and tables: `docs/AUTHORIZATION_MATRIX.md`.

When you add controllers or policies, update both the Markdown matrix and these CSVs so they stay aligned.
