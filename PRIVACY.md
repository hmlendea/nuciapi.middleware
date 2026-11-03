# Privacy and Personal Data

This document describes how NuciAPI.Middleware handles personal data. NuciAPI.Middleware is a NuGet library providing ASP.NET Core middleware base utilities; it does not operate as a service and does not collect, process, or transmit personal data on its own. Applications that reference this library control all data handling.

## 📑 Table of Contents

- [What This Document Covers](#-what-this-document-covers)
- [Self-Hosted Deployments](#-self-hosted-deployments)
- [Data We Handle](#-data-we-handle)
- [Processing and Use](#-processing-and-use)
- [Storage, Retention, and Deletion](#-storage-retention-and-deletion)
- [External Processing and Integrations](#-external-processing-and-integrations)
- [Data Protection and Security](#-data-protection-and-security)
- [Document Changes](#-document-changes)
- [Contact](#-contact)

## 🔎 What This Document Covers

This document describes how NuciAPI.Middleware at https://github.com/hmlendea/nuciapi.middleware handles personal data. It covers the library behaviour and verified integrations described below. Where the software is self-hosted, the instance operator may have separate responsibilities described below.

## 🏠 Self-Hosted Deployments

NuciAPI.Middleware is a library consumed by applications. The application developer (instance operator) controls all configuration, local storage, logs, backups, access controls, retention, and request handling. This library does not send any data to project maintainers or external services. No telemetry, update checks, crash reports, email, authentication, reverse-proxy, object-storage, or monitoring integrations are built into the library.

## 📥 Data We Handle

### Data Provided to the Application

No personal data is requested by this library. Applications using the library may request headers such as `X-Client-ID`, `X-HMAC`, `X-Request-ID`, `X-Timestamp`, and client IP addresses; handling of those values is entirely controlled by the consuming application.

### Data Generated or Collected by the Application

No personal data is generated or collected automatically by this library. The library provides helper methods to read headers and extract client IP addresses from forwarding headers (`X-Forwarded-For`, `Forwarded`, `X-Real-IP`, `CF-Connecting-IP`, `True-Client-IP`) with a fallback to `HttpContext.Connection.RemoteIpAddress`; any logging or storage of those values is performed by the consuming application.

### Data Received from Integrations

No personal data is received from integrations or third parties by this library.

## 🧭 Processing and Use

The library processes the data described above for these verified functions:
- Header access helpers — reads request headers provided by the client or infrastructure
- Client IP extraction — parses forwarding headers and falls back to the remote connection address

## 🗄️ Storage, Retention, and Deletion

This library does not store any data. All storage, retention, and deletion behaviour is determined by the consuming application. For self-hosted deployments, the instance operator controls storage, deletion, and backups.

## 🔗 External Processing and Integrations

The application has no built-in external data transfer. No external services, recipients, or integrations process or receive data from this library.

| Service or integration | Purpose | Data involved | Configuration or documentation |
|-----------------------|---------|---------------|--------------------------------|
| None | N/A | N/A | N/A |

## 🛡️ Data Protection and Security

This library provides no data protection or security controls beyond the header and IP helpers described above. The consuming application is responsible for all security measures, including input validation, access controls, encryption, and secure configuration. For self-hosted deployments, the instance operator is responsible for updates, secrets management, access controls, backups, network exposure, and log protection. No absolute security is promised.

The original maintainers are not responsible for changes, modifications, or privacy issues introduced by downstream forks.

## 🔄 Document Changes

Update this document when library data flows, storage, integrations, or deployment responsibilities change. The current version is published at https://github.com/hmlendea/nuciapi.middleware/blob/main/PRIVACY.md.

## 📬 Contact

For questions about library data handling, contact the project maintainers via GitHub issues at https://github.com/hmlendea/nuciapi.middleware/issues. For a self-hosted application using this library, contact the application operator. Include the application name and deployment context if applicable; do not send passwords, access tokens, or other secrets.