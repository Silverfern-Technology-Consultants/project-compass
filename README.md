# Governance Guardian

![Version](https://img.shields.io/github/v/release/SilverfernTech/project-compass?label=Version&color=gold&style=for-the-badge)
![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen?style=for-the-badge)
![Environment](https://img.shields.io/badge/Environment-Development-orange?style=for-the-badge)

A cloud infrastructure assessment and optimization platform for Managed Service Providers.

## Overview

Governance Guardian is a SaaS application designed to help MSPs analyze and improve their clients' Azure infrastructure governance. The platform provides automated assessments, recommendations, and reporting capabilities with client-specific customization.

## Project Status

🚧 **In Development** - Active development

## Version Information

![Backend](https://img.shields.io/badge/Backend-v2.1.0-blue?style=flat-square&logo=dotnet)
![Frontend](https://img.shields.io/badge/Frontend-v1.0.0-blue?style=flat-square&logo=react)
![API](https://img.shields.io/badge/API-Live-green?style=flat-square)

## Technology Stack

- **Frontend**: React 18 with TypeScript
- **Backend**: .NET Core 8 Web API
- **Database**: Azure SQL Database
- **Infrastructure**: Azure PaaS services
- **Authentication**: JWT with MFA support
- **OAuth**: Azure AD delegation for MSP environments

## Architecture

```
┌─────────────────────┐    ┌──────────────────────┐    ┌─────────────────────┐
│   MSP Azure         │    │   Governance         │    │   MSP Portal        │
│   Environments      │───▶│   Guardian API       │───▶│   Dashboard         │
│                     │    │                      │    │                     │
│ • Client Resources  │    │ • Assessment Engine  │    │ • Client Management │
│ • OAuth Tokens      │    │ • Client Preferences │    │ • Custom Reports    │
│ • Compliance Data   │    │ • Multi-Tenant Data  │    │ • Recommendations   │
└─────────────────────┘    └──────────────────────┘    └─────────────────────┘
```

## Project Structure
```
project-compass/
├── docs/                 # Project documentation
├── frontend/             # React web application
│   └── package.json      # Frontend dependencies
├── backend/              # .NET Core solution
│   ├── Compass.Api/      # Web API layer
│   ├── Compass.Core/     # Business logic
│   └── Compass.Data/     # Data access
├── infrastructure/       # Azure Bicep templates
├── database/            # Database scripts and migrations
└── Directory.Build.props # Centralized versioning
```

## Development Setup

### Prerequisites
- .NET 8 SDK
- Node.js 18+
- Azure CLI (for development)
- SQL Server (local or Azure)

### Backend Setup
```bash
cd backend
dotnet restore
dotnet run --project Compass.Api
```

### Frontend Setup
```bash
cd frontend
npm install
npm start
```

## Current Features

### ✅ Implemented
- Multi-tenant MSP architecture
- OAuth delegation for Azure environments
- JWT authentication with MFA
- Client management system
- Assessment orchestration
- Naming convention analysis
- Tagging compliance analysis
- Client preference system

### 🚧 In Development
- Enhanced client customization
- Advanced assessment models
- Security posture analysis

### 📋 Planned
- RBAC/IAM analysis
- Network security analysis
- Backup & DR assessments
- Compliance framework templates

## API Documentation

### Health Check
```bash
GET /health
```

### Version Info
```bash
GET /api/version
```

## License

This project is proprietary software owned by Silverfern Technology Consultants. See [LICENSE](LICENSE) for details.

## Contact

For inquiries about this project, please contact Silverfern Technology Consultants.

---

[![Silverfern Technology Consultants](https://img.shields.io/badge/Built%20by-Silverfern%20Technology%20Consultants-gold?style=for-the-badge)](https://fernworks.io)

*This is a private development project. Commercial use is prohibited without explicit permission.*