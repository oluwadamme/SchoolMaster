# Multi-Tenant Delivery Model

## The Question This Guide Answers

When a school onboards to SchoolMaster, do you need to deploy a new server, a new app, or anything new at all? And what exactly does the subdomain you create for them represent?

The short answer: **you deploy nothing**. The subdomain is a lookup label, not an infrastructure unit.

---

## The Three SaaS Tenancy Delivery Models

### Model 1: Silo Tenancy — One Deployment Per School

Every new school gets its own server, Docker container, CI/CD pipeline, and database. Their subdomain points to their own isolated deployment.

```
greenfield.schoolmaster.io  →  Server A  (Greenfield Academy only)
sunrise.schoolmaster.io     →  Server B  (Sunrise School only)
oakridge.schoolmaster.io    →  Server C  (Oakridge School only)
```

**Pros:** Total isolation. One school's outage or bug cannot affect another.

**Cons:** With 50 schools you manage 50 deployments, 50 pipelines, 50 databases, and 50 update cycles. This is how enterprise on-premise software is sold, not subscription SaaS.

**Verdict for SchoolMaster: Wrong model.**

---

### Model 2: Shared Deployment — Subdomain as a Routing Label

One API. One frontend. All schools share the same infrastructure. The subdomain is just a key that tells your single API which school's data to return.

```
greenfield.schoolmaster.io  ─┐
sunrise.schoolmaster.io     ─┼─→  Single API  →  Single Database (rows scoped by TenantId)
oakridge.schoolmaster.io    ─┘
```

A wildcard DNS record (`*.schoolmaster.io`) routes all subdomains to the same server. When a user visits `greenfield.schoolmaster.io`, their browser hits the same API as every other school. The subdomain travels as a header, the middleware resolves it to a `TenantId`, and the EF Core global query filter ensures every database query returns only that school's rows.

**Pros:** Deploy once. Update once. Scale once.

**Cons:** Less infrastructure isolation between tenants (mitigated by TenantId scoping).

**Verdict for SchoolMaster: This is the correct model, and the backend is already built for it.**

---

### Model 3: API-Only — Schools Build Their Own Frontends

SchoolMaster exposes a documented REST API. Schools hire developers to build custom portals on top of it. You are a backend platform, not an application.

**Pros:** Maximum flexibility for technically capable schools.

**Cons:** High barrier to entry. Most schools you are targeting do not have a development team. You lose adoption to competitors who offer a ready-to-use interface.

**Verdict for SchoolMaster: A valid optional add-on, not the primary delivery model.**

---

## The Recommended Architecture

Model 2 as your primary delivery, with Model 3 available as a bonus since the API is already REST-based and documented.

```
DNS wildcard:  *.schoolmaster.io  →  one load balancer
                                          │
                              ┌───────────┴────────────┐
                              │                        │
                     Frontend (Next.js)           SchoolMaster API
                     Single deployment            Single deployment
                              │                        │
                     Reads window.location        TenantResolverMiddleware
                     Extracts subdomain           Looks up Tenant by subdomain
                     Sends X-Tenant-Subdomain     Resolves TenantId into scope
                                                  EF Core query filter does the rest
```

Every school that onboards gets:
- Their subdomain (`greenfield.schoolmaster.io`) instantly — you add one row to the `Tenants` table
- Complete data isolation via the `TenantId` query filter
- The same frontend, optionally branded with their school name, logo, and colours

No new deployment. No new server. No new pipeline.

---

## How Tenant Resolution Works Today

The `TenantResolverMiddleware` already implements this pattern:

```
Request arrives at API
    → Middleware reads X-Tenant-Subdomain header
    → Queries Tenants table: SELECT * WHERE Subdomain = 'greenfield'
    → Stores resolved TenantId in HttpContext.Items["TenantId"]
    → CurrentTenant service reads it from HttpContext.Items
    → Every EF Core query filters by that TenantId automatically
```

For local development the frontend passes the subdomain as an explicit `X-Tenant-Subdomain` header.

For production, the cleaner approach is to read the `Host` header directly:

```
Request to greenfield.schoolmaster.io
→ Host header contains: greenfield.schoolmaster.io
→ Middleware splits on "." and extracts "greenfield"
→ No need for the frontend to manually set the header
```

This eliminates an entire class of bugs where the frontend forgets to send the header. Keep the `X-Tenant-Subdomain` approach for local development and introduce `Host`-based resolution for production.

---

## Onboarding a New School: What Actually Happens

```
School signs up
    → POST /api/v1/onboarding
    → OnboardingService creates one Tenant row (Id, Name, Subdomain, Plan)
    → OnboardingService creates one Admin User row (TenantId = tenant.Id)
    → OTP email sent to Admin for email verification
    → Done
```

No new server. No DNS change by you. The wildcard DNS record already covers their subdomain. The school can visit `theirname.schoolmaster.io` immediately after onboarding completes.

---

## Custom Domain Support

Some schools will want `portal.greenfieldacademy.edu` instead of `greenfield.schoolmaster.io`. This is a legitimate request and worth supporting eventually.

**What changes:**

1. Add a nullable `CustomDomain` column to the `Tenant` entity
2. Update `TenantResolverMiddleware` to check `CustomDomain` as a second lookup path when the `Host` header does not match `*.schoolmaster.io`
3. SSL certificate provisioning — the hard part. Your wildcard `*.schoolmaster.io` cert does not cover external domains. Each custom domain needs its own certificate issued and renewed automatically (Let's Encrypt or Cloudflare for SaaS)
4. DNS verification step — before activating a custom domain, the school must add a CNAME record proving they own it

**Recommendation: Do not build this yet.** Design the `Tenant` entity with a `CustomDomain` column from the start (so you do not need a data migration later), but leave the feature inactive. Build it when a real school specifically requests it.

| Feature | Subdomain | Custom Domain |
|---|---|---|
| Infrastructure complexity | Low | Medium-high |
| SSL management | Single wildcard cert | Per-domain cert provisioning |
| DNS setup by school | None | School adds a CNAME record |
| School branding | Partial | Full |
| Implementation time | Already done | 1-2 weeks |

The fastest path when you do build custom domains is **Cloudflare for SaaS** — their custom hostname product handles cert issuance and renewal entirely. You call their API when a school activates a custom domain.

---

## What to Watch Out For

**TenantId scoping is non-negotiable.** The shared deployment model is only safe because every database query is filtered by `TenantId`. Any raw SQL query or `IgnoreQueryFilters()` call bypasses the global filter. Every new entity must be verified against the filter.

**Subdomain validation at onboarding.** The subdomain becomes part of a URL. Validate it is alphanumeric with optional hyphens. Block reserved names: `admin`, `api`, `www`, `app`, `health`, `dashboard`, `status`.

**Frontend must always send the tenant context.** Whether via `X-Tenant-Subdomain` header or via the `Host` header resolution approach, every API call must carry the tenant identifier. If it is missing, `TenantId` resolves to `Guid.Empty` and protected queries return no data or throw.

**Plan enforcement is separate from tenancy.** The `Tenant` entity has a `Plan` field (`Basic`, `Pro`, etc.). Feature gating by plan is a separate concern from data isolation and should be enforced as a policy check, not inside the tenant resolver.
