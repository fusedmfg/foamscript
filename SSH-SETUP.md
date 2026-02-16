# SSH Key Authentication Setup

This guide will help you set up **passwordless SSH authentication** between your Windows machine and Ubuntu workstation. This is more secure than passwords and required for automated deployment.

---

## ✅ Benefits of SSH Keys

- 🔒 **More Secure** - No password stored anywhere
- ⚡ **Passwordless** - No typing password every time
- 🤖 **Automation-Friendly** - Scripts work without prompts
- 🏆 **Industry Standard** - Used by professionals worldwide

---

## 🚀 Quick Setup (5 Minutes)

### **Step 1: Check if You Already Have SSH Keys**

Open PowerShell and run:

```powershell
ls ~\.ssh\id_rsa.pub
```

**If you see a file:**
- ✅ You already have SSH keys! Skip to Step 3.

**If you see "cannot find path":**
- ➡️ Continue to Step 2 to generate keys.

---

### **Step 2: Generate SSH Keys (First Time Only)**

In PowerShell:

```powershell
# Generate SSH key pair (press Enter for all prompts to use defaults)
ssh-keygen -t rsa -b 4096

# Where to save? Press Enter (accepts default: C:\Users\YourName\.ssh\id_rsa)
# Passphrase? Press Enter twice (no passphrase for automation)
```

**Output:**
```
Generating public/private rsa key pair.
Your identification has been saved in C:\Users\YourName\.ssh\id_rsa
Your public key has been saved in C:\Users\YourName\.ssh\id_rsa.pub
```

✅ **Keys created!**

---

### **Step 3: Copy Public Key to Ubuntu**

Now we need to authorize your Windows machine on Ubuntu.

#### **Option A: Using `ssh-copy-id` (Easiest)**

```powershell
# Replace with your actual username and Ubuntu IP
ssh-copy-id your-username@192.168.1.100
```

It will ask for your Ubuntu password **one last time**, then you'll never need it again!

#### **Option B: Manual Method (if ssh-copy-id doesn't work)**

```powershell
# 1. Display your public key
cat ~\.ssh\id_rsa.pub

# 2. Copy the output (starts with "ssh-rsa AAAA...")

# 3. SSH into Ubuntu (last time you'll enter password!)
ssh your-username@192.168.1.100

# 4. On Ubuntu, add the key
mkdir -p ~/.ssh
echo "PASTE_YOUR_PUBLIC_KEY_HERE" >> ~/.ssh/authorized_keys
chmod 700 ~/.ssh
chmod 600 ~/.ssh/authorized_keys
exit
```

---

### **Step 4: Test Passwordless Login**

Back in PowerShell:

```powershell
# Try connecting - should NOT ask for password!
ssh your-username@192.168.1.100

# If it asks for password, something went wrong (see Troubleshooting below)
```

**Success?** You should be logged into Ubuntu without entering a password! 🎉

Type `exit` to return to Windows.

---

### **Step 5: Configure FoamScript Deployment**

Now edit your deployment config:

```powershell
cd c:\source\foamscript

# Copy the example config
cp deploy.config.example.ps1 deploy.config.ps1

# Edit with your details (Notepad or your favorite editor)
notepad deploy.config.ps1
```

**Edit these values:**
```powershell
$Config = @{
    UbuntuHost = "192.168.1.100"           # Your Ubuntu IP
    UbuntuUser = "yourname"                # Your username
    TargetDir = "~/foamscript"
    OpenFOAMBashrc = "/opt/openfoam2512/etc/bashrc"
}
```

Save and close.

---

### **Step 6: Test Automated Deployment!**

```powershell
# Deploy with automatic validation
.\deploy.ps1 -RunValidation
```

**No password prompt!** Everything should work automatically. 🚀

---

## 🔧 Troubleshooting

### "Still Asking for Password After Setup"

**Check 1: Verify public key is on Ubuntu**
```bash
# On Ubuntu
cat ~/.ssh/authorized_keys

# Should see your public key (starts with "ssh-rsa AAAA...")
```

**Check 2: Permissions on Ubuntu**
```bash
# On Ubuntu - fix permissions
chmod 700 ~/.ssh
chmod 600 ~/.ssh/authorized_keys
```

**Check 3: SSH server config on Ubuntu**
```bash
# On Ubuntu
sudo nano /etc/ssh/sshd_config

# Ensure these lines are set (remove # if commented):
PubkeyAuthentication yes
AuthorizedKeysFile .ssh/authorized_keys

# Save, then restart SSH
sudo systemctl restart ssh
```

---

### "Permission Denied (publickey)"

Your private key might not be in the default location. Tell SSH where it is:

```powershell
# Windows - specify key explicitly
ssh -i ~\.ssh\id_rsa your-username@192.168.1.100
```

Or create SSH config file (`~\.ssh\config`):
```
Host ubuntu-cfd
    HostName 192.168.1.100
    User your-username
    IdentityFile C:\Users\YourName\.ssh\id_rsa
```

Then connect with: `ssh ubuntu-cfd`

---

### "ssh-copy-id: command not found"

Windows 10/11 might not have this command. Use **Option B (Manual Method)** above instead.

---

## 🔒 Security Best Practices

✅ **DO:**
- Keep your private key (`id_rsa`) secret - never share it
- Use SSH keys instead of passwords for automation
- Back up your `.ssh` folder to a secure location

❌ **DON'T:**
- Commit `deploy.config.ps1` to GitHub (it's already in `.gitignore`)
- Share your private key with anyone
- Use the same SSH key for everything (you can generate separate keys per project)

---

## 📚 What Just Happened?

1. **SSH Key Pair Created:**
   - **Private Key** (`id_rsa`) - Stays on Windows (like your house key)
   - **Public Key** (`id_rsa.pub`) - Goes to Ubuntu (like a lock)

2. **Public Key Installed on Ubuntu:**
   - Added to `~/.ssh/authorized_keys`
   - Ubuntu now trusts connections from Windows

3. **Passwordless Authentication:**
   - Windows proves identity using private key
   - Ubuntu verifies using public key
   - No password needed!

---

## ✅ You're All Set!

Now you can:
- ✅ Deploy to Ubuntu with `.\deploy.ps1`
- ✅ No password prompts
- ✅ Fully automated workflow
- ✅ Secure credentials (never committed to Git)

Happy deploying! 🎯
