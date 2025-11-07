# EHR Integration Bridge - Deployment Guide

## Architecture Overview

```
Internet
   ↓
Caddy Reverse Proxy (proxy-vm)
   ↓
Private Network (10.0.0.224)
   ↓
   ├─→ EhrBridge API (ehrbridge-vm:8080)
   └─→ OpenEMR (ehrbridge-vm:8081)
```

## Infrastructure

### Servers

1. **proxy-vm** (Public-facing)
   - Role: Reverse proxy
   - Software: Caddy
   - Handles: SSL/TLS, routing, HSTS headers
   - Config: `/etc/caddy/Caddyfile`

2. **ehrbridge-vm** (Private network: 10.0.0.224)
   - Role: Application server
   - Software: Docker, Docker Compose
   - Runs: OpenEMR container (port 8081), EhrBridge API container (port 8080)
   - Config: `~/EHR-Integration-Bridge/docker-compose.yml`

### Domains

- `ehrbridge.kepekepe.com` - Main entry point
- `ehrbridgeapi.kepekepe.com` - API and OpenEMR access

Both domains point to proxy-vm's public IP.

## URL Routing

### External URLs → Internal Routing

| External URL | Caddy Handler | Forwarded To | Backend |
|--------------|---------------|--------------|---------|
| `/api/*` | `handle` | `10.0.0.224:8080` | EhrBridge API |
| `/openemr/*` | `handle_path` (strips prefix) | `10.0.0.224:8081` | OpenEMR |
| `/public/*` | `handle` (preserves path) | `10.0.0.224:8081` | OpenEMR static assets |
| `/assets/*` | `handle` (preserves path) | `10.0.0.224:8081` | OpenEMR static assets |

### Why Different Handlers?

**OpenEMR 7.x has a dual URL pattern:**

1. **Application routes** (PHP pages):
   - Generated as: `/openemr/interface/login/login.php`
   - Caddy strips `/openemr` → forwards `/interface/login/login.php`
   - Apache serves from: `/var/www/localhost/htdocs/openemr/interface/login/login.php`

2. **Static assets** (CSS, JS, fonts):
   - Generated as: `/public/themes/style.css` (no `/openemr` prefix)
   - Caddy preserves path → forwards `/public/themes/style.css`
   - Apache serves from: `/var/www/localhost/htdocs/openemr/public/themes/style.css`

**Key insight:** Apache's DocumentRoot is `/var/www/localhost/htdocs/openemr`, so it expects paths WITHOUT the `/openemr` prefix.

## Deployment Process

### Automatic Deployment (GitHub Actions)

**Trigger:** Push to `main` branch

**What happens:**
1. GitHub Actions connects to ehrbridge-vm via SSH
2. Pulls latest code from repository
3. Runs `docker-compose down` to stop containers
4. Runs `docker-compose up -d --build` to rebuild and start containers
5. Verifies backend health via Swagger endpoint

**What's NOT deployed:**
- Caddyfile on proxy-vm (manual management only)
- Database data (preserved in Docker volumes)
- OpenEMR configuration (preserved in Docker volumes)

### Manual Deployment Steps

#### Deploying Application Changes (ehrbridge-vm)

```bash
# SSH to ehrbridge-vm
ssh ubuntu@ehrbridge-vm

# Navigate to project
cd ~/EHR-Integration-Bridge

# Pull latest changes
git pull origin main

# Rebuild and restart containers
sudo docker-compose down
sudo docker-compose up -d --build

# Verify containers are running
sudo docker ps

# Check logs if needed
sudo docker-compose logs -f
```

#### Updating Caddy Configuration (proxy-vm)

```bash
# SSH to proxy-vm
ssh ubuntu@proxy-vm

# Backup current config
sudo cp /etc/caddy/Caddyfile /etc/caddy/Caddyfile.backup-$(date +%Y%m%d-%H%M%S)

# Edit configuration
sudo nano /etc/caddy/Caddyfile

# Validate syntax
sudo caddy validate --config /etc/caddy/Caddyfile

# Reload (zero-downtime)
sudo systemctl reload caddy

# Check status
sudo systemctl status caddy

# View logs if needed
sudo journalctl -u caddy -f
```

## Configuration Files

### docker-compose.yml (ehrbridge-vm)

**Location:** `~/EHR-Integration-Bridge/docker-compose.yml`

**Key settings:**
```yaml
openemr:
  environment:
    OE_BASE_URL: /openemr  # Must match external URL path
  ports:
    - "8081:80"  # OpenEMR on port 8081
```

**⚠️ Important:** Do NOT change `OE_BASE_URL` to empty string or remove `/openemr`. This must match the external URL structure.

### Caddyfile (proxy-vm)

**Location:** `/etc/caddy/Caddyfile`

**Reference copy:** `docs/Caddyfile` (in this repository)

**⚠️ Important:** The reference copy is for documentation only. Changes must be applied manually on proxy-vm.

## Troubleshooting

### OpenEMR CSS Not Loading

**Symptoms:** Login page loads but appears unstyled (raw HTML)

**Check:**
```bash
# Test CSS file
curl -I https://ehrbridgeapi.kepekepe.com/public/themes/style_light.css

# Should return: HTTP/2 200
```

**Common causes:**
1. Caddyfile missing `/public/*` handler
2. Caddyfile using `handle` instead of `handle_path` for `/openemr/*`
3. `OE_BASE_URL` set incorrectly in docker-compose.yml

**Fix:** Verify Caddyfile matches `docs/Caddyfile` reference.

### OpenEMR Login Page 404

**Symptoms:** `https://ehrbridgeapi.kepekepe.com/openemr/interface/login/login.php` returns 404

**Check:**
```bash
# Test inside container
ssh ubuntu@ehrbridge-vm
docker exec openemr curl -I http://localhost/interface/login/login.php

# Should return: HTTP/1.1 200 OK
```

**Common causes:**
1. Caddyfile using `handle` instead of `handle_path` for `/openemr/*`
2. OpenEMR container not running
3. Apache misconfigured inside container

**Fix:** 
- Verify Caddyfile uses `handle_path /openemr/*`
- Check container status: `sudo docker ps`

### API Not Responding

**Symptoms:** API endpoints return 502 or timeout

**Check:**
```bash
# Test API directly on ehrbridge-vm
ssh ubuntu@ehrbridge-vm
curl -I http://localhost:8080/swagger/v1/swagger.json

# Should return: HTTP/1.1 200 OK
```

**Common causes:**
1. EhrBridge API container crashed
2. Port 8080 not accessible
3. Database connection issues

**Fix:**
```bash
# Check container logs
sudo docker-compose logs ehrbridge_api

# Restart if needed
sudo docker-compose restart ehrbridge_api
```

## Security Notes

### Secrets Management

**Never commit these to the repository:**
- Database passwords (use environment variables or secrets)
- TLS certificates (managed by Caddy automatically)
- SSH keys (stored in GitHub Secrets)

**Current secrets in GitHub Actions:**
- `VM_HOST` - ehrbridge-vm IP address
- `VM_USER` - SSH username
- `VM_KEY` - SSH private key

### Network Security

- proxy-vm is public-facing (ports 80, 443 open)
- ehrbridge-vm is on private network (10.0.0.224)
- Only proxy-vm can reach ehrbridge-vm
- Database (MariaDB) is only accessible within Docker network

### HSTS Configuration

Caddy enforces HTTPS with:
```
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
```

This means browsers will ONLY connect via HTTPS for 1 year after first visit.

## Maintenance

### Regular Tasks

**Weekly:**
- Check disk space: `df -h`
- Review logs for errors: `sudo journalctl -u caddy --since "1 week ago"`
- Verify backups are running

**Monthly:**
- Update system packages: `sudo apt update && sudo apt upgrade`
- Review Docker image updates
- Test disaster recovery procedures

**Quarterly:**
- Review and rotate secrets
- Audit access logs
- Update documentation

### Backup Strategy

**What to backup:**
1. OpenEMR data: Docker volume `openemr_sites`
2. Database: Docker volume `mariadb_data`
3. Caddyfile: `/etc/caddy/Caddyfile`
4. Application code: Git repository (already backed up)

**Backup commands:**
```bash
# Backup OpenEMR sites volume
sudo docker run --rm -v openemr_sites:/data -v $(pwd):/backup ubuntu tar czf /backup/openemr_sites_$(date +%Y%m%d).tar.gz /data

# Backup database volume
sudo docker run --rm -v mariadb_data:/data -v $(pwd):/backup ubuntu tar czf /backup/mariadb_data_$(date +%Y%m%d).tar.gz /data

# Backup Caddyfile
sudo cp /etc/caddy/Caddyfile ~/backups/Caddyfile_$(date +%Y%m%d)
```

## Version History

- **2025-11-07:** Fixed OpenEMR CSS rendering issue by configuring dual path handlers in Caddy
- **Initial deployment:** Set up reverse proxy architecture with Caddy and Docker Compose

## Support

For issues or questions:
1. Check this documentation first
2. Review container logs: `sudo docker-compose logs`
3. Check Caddy logs: `sudo journalctl -u caddy -f`
4. Refer to diagnostic report in repository
