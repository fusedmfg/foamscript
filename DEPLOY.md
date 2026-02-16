# FoamScript Deployment Guide

## Quick Start

### 1. Configure Your Ubuntu Connection

Edit `deploy.config.ps1` and set your Ubuntu workstation details:

```powershell
$Config = @{
    UbuntuHost = "192.168.1.100"           # Your Ubuntu IP or hostname
    UbuntuUser = "yourname"                # Your Ubuntu username
    TargetDir = "~/foamscript"             # Where to deploy on Ubuntu
    OpenFOAMBashrc = "/opt/openfoam2512/etc/bashrc"
}
```

### 2. Ensure SSH is Set Up

The deployment script uses SSH/SCP. Make sure you can connect:

```powershell
ssh yourname@192.168.1.100
```

**First time?** Set up SSH key authentication (optional but recommended):
```powershell
# Generate SSH key (if you don't have one)
ssh-keygen -t rsa

# Copy to Ubuntu
ssh-copy-id yourname@192.168.1.100
```

### 3. Deploy!

From PowerShell in `c:\source\foamscript\`:

```powershell
# Standard deployment (runs tests, deploys, builds on Ubuntu)
.\deploy.ps1

# Deploy and automatically run validation
.\deploy.ps1 -RunValidation

# Skip tests (faster iteration during development)
.\deploy.ps1 -SkipTests

# Just create package, don't deploy
.\deploy.ps1 -BuildOnly
```

---

## Deployment Workflow

The script performs these steps:

1. **Run Tests** (unless `-SkipTests`) - Ensures code quality
2. **Clean Build Artifacts** - Removes old binaries
3. **Create Deployment Package** - Copies source files (excludes bin/obj/tests)
4. **Deploy to Ubuntu** - Uses SCP to copy files
5. **Build on Ubuntu** - Runs `dotnet build` remotely

---

## Common Scenarios

### During Active Development
```powershell
# Fast iteration - skip tests
.\deploy.ps1 -SkipTests
```

### Before Committing Code
```powershell
# Full validation with tests
.\deploy.ps1 -RunValidation
```

### Testing OpenFOAM Integration
```powershell
# Deploy and run validation automatically
.\deploy.ps1 -RunValidation
```

---

## Troubleshooting

### "SCP not found"
Install OpenSSH Client on Windows:
1. Settings → Apps → Optional Features
2. Add a Feature → OpenSSH Client → Install

### "Failed to connect to Ubuntu"
Check:
- Ubuntu is powered on and network-accessible
- Hostname/IP is correct in `deploy.config.ps1`
- Username is correct
- SSH is enabled on Ubuntu: `sudo systemctl status ssh`

### "Build failed on Ubuntu"
SSH into Ubuntu and check:
```bash
ssh yourname@ubuntu-host
cd ~/foamscript
dotnet build -c Release
```

Check if .NET is installed: `dotnet --version`

---

## Manual Deployment (Alternative)

If you prefer manual deployment:

1. **Use WinSCP** to copy files from `c:\source\foamscript\` to Ubuntu `~/foamscript/`
2. **Use PuTTY** to SSH into Ubuntu
3. **Build manually:**
   ```bash
   cd ~/foamscript
   dotnet build -c Release
   ```

---

## Next Steps

After successful deployment, SSH into Ubuntu and run:

```bash
# Source OpenFOAM environment
source /opt/openfoam2512/etc/bashrc

# Run validation
cd ~/foamscript
dotnet run -c Release -- validate --verbose
```

Expected output:
```
✓ OpenFOAM Version: v2512 detected
✓ All required tools found
✓ Summary: All checks passed. FoamScript is ready to use.
```
