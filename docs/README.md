# Documentation

This directory contains deployment and configuration documentation for the EHR Integration Bridge project.

## Files

### [DEPLOYMENT.md](DEPLOYMENT.md)
Complete deployment guide covering:
- Architecture overview
- Server configuration
- URL routing and path handling
- Deployment procedures (automatic and manual)
- Troubleshooting common issues
- Security and maintenance

### [Caddyfile](Caddyfile)
Reference copy of the Caddy reverse proxy configuration.

**⚠️ Important:** This is for documentation only. The actual Caddyfile lives on proxy-vm at `/etc/caddy/Caddyfile` and must be updated manually.

## Quick Links

### Common Tasks

**Deploy application changes:**
```bash
# Automatic: Push to main branch (GitHub Actions handles it)
git push origin main

# Manual: SSH to ehrbridge-vm and run:
cd ~/EHR-Integration-Bridge
git pull origin main
sudo docker-compose down
sudo docker-compose up -d --build
Update Caddy configuration:

# SSH to proxy-vm
sudo nano /etc/caddy/Caddyfile
sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl reload caddy
View logs:

# Application logs (ehrbridge-vm)
sudo docker-compose logs -f

# Caddy logs (proxy-vm)
sudo journalctl -u caddy -f
Troubleshooting
OpenEMR not loading properly?

Check DEPLOYMENT.md → Troubleshooting section

Verify Caddyfile matches reference copy

Test URLs: /openemr/interface/login/login.php and /public/themes/style_light.css

API not responding?

Check container status: sudo docker ps

View API logs: sudo docker-compose logs ehrbridge_api

Test direct access: curl http://localhost:8080/swagger/v1/swagger.json

Architecture Diagram
┌─────────────────────────────────────────────────────────────┐
│                         Internet                             │
└────────────────────────────┬────────────────────────────────┘
                             │
                             │ HTTPS (443)
                             │
                    ┌────────▼────────┐
                    │   proxy-vm      │
                    │   (Caddy)       │
                    │                 │
                    │ SSL/TLS         │
                    │ Routing         │
                    │ HSTS Headers    │
                    └────────┬────────┘
                             │
                             │ Private Network
                             │ (10.0.0.224)
                             │
                    ┌────────▼────────┐
                    │  ehrbridge-vm   │
                    │                 │
                    │  ┌───────────┐  │
                    │  │ OpenEMR   │  │ :8081
                    │  │ Container │  │
                    │  └───────────┘  │
                    │                 │
                    │  ┌───────────┐  │
                    │  │ EhrBridge │  │ :8080
                    │  │ API       │  │
                    │  └───────────┘  │
                    │                 │
                    │  ┌───────────┐  │
                    │  │ MariaDB   │  │ :3306
                    │  └───────────┘  │
                    └─────────────────┘
Key Configuration Points
OpenEMR URL Patterns
OpenEMR 7.x generates two types of URLs:

Application routes: /openemr/interface/...

Caddy strips /openemr prefix before forwarding

Apache receives: /interface/...

Static assets: /public/themes/...

Caddy preserves full path

Apache receives: /public/themes/...

This dual pattern is why the Caddyfile has both handle_path and handle directives.

Critical Settings
docker-compose.yml:

OE_BASE_URL: /openemr  # Must NOT be empty or changed
Caddyfile:

handle_path /openemr/* { ... }  # Strips prefix
handle /public/* { ... }        # Preserves path
Additional Resources
OpenEMR Documentation

Caddy Documentation

Docker Compose Documentation

Project History & Architectural Rationale
This section captures key lessons from earlier development phases to prevent regressions and preserve design intent.

1. Final Workflow
The final asset is a self-contained .NET application that:

Creates a realistic OpenEMR environment with seeded data (Sync mode).

Performs a data quality audit and generates CSV reports (Extract mode).

Modes are controlled via WORKER_MODE in docker-compose.yml:

Sync: Seeds 1,000 realistic but intentionally messy patient records.

Extract: Connects to the local MariaDB, analyzes data, and generates audit CSVs.

Each run produces a complete “before and after” narrative suitable for demonstration or sales purposes.

2. Architectural Evolution
Phase	Description	Outcome
1. VirtualBox Instability	Initial attempt on local Ubuntu VM failed due to network and OS issues.	Migrated to GitHub Codespaces, enabling containerized stability.
2. FHIR API Integration	Pursued OpenEMR’s FHIR interface (multiple versions).	Abandoned — broken API client UI and phpGACL authorization issues.
3. Public HAPI FHIR Server	Tested external FHIR demo servers.	Technically functional but strategically poor (data mismatch, empty reports).
4. Direct Database Integration	Connected .NET app directly to MariaDB within Docker network.	Final architecture — deterministic, self-contained, and reproducible.
