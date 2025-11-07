# OpenEMR CSS Rendering Fix - Summary

**Date:** 2025-11-07  
**Issue:** OpenEMR login page loaded as unstyled HTML (CSS files returned 404)  
**Status:** ✅ RESOLVED

## What Was Wrong

OpenEMR 7.x generates two different URL patterns:
1. **Application routes:** `/openemr/interface/...` (with prefix)
2. **Static assets:** `/public/themes/...` (without prefix)

The original Caddyfile only handled `/openemr/*` routes, so CSS files at `/public/*` returned 404.

## The Fix

Updated Caddyfile on **proxy-vm** to handle both patterns:

```caddy
# Application routes - strip /openemr prefix
handle_path /openemr/* {
    reverse_proxy 10.0.0.224:8081
}

# Static assets - preserve path
handle /public/* {
    reverse_proxy 10.0.0.224:8081
}

handle /assets/* {
    reverse_proxy 10.0.0.224:8081
}
```

## Configuration Files

### ✅ Correct Settings

**docker-compose.yml (ehrbridge-vm):**
```yaml
OE_BASE_URL: /openemr  # ⚠️ Do NOT change this
```

**Caddyfile (proxy-vm):**
- See `docs/Caddyfile` for reference copy
- Actual file: `/etc/caddy/Caddyfile` on proxy-vm

## Verification

Test both URL types:
```bash
# Application route (should return 200)
curl -I https://ehrbridgeapi.kepekepe.com/openemr/interface/login/login.php

# Static asset (should return 200)
curl -I https://ehrbridgeapi.kepekepe.com/public/themes/style_light.css
```

## Important Notes

1. **Never change `OE_BASE_URL`** in docker-compose.yml - it must be `/openemr`
2. **Caddyfile is NOT auto-deployed** - changes must be applied manually on proxy-vm
3. **Use `handle_path` for `/openemr/*`** - this strips the prefix before forwarding
4. **Use `handle` for `/public/*`** - this preserves the full path

## Related Documentation

- [DEPLOYMENT.md](DEPLOYMENT.md) - Full deployment guide
- [Caddyfile](Caddyfile) - Reference configuration
- [README.md](README.md) - Documentation index

## Future Deployments

When deploying updates:
- ✅ Application code changes: Automatic via GitHub Actions
- ✅ docker-compose.yml changes: Automatic via GitHub Actions
- ❌ Caddyfile changes: Manual only (SSH to proxy-vm)

The fix is permanent and won't be overwritten by deployments.
