# Proxxi - Local HTTP/HTTPS Proxy Tool

![Project Status](https://img.shields.io/badge/status-alpha-red) 
![Python Version](https://img.shields.io/badge/python-3.8+-blue)

**⚠️ Work in progress - Not ready for release! ⚠️**  

<img src="https://github.com/user-attachments/assets/9032b904-0604-4913-bda5-b8ba21bf0a34" width="400" alt="Proxxi Screenshot">     <img src="https://github.com/user-attachments/assets/d6fe4da8-deee-4a5f-bcd3-1dfabdbf5d51" width="400" alt="Proxxi Screenshot">

## What is Proxxi?

Proxxi is a local proxy server that lets you intercept and modify API responses. It's built on top of `mitmproxy` with a simple WinUI 3 interface.

Features

✔️ URL matching (exact or regex)

✔️ Static JSON response replacement

✔️ Python scripting for dynamic changes

✔️ HTTP status code matching

✔️ Toggle rules on/off

## ⚠️ Known Issues

### Installation
- **Python dependencies**: Auto-install is unstable and might not detect globally installed packages  

### Functional Limitations
- Only works with **JSON responses** (no HTML/plain text support)
- Cannot modify **HTTP headers**
- No **request type filtering** (GET/POST/PUT/etc.)
- **WebSockets** not supported
- Potential **memory leaks** during long sessions

## 🚀 Usage

1. **Configure your app** to use:
Address: 127.0.0.1
Port: 45871


2. **For HTTPS traffic**:
- Install mitmproxy certificate via Settings

3. **Add target rules**:
- Define URL patterns
- Set response modifications

4. **Enable proxy** in Settings tab

## 🔧 Proxifier Setup

For application-specific proxying (without system-wide changes):

1. **Add Proxy Server**
- Address: 127.0.0.1
- Port: 45871 
- Protocol: HTTPS

2. **Create Rule**
- Select your target application
- Assign to Proxxi proxy profile

3. **Enable Profile**
- Activate the rule
- Verify traffic appears in Proxxi console
