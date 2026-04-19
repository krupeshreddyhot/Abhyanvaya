# Deploy Abhyanvaya for a demo

This walks through a **free-tier friendly** layout: **Neon** (PostgreSQL), **Render** (API Docker), **Cloudflare Pages** (static Vite UI). Substitute Netlify/Vercel/Fly.io if you prefer—the env vars stay the same idea.

### First-time database: Super Admin (after `dotnet ef database update`)

A seed user is added by migration `AddSuperAdminUser`: **username** `superadmin`, **password** `SuperAdmin@1` (change in production). Use the login page **Super Admin** mode, then open **Organization** to add universities and provision new college tenants. Institution users use **Institution** login (university + college code + their admin/faculty user).

---

## 1. Database (Neon)

1. Create a project at [neon.tech](https://neon.tech).
2. Copy the connection string (SSL). It looks like:
   `postgresql://user:password@host/dbname?sslmode=require`
3. Run EF migrations against Neon from your PC (once), from the repo root:

```bash
dotnet ef database update --project Abhyanvaya.Infrastructure --startup-project Abhyanvaya.API
```

Set `ConnectionStrings__DefaultConnection` to Neon first (env var or user-secrets), or pass it for one shot:

```bash
$env:ConnectionStrings__DefaultConnection="Host=...;Username=...;Password=...;Database=...;SSL Mode=require"
dotnet ef database update --project Abhyanvaya.Infrastructure --startup-project Abhyanvaya.API
```

Set `ConnectionStrings__DefaultConnection` to your Neon URL when deploying the API (see below).

---

## 2. API (Render — Docker)

1. Push this repo to GitHub/GitLab.
2. In Render: **New → Web Service**, connect the repo.
3. **Root directory**: leave empty (repo root contains `Dockerfile`).
4. **Dockerfile path**: `Dockerfile`
5. **Instance**: Free (cold starts are normal for demos).

### Environment variables (Render → Service → Environment)

| Key | Example / notes |
|-----|------------------|
| `ConnectionStrings__DefaultConnection` | Neon URL (same as local `Host=...` style or URI form). |
| `Jwt__Key` | Long random string (do **not** reuse dev secrets in production demos). |
| `Jwt__Issuer` | e.g. `Abhyanvaya` |
| `Jwt__Audience` | e.g. `AbhyanvayaUsers` |
| `UseRedis` | `false` (demo; no Redis needed). |
| `Cors__ReactOrigin` | Your UI origin(s), comma-separated: `https://your-app.pages.dev,http://localhost:5173` |
| `EnableSwagger` | `true` if you want `/swagger` on the demo API. |

Optional:

| Key | Notes |
|-----|--------|
| `PORT` | Render sets this automatically; the app listens on it. |

After deploy, note the API URL, e.g. `https://abhyanvaya-api.onrender.com`.

**Health check**: open `https://YOUR-API/swagger` if `EnableSwagger=true`, or any public GET you expose.

---

## 3. Frontend (Cloudflare Pages)

Cloudflare merged **Pages** into **Workers & Pages** — there is no separate “Pages” item in the sidebar anymore.

1. In the dashboard go to **Build → Compute → Workers & Pages**.
2. Click **Create application**.
3. Choose **Pages** / **Deploy a web app** / **Connect to Git** (static or framework)—**not** a bare Worker template that only runs **`npx wrangler deploy`**.
4. Connect your Git repo and pick the **Pages** flow. For **Framework preset**, choose **None** if **Vite** is not listed (**VitePress** is only for VitePress docs sites—not this React app). You will set build/output manually in the next steps.
5. **Project name**: must be **only** `a-z`, `0-9`, and `-` (e.g. `abhyanvaya-ui`). Names like `Abhyanvaya` are rejected and can block the form until fixed.
6. **Root directory**: `abhyanvaya-ui`
7. **Build command**: `npm ci && npm run build` (or `npm run build`)
8. **Build output directory**: `dist`

If fields feel “stuck”, fix the **project name** error first (red validation text). Wrong product type (Worker vs Pages) also shows **`npx wrangler deploy`** only—switch to a **Pages** setup with framework **None** and manual build/output (`npm run build`, `dist`).

### Build environment variable

| Key | Value |
|-----|--------|
| `VITE_API_BASE_URL` | `https://YOUR-API.onrender.com/api` |

Must end with `/api` (same as local axios default).

Redeploy the Pages build after changing the API URL.

---

## 4. Local testing against cloud API

Create `abhyanvaya-ui/.env.local`:

```
VITE_API_BASE_URL=https://YOUR-API.onrender.com/api
```

Then `npm run dev` — ensure `Cors__ReactOrigin` includes `http://localhost:5173`.

---

## 5. Security notes for demos

- Rotate `Jwt__Key` and DB passwords if the repo was ever public with real secrets.
- Prefer Neon + Render env vars over committing credentials (override `appsettings.json` with env vars on the host).
- Free tiers sleep; first request after idle may take ~30–60 seconds.

---

## Docker build (optional local check)

From the repo root (folder that contains `Dockerfile`):

```bash
docker build -t abhyanvaya-api .
docker run --rm -p 8080:8080 -e ConnectionStrings__DefaultConnection="..." -e Jwt__Key="..." -e UseRedis=false abhyanvaya-api
```

Then open `http://localhost:8080/swagger` if Swagger is enabled.
