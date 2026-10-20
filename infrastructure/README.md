# Backend deployment contract

BusinessSuite consumes Framework's libraries, while owning its API, database schema, identity configuration and deployment lifecycle. Framework's `infrastructure/README.md` supplies useful deployment conventions: use one IaC owner per environment, managed identities where supported, protected state, deployment secrets and separate provisioning/application delivery.

The Framework reference infrastructure provisions general hosting and Azure SQL; optional AI resources now belong to AI-Framework. BusinessSuite's current migration targets PostgreSQL and has no AI endpoints or ingestion workers. Its first deployment therefore requires a PostgreSQL service and a separate resource plan; do not deploy the reference stack unchanged. No BusinessSuite cloud resources have been provisioned here.

| Setting | Requirement |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` outside local development |
| `ConnectionStrings__DefaultConnection` | PostgreSQL secret; production should require TLS |
| `Database__Provider` | `PostgreSql` in this increment |
| `Authentication__Authority` | HTTPS OIDC authority issuing validated access tokens |
| `Authentication__Audience` | BusinessSuite API audience |
| `AllowedHosts` | Explicit deployed hostnames; default permits localhost only |
| `ASPNETCORE_HTTPS_PORT` | Public HTTPS port when redirecting from the container HTTP port |

Deploy behind a TLS ingress. Configure trusted forwarded headers/proxies and the public HTTPS redirect port for the actual hosting platform before rollout; forwarding behavior is not universally configured by this scaffold. Health probes can call `/health/live` and `/health/ready` directly. The limiter is in-process; distributed/ingress enforcement is required when replicas scale. Anonymous health probes use the anonymous/IP limiter partition.

Use a separate migration/provisioning identity with schema privileges and a runtime identity with only required data access. Protect identity and audit tables from runtime mutation where practical. Initial owner bootstrap is an explicit trusted operator command and must use the correct identity-provider subject and tenant ID. Disable operator secrets in the running API environment after provisioning.

Delivery sequence:

1. Validate the pinned Framework revision, package advisories, build/tests and generated migration SQL.
2. Provision isolated PostgreSQL, API hosting, secret storage, networking and telemetry for the environment using a reviewed IaC plan.
3. Apply reviewed migrations through a deployment job; keep migrations out of normal startup. Backup and rollback/forward-fix procedures must be defined before changing existing data.
4. Provision the first tenant owner explicitly. Configure token tenant claims and membership lifecycle with the identity provider.
5. Deploy the container, check readiness and smoke-test authenticated hierarchy creation and unauthorized access rejection.
6. Validate backups, restore drills, alerting and least-privilege database credentials before production release.

Document storage, queues, durable AI ingestion, outbox delivery, private endpoints and cloud IaC templates are subsequent foundation work. Choose Bicep or Terraform for each resource set and document ownership before adding them.
