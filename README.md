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

![Backend](https://img.shields.io/badge/Backend-v2.3.2-blue?style=flat-square&logo=dotnet)
![Frontend](https://img.shields.io/badge/Frontend-v2.3.2-blue?style=flat-square&logo=react)
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
├── docs/                          # Project documentation
├── frontend/                      # React web application
│   └── src/
│       ├── components/
│       │   ├── assessment/        # Assessment-related components
│       │   ├── client/            # Client management components
│       │   └── common/            # Shared UI components
│       ├── pages/                 # Main application pages
│       ├── services/              # API service layer
│       └── context/               # React context providers
├── backend/                       # .NET Core solution
│   ├── Compass.Api/               # Web API layer
│   │   ├── Controllers/           # API endpoints
│   │   ├── Services/              # Application services
│   │   └── Middleware/            # Custom middleware
│   ├── Compass.Core/              # Business logic layer
│   │   ├── Models/                # Data transfer objects
│   │   │   ├── Assessment/        # Organized assessment models
│   │   ├── Services/              # Core business logic
│   │   │   ├── Naming/            # Naming convention services
│   │   └── Interfaces/            # Service contracts
│   └── Compass.Data/              # Data access layer
│       ├── Entities/              # Database models
│       ├── Repositories/          # Data access implementations
│       └── Interfaces/            # Repository contracts
├── infrastructure/                # Azure Bicep templates
├── database/                     # Database scripts and migrations
└── Directory.Build.props         # Centralized versioning
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

# Current Features

## ✅ Implemented
- **Multi-tenant MSP architecture** with organization-scoped data isolation
- **OAuth delegation system** for secure Azure environment access with ARM and Graph API integration
- **JWT authentication with MFA** including TOTP and backup codes via Microsoft Graph
- **Comprehensive client management system** with access control and team collaboration
- **Modular assessment orchestration** with specialized analyzer components
- **Advanced naming convention analysis** with client preference-aware validation
- **Tagging compliance analysis** with customizable governance rules
- **Client preference system** with JSON-based naming scheme configuration
- **Complete Identity and Access Management (IAM) analysis**:
  - Enterprise application security assessment with app registration review
  - Stale user and device detection with compliance reporting
  - Resource IAM/RBAC analysis with role assignment evaluation
  - Conditional access policy coverage and security gap assessment
- **Microsoft Graph integration** for enhanced security insights and email services
- **Categorized assessment workflow** with 4 specialized assessment categories
- **Premium branded login experience** with atmospheric animations and corporate identity

## 🚧 In Development
- **Security posture analysis** with network configuration evaluation
- **Business continuity assessments** with backup and DR analysis
- **Enhanced client customization** with advanced preference configurations
- **Assessment result analytics** with trend analysis and reporting

## 📋 Planned
- **Compliance framework templates** (SOC 2, ISO 27001, NIST)
- **Cost optimization analysis** with resource rightsizing recommendations
- **Advanced reporting system** with executive dashboards
- **API integration marketplace** for third-party assessment tools

# Assessment Models

## Resource Governance
- **Naming Convention Analysis**: Resource naming pattern compliance with client-specific scheme validation
- **Tagging Compliance**: Tag governance assessment with customizable policy enforcement
- **Governance Full**: Comprehensive resource governance assessment combining naming and tagging analysis

## Identity & Access Management
- **Enterprise Applications**: App registration security, service principal analysis, and permission review
- **Stale Users & Devices**: Inactive account detection, device compliance monitoring, and cleanup recommendations
- **Resource IAM/RBAC**: Role assignment analysis, permission evaluation, and access pattern review
- **Conditional Access**: Policy coverage assessment, security gap identification, and compliance evaluation
- **Identity Full**: Complete IAM security assessment with comprehensive access governance review

## Security Posture
- **Network Security**: Network configuration analysis, security group evaluation, and traffic flow assessment
- **Security Configuration**: Microsoft Defender for Cloud integration with security posture scoring
- **Security Full**: Complete security posture assessment with network and configuration analysis

## Business Continuity & Disaster Recovery
- **Backup Coverage**: Backup configuration analysis, success rate monitoring, and coverage gap identification
- **Recovery Configuration**: Disaster recovery setup evaluation, RTO/RPO analysis, and failover testing assessment
- **BCDR Full**: Comprehensive business continuity assessment with backup and recovery analysis

## Assessment Architecture

### Modular Component Design
- **Specialized Assessment Modals**: 4 dedicated modal components for different assessment categories
- **Smart Assessment Dropdown**: Intelligent positioning with multi-selection support
- **Client-Aware Workflows**: Preference integration across all assessment types
- **Real-Time Processing**: Background orchestration with live status updates

### Technical Implementation
- **11 Total Assessment Types** across 4 main categories
- **Client Preference Integration**: JSON-based naming scheme configuration
- **Service Abbreviation Support**: 100+ service abbreviation mappings
- **Organization-Scoped Security**: Multi-tenant data isolation with proper access controls

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

## License

This project is proprietary software owned by Silverfern Technology Consultants. See [LICENSE](LICENSE) for details.

## Contact

For inquiries about this project, please contact Silverfern Technology Consultants.

---

[![Silverfern Technology Consultants](https://img.shields.io/badge/Built%20by-Silverfern%20Technology%20Consultants-gold?style=for-the-badge)](https://fernworks.io)

*This is a private development project. Commercial use is prohibited without explicit permission.*
