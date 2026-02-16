# FoamScript Deployment Configuration (EXAMPLE)
# Copy this file to 'deploy.config.ps1' and edit with your actual values
#
# IMPORTANT: deploy.config.ps1 is in .gitignore and will NOT be committed
# This keeps your credentials safe!

$Config = @{
    # Ubuntu workstation hostname or IP address
    # RECOMMENDED: Use hostname instead of IP (won't break if DHCP changes IP)
    # Examples: "fusiondiscgolf", "ubuntu-cfd", "ubuntu-cfd.local"
    # Note: Hostname must be resolvable on your network
    UbuntuHost = "your-ubuntu-host"

    # Ubuntu username
    UbuntuUser = "your-username"

    # Target directory on Ubuntu (will be created if it doesn't exist)
    TargetDir = "~/foamscript"

    # OpenFOAM bashrc path (adjust version number and path if different)
    # Common paths: /usr/lib/openfoam/openfoam2512/etc/bashrc, /opt/openfoam2512/etc/bashrc
    # Note: The validate command will auto-detect installations to help you find the correct path
    OpenFOAMBashrc = "/usr/lib/openfoam/openfoam2512/etc/bashrc"
}

# Return the configuration
return $Config
