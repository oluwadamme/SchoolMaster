# Refresh Tokens — What, Why, and How

## The Problem You're Solving

Right now, your login flow works like this:

```
User logs in → gets a JWT (access token) → uses it for 60 minutes → token expires → user must log in again
```

That's annoying for users. Imagine being forced to re-enter your password every hour.

## What Is a Refresh Token?

A **refresh token** is a second, long-lived token issued alongside the JWT. It exists for one purpose only: **getting a new access token without re-entering credentials**.

```
User logs in → gets JWT (60 min) + Refresh Token (7 days)
                    ↓
         JWT expires after 60 min
                    ↓
     Client sends refresh token to /api/v1/auth/refresh-token
                    ↓
       Server validates it → issues NEW JWT + NEW refresh token
                    ↓
           User continues without interruption
```

## Access Token vs Refresh Token

| | Access Token (JWT) | Refresh Token |
|---|---|---|
| **Purpose** | Authorize API requests | Get new access tokens |
| **Lifetime** | Short (15-60 min) | Long (7-30 days) |
| **Stored where** | Memory / app state | Secure storage (HttpOnly cookie, Keychain) |
| **Sent with** | Every API request | Only to `/refresh-token` endpoint |
| **Format** | JWT (self-contained, has claims) | Opaque random string |
| **Validated how** | Signature check (stateless) | Database lookup (stateful) |

> [!IMPORTANT]
> The JWT is **stateless** — your server never stores it. The refresh token is **stateful** — it's stored in the database so you can revoke it.

## Why Not Just Make the JWT Last Longer?

Because JWTs **cannot be revoked**. Once issued, they're valid until expiry. If a JWT with a 30-day lifetime gets stolen, the attacker has 30 days of access with no way to stop them.

With refresh tokens:
- JWT gets stolen? It expires in 60 minutes. Limited damage.
- Refresh token gets stolen? You **revoke it in the database**. Immediate protection.

## Security Concepts

### Token Rotation
Every time a refresh token is used, you **invalidate the old one and issue a new one**. This is called **rotation**. If an attacker steals an old refresh token and tries to use it, you know something is wrong.

### Token Families
If a revoked refresh token is used, it could mean the token was stolen. A paranoid approach revokes **all** refresh tokens for that user (forcing re-login on all devices).

---

## The Complete Flow

```
┌─────────────────────────────────────────────────────────┐
│                    INITIAL LOGIN                         │
│                                                         │
│  POST /api/v1/auth/login                                │
│  { email, password }                                    │
│         ↓                                               │
│  Response: {                                            │
│    token: "eyJhbG...",        ← JWT, expires in 60 min  │
│    refreshToken: "a8Kx9...", ← stored in DB, 7 days    │
│  }                                                      │
└─────────────────────────────────────────────────────────┘

        ... 60 minutes later, JWT expires ...

┌─────────────────────────────────────────────────────────┐
│                   TOKEN REFRESH                          │
│                                                         │
│  POST /api/v1/auth/refresh-token                        │
│  { refreshToken: "a8Kx9..." }                          │
│         ↓                                               │
│  Server checks DB → valid + not expired                 │
│         ↓                                               │
│  Response: {                                            │
│    token: "eyJnew...",        ← NEW JWT                 │
│    refreshToken: "b7Zy3...", ← NEW refresh token        │
│    }                                                    │
│  (old refresh token "a8Kx9..." is now invalid)          │
└─────────────────────────────────────────────────────────┘
```

---

### In Summary

Refresh tokens allow you to keep your access tokens short-lived (secure) while providing a seamless user experience (no constant re-logging). Combined with **rotation**, they provide a robust security model for modern web applications.
 NEW refresh token        │
│    }                                                    │
│  (old refresh token "a8Kx9..." is now invalid)          │
└─────────────────────────────────────────────────────────┘
```

---

### In Summary

Refresh tokens allow you to keep your access tokens short-lived (secure) while providing a seamless user experience (no constant re-logging). Combined with **rotation**, they provide a robust security model for modern web applications.
