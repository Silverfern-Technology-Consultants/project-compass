# Governance Guardian

![Version](https://img.shields.io/github/v/release/Silverfern-Technology-Consultants/project-compass?include_prereleases&label=Version&color=gold&style=for-the-badge)
![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen?style=for-the-badge)
![Environment](https://img.shields.io/badge/Environment-Development-orange?style=for-the-badge)

A cloud infrastructure assessment and optimization platform for Managed Service Providers.

## Overview

Governance Guardian is a SaaS application designed to help MSPs analyze and improve their clients' Azure infrastructure governance. The platform provides automated assessments, recommendations, and reporting capabilities with client-specific customization.

## Project Status

🚧 **In Development** - Active development

## Version Information

![Backend](https://img.shields.io/badge/Backend-v2.2.1-blue?style=flat-square&logo=dotnet)
=======
![Backend](https://img.shields.io/badge/Backend-v2.2.0-blue?style=flat-square&logo=dotnet)
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
│   │   └── Services/
│   │       └── Identity/ # Modular identity analyzers
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
- Assessment orchestration with modular analyzers
- Naming convention analysis
- Tagging compliance analysis
- Client preference system
- Identity and Access Management (IAM) analysis
  - Enterprise application security assessment
  - Stale user and device detection
  - Resource IAM/RBAC analysis
  - Conditional access policy evaluation
- Microsoft Graph integration for enhanced security insights

### 🚧 In Development
- Enhanced client customization
- Advanced assessment models
- Security posture analysis
- Business continuity assessments

### 📋 Planned
- Network security analysis
- Backup & DR assessments
- Compliance framework templates
- Cost optimization analysis

## Assessment Models

### Resource Governance
- **Naming Convention**: Resource naming pattern analysis
- **Tagging**: Tag compliance and governance assessment
- **Governance Full**: Comprehensive governance assessment

### Identity & Access Management
- **Enterprise Applications**: App registration and service principal security
- **Stale Users & Devices**: Inactive account and device compliance detection
- **Resource IAM/RBAC**: Role assignment and permission analysis
- **Conditional Access**: Policy coverage and security gap assessment
- **Identity Full**: Complete IAM security assessment

### Business Continuity (Planned)
- **Backup Coverage**: Backup configuration and success analysis
- **Recovery Configuration**: Disaster recovery setup evaluation
- **Business Continuity Full**: Comprehensive BCDR assessment

### Security Posture (Planned)
- **Network Security**: Network configuration and control evaluation
- **Defender for Cloud**: Microsoft Defender security posture review
- **Security Full**: Complete security posture assessment

## API Documentation

### Health Check
```bash
GET /health
```

### Version Info
```bash
GET /api/version
```

### Assessment Types
```bash
GET /api/assessments/types
```

### Identity Assessment Status
```bash
GET /api/identity-assessment/status
```

## Recent Updates (v2.2.1)

- Implemented modular identity assessment architecture
- Enhanced service principal filtering to exclude Microsoft first-party services
- Improved conditional access analysis with report-only policy detection
- Better user account type detection for more accurate inactive user analysis
- Consolidated service registration and dependency injection improvements

## License

This project is proprietary software owned by Silverfern Technology Consultants. See [LICENSE](LICENSE) for details.

## Contact

For inquiries about this project, please contact Silverfern Technology Consultants.

---

[![Silverfern Technology Consultants](https://img.shields.io/badge/Built%20by-Silverfern%20Technology%20Consultants-gold?style=for-the-badge)](https://fernworks.io)

*This is a private development project. Commercial use is prohibited without explicit permission.*
