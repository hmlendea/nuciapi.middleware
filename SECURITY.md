# Security Policy

This document describes the security policy for NuciAPI.Middleware, a NuGet library providing ASP.NET Core middleware base utilities. The policy covers vulnerability reporting, supported versions, and coordinated disclosure expectations.

## 📑 Table of Contents

- [Supported Versions](#-supported-versions)
- [Reporting a Vulnerability](#-reporting-a-vulnerability)
- [Scope](#-scope)
- [Disclosure Policy](#-disclosure-policy)
- [Automated Security Scanning](#-automated-security-scanning)
- [Safe Harbour](#-safe-harbour)
- [Recognition](#-recognition)

## 🛡️ Supported Versions

Use this table to indicate which project versions currently receive security maintenance.

| Version | Distribution Channel | Supported |
|---------|--------------------|-----------|
| Latest version | NuGet.org | ✅ |
| Latest version | GitHub Releases | ✅ |
| Preceding versions | Any distribution channel | ❌ |

## 🚨 Reporting a Vulnerability

Please do not disclose suspected vulnerabilities publicly before maintainers have had an opportunity to validate and remediate them.

To report a vulnerability:
- [GitHub Security Advisories](https://github.com/hmlendea/nuciapi.middleware/security/advisories)
- Contact the maintainers directly

## 📌 Scope

The original maintainers are not responsible for changes, modifications, or security issues introduced by downstream forks.

The subsequent report categories are in scope for this repository:
- Vulnerabilities in the NuciAPI.Middleware library code
- Vulnerabilities in the build and release pipeline
- Vulnerabilities in declared dependencies

The subsequent categories are out of scope unless explicitly stated to the contrary:
- Vulnerabilities in applications consuming this library
- Vulnerabilities in downstream forks or modifications
- Vulnerabilities in transitive dependencies not directly declared
- Social engineering or physical attacks

## 📢 Disclosure Policy

This project follows coordinated disclosure:
1. Vulnerabilities are investigated privately.
2. A remediation plan is prepared and validated.
3. Public disclosure is published after a fix, mitigation, or agreed risk decision is available.
4. Credit is attributed in accordance with reporter preference and project policy.

## 🔍 Automated Security Scanning

This repository runs automated security scans on every push, pull request, and weekly schedule. The scan includes:
- **Vulnerable package detection** Checks all direct and transitive NuGet dependencies for known vulnerabilities using `dotnet list package --vulnerable --include-transitive`
- **CodeQL static analysis** Performs semantic code analysis for C# to detect security anti-patterns, injection flaws, and other code-level vulnerabilities

Results are published to the [Security](https://github.com/hmlendea/nuciapi.middleware/security) tab and the [Actions](https://github.com/hmlendea/nuciapi.middleware/actions/workflows/security.yml) workflow page.

## 🧾 Safe Harbour

If your research is conducted in good faith, confined to authorised scope, and disclosed responsibly, the maintainers will not pursue action for policy-compliant activity.

## 🙏 Recognition

We appreciate responsible disclosure. Reporters who desire public attribution may be acknowledged in release notes, advisories, or a dedicated acknowledgements section.
