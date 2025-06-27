import React, { useMemo } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { ChevronRight, Home, Building2, Users, FileText, BarChart3, Shield, Settings, User } from 'lucide-react';
import { useClient } from '../../contexts/ClientContext';
import { useAuth } from '../../contexts/AuthContext';

const Breadcrumb = () => {
    const navigate = useNavigate();
    const location = useLocation();
    const { selectedClient, getClientDisplayName, isInternalSelected } = useClient();
    const { user } = useAuth();

    const getOrganizationName = () => {
        return user?.companyName || user?.CompanyName || user?.organizationName || user?.OrganizationName || 'My Company';
    };

    const generateBreadcrumbs = useMemo(() => {
        const path = location.pathname;
        const segments = path.split('/').filter(Boolean);
        const breadcrumbs = [];

        // Always start with organization
        breadcrumbs.push({
            label: getOrganizationName(),
            path: '/app/dashboard',
            icon: Building2,
            isClickable: true
        });

        // Parse the current route
        if (segments.length >= 1) {
            const section = segments[0]; // 'app'
            const page = segments[1]; // 'dashboard', 'company', 'settings', 'assessments', etc.
            const subPage = segments[2]; // 'clients', 'team', 'settings' (for company routes)

            switch (page) {
                case 'dashboard':
                    if (selectedClient) {
                        if (isInternalSelected?.()) {
                            breadcrumbs.push({
                                label: 'Internal Infrastructure',
                                path: '/app/dashboard',
                                icon: Home,
                                isClickable: false
                            });
                        } else {
                            breadcrumbs.push({
                                label: 'Clients',
                                path: '/app/company/clients',
                                icon: Building2,
                                isClickable: true
                            });
                            breadcrumbs.push({
                                label: `${getClientDisplayName?.() || 'Client'} Dashboard`,
                                path: '/app/dashboard',
                                icon: Building2,
                                isClickable: false
                            });
                        }
                    } else {
                        breadcrumbs.push({
                            label: 'Dashboard',
                            path: '/app/dashboard',
                            icon: Home,
                            isClickable: false
                        });
                    }
                    break;

                case 'assessments':
                    if (selectedClient) {
                        if (isInternalSelected?.()) {
                            breadcrumbs.push({
                                label: 'Internal Infrastructure',
                                path: '/app/dashboard',
                                icon: Home,
                                isClickable: true
                            });
                            breadcrumbs.push({
                                label: 'Assessments',
                                path: '/app/assessments',
                                icon: FileText,
                                isClickable: false
                            });
                        } else {
                            breadcrumbs.push({
                                label: 'Clients',
                                path: '/app/company/clients',
                                icon: Building2,
                                isClickable: true
                            });
                            breadcrumbs.push({
                                label: getClientDisplayName?.() || 'Client',
                                path: '/app/dashboard',
                                icon: Building2,
                                isClickable: true
                            });
                            breadcrumbs.push({
                                label: 'Assessments',
                                path: '/app/assessments',
                                icon: FileText,
                                isClickable: false
                            });
                        }
                    } else {
                        breadcrumbs.push({
                            label: 'Assessments',
                            path: '/app/assessments',
                            icon: FileText,
                            isClickable: false
                        });
                    }
                    break;

                case 'reports':
                    if (selectedClient) {
                        if (isInternalSelected()) {
                            breadcrumbs.push({
                                label: 'Internal Infrastructure',
                                path: '/app/dashboard',
                                icon: Home,
                                isClickable: true
                            });
                            breadcrumbs.push({
                                label: 'Reports',
                                path: '/app/reports',
                                icon: BarChart3,
                                isClickable: false
                            });
                        } else {
                            breadcrumbs.push({
                                label: 'Clients',
                                path: '/app/company/clients',
                                icon: Building2,
                                isClickable: true
                            });
                            breadcrumbs.push({
                                label: getClientDisplayName(),
                                path: '/app/dashboard',
                                icon: Building2,
                                isClickable: true
                            });
                            breadcrumbs.push({
                                label: 'Reports',
                                path: '/app/reports',
                                icon: BarChart3,
                                isClickable: false
                            });
                        }
                    } else {
                        breadcrumbs.push({
                            label: 'Reports',
                            path: '/app/reports',
                            icon: BarChart3,
                            isClickable: false
                        });
                    }
                    break;

                case 'compliance':
                    if (selectedClient) {
                        if (isInternalSelected()) {
                            breadcrumbs.push({
                                label: 'Internal Infrastructure',
                                path: '/app/dashboard',
                                icon: Home,
                                isClickable: true
                            });
                            breadcrumbs.push({
                                label: 'Compliance',
                                path: '/app/compliance',
                                icon: Shield,
                                isClickable: false
                            });
                        } else {
                            breadcrumbs.push({
                                label: 'Clients',
                                path: '/app/company/clients',
                                icon: Building2,
                                isClickable: true
                            });
                            breadcrumbs.push({
                                label: getClientDisplayName(),
                                path: '/app/dashboard',
                                icon: Building2,
                                isClickable: true
                            });
                            breadcrumbs.push({
                                label: 'Compliance',
                                path: '/app/compliance',
                                icon: Shield,
                                isClickable: false
                            });
                        }
                    } else {
                        breadcrumbs.push({
                            label: 'Compliance',
                            path: '/app/compliance',
                            icon: Shield,
                            isClickable: false
                        });
                    }
                    break;

                case 'company':
                    switch (subPage) {
                        case 'clients':
                            breadcrumbs.push({
                                label: 'My Clients',
                                path: '/app/company/clients',
                                icon: Building2,
                                isClickable: false
                            });
                            break;
                        case 'team':
                            breadcrumbs.push({
                                label: 'Team Management',
                                path: '/app/company/team',
                                icon: Users,
                                isClickable: false
                            });
                            break;
                        case 'settings':
                            breadcrumbs.push({
                                label: 'Company Settings',
                                path: '/app/company/settings',
                                icon: Settings,
                                isClickable: false
                            });
                            break;
                    }
                    break;

                case 'settings':
                    breadcrumbs.push({
                        label: 'My Settings',
                        path: '/app/settings',
                        icon: User,
                        isClickable: false
                    });
                    break;

                case 'profile':
                    breadcrumbs.push({
                        label: 'My Settings',
                        path: '/app/profile',
                        icon: User,
                        isClickable: false
                    });
                    break;

                default:
                    breadcrumbs.push({
                        label: page ? page.charAt(0).toUpperCase() + page.slice(1) : 'Dashboard',
                        path: location.pathname,
                        icon: Home,
                        isClickable: false
                    });
                    break;
            }
        }

        return breadcrumbs;
    }, [location.pathname, selectedClient, isInternalSelected, getClientDisplayName, getOrganizationName, user]);

    const breadcrumbs = generateBreadcrumbs;

    const handleBreadcrumbClick = (breadcrumb) => {
        if (breadcrumb.isClickable) {
            navigate(breadcrumb.path);
        }
    };

    if (breadcrumbs.length <= 1) {
        return null; // Don't show breadcrumbs if there's only the organization
    }

    return (
        <nav className="flex items-center space-x-2 text-sm text-gray-400 mb-6">
            {breadcrumbs.map((breadcrumb, index) => (
                <React.Fragment key={index}>
                    <div className="flex items-center space-x-2">
                        <breadcrumb.icon size={14} />
                        <button
                            onClick={() => handleBreadcrumbClick(breadcrumb)}
                            className={`${breadcrumb.isClickable
                                    ? 'hover:text-yellow-400 cursor-pointer transition-colors'
                                    : 'text-white cursor-default'
                                } ${index === breadcrumbs.length - 1 ? 'font-medium' : ''}`}
                            disabled={!breadcrumb.isClickable}
                        >
                            {breadcrumb.label}
                        </button>
                    </div>
                    {index < breadcrumbs.length - 1 && (
                        <ChevronRight size={14} className="text-gray-600" />
                    )}
                </React.Fragment>
            ))}
        </nav>
    );
};

export default Breadcrumb;