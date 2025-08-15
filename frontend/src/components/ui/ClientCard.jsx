import React, { useState } from 'react';
import {
    Building2,
    Edit3,
    Cloud,
    MoreVertical,
    Mail,
    Phone,
    MapPin,
    Activity,
    Database,
    Shield,
    Eye,
    Trash2,
    Sliders,
    Plus,
    Copy,
    CheckCircle
} from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { useClient } from '../../contexts/ClientContext';
import ConfirmDeleteModal from '../modals/ConfirmDeleteModal';

const ClientCard = ({
    client,
    onEdit,
    onManageSubscriptions,
    onViewDetails,
    onManagePreferences,
    onNewAssessment,
    viewMode = 'grid' // 'grid' or 'list'
}) => {
    const [showDropdown, setShowDropdown] = useState(false);
    const [showDeleteModal, setShowDeleteModal] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);
    const [deleteError, setDeleteError] = useState(null);
    const [expandedDescription, setExpandedDescription] = useState(false);
    const [copiedField, setCopiedField] = useState(null);
    const { user } = useAuth();
    const { deleteClient } = useClient();

    // Check if user can delete clients (Owner/Admin only based on backend logic)
    const canDeleteClients = () => {
        const role = user?.Role;
        return role === 'Owner' || role === 'Admin';
    };

    // Check if client has preferences configured
    const hasPreferences = () => {
        return client.HasPreferences === true;
    };

    const getStatusPillColor = (status) => {
        switch (status?.toLowerCase()) {
            case 'active':
                return 'bg-gradient-to-r from-green-600 to-green-700 text-white hover:from-green-700 hover:to-green-800';
            case 'inactive':
                return 'bg-gradient-to-r from-gray-600 to-gray-700 text-white hover:from-gray-700 hover:to-gray-800';
            case 'pending':
                return 'bg-gradient-to-r from-yellow-600 to-yellow-700 text-black hover:from-yellow-700 hover:to-yellow-800';
            case 'suspended':
                return 'bg-gradient-to-r from-red-600 to-red-700 text-white hover:from-red-700 hover:to-red-800';
            default:
                return 'bg-gradient-to-r from-gray-600 to-gray-700 text-white hover:from-gray-700 hover:to-gray-800';
        }
    };

    const handleCopyToClipboard = async (text, field) => {
        try {
            await navigator.clipboard.writeText(text);
            setCopiedField(field);
            setTimeout(() => setCopiedField(null), 2000);
        } catch (err) {
            console.error('Failed to copy text: ', err);
        }
    };

    const handleDeleteClick = () => {
        setShowDropdown(false);
        setDeleteError(null);
        setShowDeleteModal(true);
    };

    const handleDeleteConfirm = async () => {
        setIsDeleting(true);
        setDeleteError(null);

        try {
            const result = await deleteClient(client.ClientId);

            if (result.success) {
                setShowDeleteModal(false);
            } else {
                setDeleteError({
                    message: result.message,
                    details: result.details,
                    suggestion: result.suggestion
                });
            }
        } catch (error) {
            console.error('[ClientCard] Delete error:', error);
            setDeleteError({
                message: 'Unexpected error occurred while deleting client'
            });
        } finally {
            setIsDeleting(false);
        }
    };

    const handleDeleteCancel = () => {
        setShowDeleteModal(false);
        setDeleteError(null);
    };

    const truncateDescription = (text, maxLength = 120) => {
        if (!text || text.length <= maxLength) return text;
        return text.substring(0, maxLength) + '...';
    };

    const formatLastAssessment = () => {
        // This would come from your API - placeholder for now
        if (client.LastAssessmentDate) {
            const date = new Date(client.LastAssessmentDate);
            const now = new Date();
            const diffDays = Math.floor((now - date) / (1000 * 60 * 60 * 24));

            if (diffDays === 0) return 'Today';
            if (diffDays === 1) return 'Yesterday';
            if (diffDays < 7) return `${diffDays} days ago`;
            if (diffDays < 30) return `${Math.floor(diffDays / 7)} weeks ago`;
            return `${Math.floor(diffDays / 30)} months ago`;
        }
        return null;
    };

    const hasAssociatedData = (client.AssessmentCount || 0) > 0 ||
        (client.EnvironmentCount || 0) > 0 ||
        (client.SubscriptionCount || 0) > 0;

    const lastAssessment = formatLastAssessment();

    // List view layout
    if (viewMode === 'list') {
        return (
            <>
                <div className="bg-gray-900 border border-gray-800 rounded-lg p-4 hover:bg-gray-800/50 transition-all duration-200 group">
                    <div className="flex items-center justify-between">
                        {/* Left side - Company info */}
                        <div className="flex items-center space-x-4 flex-1">
                            <div className="w-10 h-10 bg-blue-600 rounded-lg flex items-center justify-center flex-shrink-0">
                                <Building2 size={20} className="text-white" />
                            </div>

                            <div className="flex-1 min-w-0">
                                <div className="flex items-center space-x-3 mb-1">
                                    <h3 className="text-lg font-semibold text-white truncate">{client.Name}</h3>
                                    {client.Industry && (
                                        <span className="px-2 py-1 bg-blue-900/30 text-blue-300 text-xs rounded-full border border-blue-800">
                                            {client.Industry}
                                        </span>
                                    )}
                                </div>

                                {client.Description && (
                                    <p className="text-sm text-gray-400 mb-2 leading-relaxed">
                                        {expandedDescription ? client.Description : truncateDescription(client.Description)}
                                        {client.Description.length > 120 && (
                                            <button
                                                onClick={() => setExpandedDescription(!expandedDescription)}
                                                className="ml-2 text-blue-400 hover:text-blue-300 text-xs"
                                            >
                                                {expandedDescription ? 'Show less' : 'Show more'}
                                            </button>
                                        )}
                                    </p>
                                )}

                                <div className="flex items-center space-x-6 text-sm">
                                    {client.ContactEmail && (
                                        <div className="flex items-center space-x-2 group/copy cursor-pointer"
                                            onClick={() => handleCopyToClipboard(client.ContactEmail, 'email')}>
                                            <Mail size={14} className="text-gray-500" />
                                            <span className="text-gray-300">{client.ContactEmail}</span>
                                            {copiedField === 'email' ? (
                                                <CheckCircle size={14} className="text-green-400" />
                                            ) : (
                                                <Copy size={12} className="text-gray-500 opacity-0 group-hover/copy:opacity-100 transition-opacity" />
                                            )}
                                        </div>
                                    )}

                                    {client.ContactPhone && (
                                        <div className="flex items-center space-x-2 group/copy cursor-pointer"
                                            onClick={() => handleCopyToClipboard(client.ContactPhone, 'phone')}>
                                            <Phone size={14} className="text-gray-500" />
                                            <span className="text-gray-300">{client.ContactPhone}</span>
                                            {copiedField === 'phone' ? (
                                                <CheckCircle size={14} className="text-green-400" />
                                            ) : (
                                                <Copy size={12} className="text-gray-500 opacity-0 group-hover/copy:opacity-100 transition-opacity" />
                                            )}
                                        </div>
                                    )}

                                    {(client.City || client.State || client.Country) && (
                                        <div className="flex items-center space-x-2">
                                            <MapPin size={14} className="text-gray-500" />
                                            <span className="text-gray-300">
                                                {[client.City, client.State, client.Country].filter(Boolean).join(', ')}
                                            </span>
                                        </div>
                                    )}

                                    {/* Inline statistics - larger and more prominent */}
                                    <div className="flex items-center space-x-6 ml-auto">
                                        <div className="flex items-center space-x-2 cursor-pointer hover:bg-gray-800 rounded-lg px-2 py-1 transition-colors"
                                            onClick={() => onViewDetails?.(client)} title="View assessments">
                                            <Activity size={16} className="text-blue-400" />
                                            <span className={`text-lg font-semibold ${client.AssessmentCount > 0 ? 'text-white' : 'text-gray-500'}`}>
                                                {client.AssessmentCount || 0}
                                            </span>
                                            <span className="text-sm text-gray-400">Assessments</span>
                                        </div>
                                        <div className="flex items-center space-x-2 cursor-pointer hover:bg-gray-800 rounded-lg px-2 py-1 transition-colors"
                                            onClick={() => onManageSubscriptions?.(client)} title="Manage environments">
                                            <Database size={16} className="text-green-400" />
                                            <span className={`text-lg font-semibold ${client.EnvironmentCount > 0 ? 'text-white' : 'text-gray-500'}`}>
                                                {client.EnvironmentCount || 0}
                                            </span>
                                            <span className="text-sm text-gray-400">Environments</span>
                                        </div>
                                        <div className="flex items-center space-x-2 cursor-pointer hover:bg-gray-800 rounded-lg px-2 py-1 transition-colors"
                                            onClick={() => onManageSubscriptions?.(client)} title="Manage subscriptions">
                                            <Shield size={16} className="text-purple-400" />
                                            <span className={`text-lg font-semibold ${client.SubscriptionCount > 0 ? 'text-white' : 'text-gray-500'}`}>
                                                {client.SubscriptionCount || 0}
                                            </span>
                                            <span className="text-sm text-gray-400">Subscriptions</span>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>

                        {/* Right side - Status, preferences, and actions */}
                        <div className="flex items-center space-x-3 ml-6">
                            {/* Status Badge */}
                            <div className={`flex items-center px-3 py-1 rounded-full text-sm font-medium transition-all shadow-sm ${getStatusPillColor(client.Status)}`}>
                                <span>{client.Status || 'Active'}</span>
                            </div>

                            {/* Preferences Indicator */}
                            {hasPreferences() && (
                                <div
                                    className="flex items-center space-x-2 px-3 py-1 bg-gradient-to-r from-purple-600 to-purple-700 rounded-full text-white text-sm font-medium cursor-pointer hover:from-purple-700 hover:to-purple-800 transition-all shadow-sm"
                                    onClick={() => onManagePreferences?.(client)}
                                    title="Custom governance preferences configured"
                                >
                                    <Sliders size={14} />
                                    <span>Configured</span>
                                </div>
                            )}

                            {/* Quick Action Buttons */}
                            <button
                                onClick={() => onViewDetails?.(client)}
                                className="flex items-center space-x-2 px-3 py-1 bg-gray-700 hover:bg-gray-600 text-white rounded-lg text-sm font-medium transition-colors"
                                title="Client overview"
                            >
                                <Eye size={14} />
                                <span>Overview</span>
                            </button>

                            <button
                                onClick={() => onEdit?.(client)}
                                className="flex items-center space-x-2 px-3 py-1 bg-gray-700 hover:bg-gray-600 text-white rounded-lg text-sm font-medium transition-colors"
                                title="Edit client"
                            >
                                <Edit3 size={14} />
                                <span>Edit</span>
                            </button>

                            <button
                                onClick={() => onManageSubscriptions?.(client)}
                                className="flex items-center space-x-2 px-3 py-1 bg-gray-700 hover:bg-gray-600 text-white rounded-lg text-sm font-medium transition-colors"
                                title="Manage subscriptions"
                            >
                                <Cloud size={14} />
                                <span>Subscriptions</span>
                            </button>

                            {/* Quick Assessment Button */}
                            <button
                                onClick={() => onNewAssessment?.(client)}
                                className="flex items-center space-x-2 px-3 py-1 bg-yellow-600 hover:bg-yellow-700 text-black rounded-lg text-sm font-medium transition-colors"
                                title="Start new assessment"
                            >
                                <Plus size={14} />
                                <span>Assess</span>
                            </button>

                            {/* Actions Dropdown */}
                            <div className="relative">
                                <button
                                    onClick={() => setShowDropdown(!showDropdown)}
                                    className="p-2 text-gray-400 hover:text-white hover:bg-gray-700 rounded-lg transition-colors"
                                >
                                    <MoreVertical size={16} />
                                </button>

                                {showDropdown && (
                                    <>
                                        <div className="fixed inset-0 z-10" onClick={() => setShowDropdown(false)} />
                                        <div className="absolute right-0 top-full mt-1 w-56 bg-gray-800 border border-gray-700 rounded-lg shadow-xl z-20">
                                            <div className="py-1">
                                                <button
                                                    onClick={() => { onEdit?.(client); setShowDropdown(false); }}
                                                    className="w-full px-4 py-2 text-left text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-3"
                                                >
                                                    <Edit3 size={16} />
                                                    <span>Edit Client</span>
                                                </button>

                                                <button
                                                    onClick={() => { onNewAssessment?.(client); setShowDropdown(false); }}
                                                    className="w-full px-4 py-2 text-left text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-3"
                                                >
                                                    <Plus size={16} />
                                                    <span>New Assessment</span>
                                                </button>

                                                <div className="border-t border-gray-600 my-1"></div>

                                                <button
                                                    onClick={() => { onManagePreferences?.(client); setShowDropdown(false); }}
                                                    className={`w-full px-4 py-2 text-left text-sm hover:bg-gray-700 hover:text-white flex items-center space-x-3 ${hasPreferences() ? 'text-purple-400' : 'text-gray-300'
                                                        }`}
                                                >
                                                    <Sliders size={16} />
                                                    <span>{hasPreferences() ? 'Edit Preferences' : 'Set Preferences'}</span>
                                                </button>

                                                <button
                                                onClick={() => { onManageSubscriptions?.(client); setShowDropdown(false); }}
                                                className="w-full px-4 py-2 text-left text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-3"
                                                >
                                                <Cloud size={16} />
                                                <span>Manage Subscriptions</span>
                                                </button>

                                                <div className="border-t border-gray-600 my-1"></div>

                                                <button
                                                    onClick={() => { onViewDetails?.(client); setShowDropdown(false); }}
                                                    className="w-full px-4 py-2 text-left text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-3"
                                                >
                                                    <Eye size={16} />
                                                    <span>Client Overview</span>
                                                </button>

                                                {canDeleteClients() && (
                                                    <>
                                                        <div className="border-t border-gray-600 my-1"></div>
                                                        <button
                                                            onClick={handleDeleteClick}
                                                            className="w-full px-4 py-2 text-left text-sm text-red-400 hover:bg-red-900/20 hover:text-red-300 flex items-center space-x-3"
                                                        >
                                                            <Trash2 size={16} />
                                                            <span>Delete Client</span>
                                                        </button>
                                                    </>
                                                )}
                                            </div>
                                        </div>
                                    </>
                                )}
                            </div>
                        </div>
                    </div>
                </div>

                <ConfirmDeleteModal
                    isOpen={showDeleteModal}
                    onClose={handleDeleteCancel}
                    onConfirm={handleDeleteConfirm}
                    title="Delete Client"
                    message={hasAssociatedData
                        ? "This client has associated data. Deleting may not be possible."
                        : "Are you sure you want to delete this client? This action cannot be undone."
                    }
                    itemName={client.Name}
                    deleteButtonText="Delete Client"
                    isLoading={isDeleting}
                    details={deleteError?.details}
                />
            </>
        );
    }

    // Grid view layout (enhanced)
    return (
        <>
            <div className="bg-gray-900 border border-gray-800 rounded-lg p-6 hover:bg-gray-800/50 hover:shadow-lg transition-all duration-200 group">
                {/* Header */}
                <div className="mb-4">
                    {/* Top row: Company info and dropdown */}
                    <div className="flex items-start justify-between mb-3">
                        <div className="flex items-center space-x-4 flex-1">
                            <div className="w-12 h-12 bg-blue-600 rounded-lg flex items-center justify-center flex-shrink-0">
                                <Building2 size={24} className="text-white" />
                            </div>
                            <div className="min-w-0 flex-1">
                                <h3 className="text-lg font-semibold text-white truncate">{client.Name}</h3>

                                {/* Contact Email - More prominent */}
                                {client.ContactEmail && (
                                    <div className="flex items-center space-x-2 mt-1 group/copy cursor-pointer"
                                        onClick={() => handleCopyToClipboard(client.ContactEmail, 'email')}>
                                        <Mail size={12} className="text-gray-500" />
                                        <span className="text-sm text-gray-300">{client.ContactEmail}</span>
                                        {copiedField === 'email' ? (
                                            <CheckCircle size={12} className="text-green-400" />
                                        ) : (
                                            <Copy size={10} className="text-gray-500 opacity-0 group-hover/copy:opacity-100 transition-opacity" />
                                        )}
                                    </div>
                                )}

                                {/* Industry as badge */}
                                {client.Industry && (
                                    <span className="inline-block mt-2 px-2 py-1 bg-blue-900/30 text-blue-300 text-xs rounded-full border border-blue-800">
                                        {client.Industry}
                                    </span>
                                )}

                                {/* Description with better line height and truncation */}
                                {client.Description && (
                                    <p className="text-xs text-gray-500 mt-2 leading-relaxed">
                                        {expandedDescription ? client.Description : truncateDescription(client.Description)}
                                        {client.Description.length > 120 && (
                                            <button
                                                onClick={() => setExpandedDescription(!expandedDescription)}
                                                className="ml-2 text-blue-400 hover:text-blue-300"
                                            >
                                                {expandedDescription ? 'less' : 'more'}
                                            </button>
                                        )}
                                    </p>
                                )}
                            </div>
                        </div>

                        {/* Actions Dropdown */}
                        <div className="relative ml-4">
                            <button
                                onClick={() => setShowDropdown(!showDropdown)}
                                className="p-2 text-gray-400 hover:text-white hover:bg-gray-700 rounded-lg transition-colors"
                            >
                                <MoreVertical size={16} />
                            </button>

                            {showDropdown && (
                                <>
                                    <div className="fixed inset-0 z-10" onClick={() => setShowDropdown(false)} />
                                    <div className="absolute right-0 top-full mt-1 w-56 bg-gray-800 border border-gray-700 rounded-lg shadow-xl z-20">
                                        <div className="py-1">
                                            <button
                                                onClick={() => { onEdit?.(client); setShowDropdown(false); }}
                                                className="w-full px-4 py-2 text-left text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-3"
                                            >
                                                <Edit3 size={16} />
                                                <span>Edit Client</span>
                                            </button>

                                            <button
                                                onClick={() => { onNewAssessment?.(client); setShowDropdown(false); }}
                                                className="w-full px-4 py-2 text-left text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-3"
                                            >
                                                <Plus size={16} />
                                                <span>New Assessment</span>
                                            </button>

                                            <div className="border-t border-gray-600 my-1"></div>

                                            <button
                                                onClick={() => { onManagePreferences?.(client); setShowDropdown(false); }}
                                                className={`w-full px-4 py-2 text-left text-sm hover:bg-gray-700 hover:text-white flex items-center space-x-3 ${hasPreferences() ? 'text-purple-400' : 'text-gray-300'
                                                    }`}
                                            >
                                                <Sliders size={16} />
                                                <span>{hasPreferences() ? 'Edit Preferences' : 'Set Preferences'}</span>
                                            </button>

                                            <button
                                                onClick={() => { onManageSubscriptions?.(client); setShowDropdown(false); }}
                                                className="w-full px-4 py-2 text-left text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-3"
                                            >
                                                <Cloud size={16} />
                                                <span>Manage Subscriptions</span>
                                            </button>

                                            <div className="border-t border-gray-600 my-1"></div>

                                            <button
                                                onClick={() => { onViewDetails?.(client); setShowDropdown(false); }}
                                                className="w-full px-4 py-2 text-left text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-3"
                                            >
                                                <Eye size={16} />
                                                <span>Client Overview</span>
                                            </button>

                                            {canDeleteClients() && (
                                                <>
                                                    <div className="border-t border-gray-600 my-1"></div>
                                                    <button
                                                        onClick={handleDeleteClick}
                                                        className="w-full px-4 py-2 text-left text-sm text-red-400 hover:bg-red-900/20 hover:text-red-300 flex items-center space-x-3"
                                                    >
                                                        <Trash2 size={16} />
                                                        <span>Delete Client</span>
                                                    </button>
                                                </>
                                            )}
                                        </div>
                                    </div>
                                </>
                            )}
                        </div>
                    </div>

                    {/* Bottom row: Status and Preferences indicators */}
                    <div className="flex items-center justify-between">
                        <div className="flex items-center space-x-2">
                            {/* Status Badge - matching pill style */}
                            <div className={`flex items-center px-4 py-2 rounded-full text-sm font-medium transition-all shadow-sm ${getStatusPillColor(client.Status)}`}>
                                <span>{client.Status || 'Active'}</span>
                            </div>

                            {/* Preferences Indicator */}
                            {hasPreferences() && (
                                <div
                                    className="flex items-center space-x-2 px-4 py-2 bg-gradient-to-r from-purple-600 to-purple-700 rounded-full text-white text-sm font-medium cursor-pointer hover:from-purple-700 hover:to-purple-800 transition-all shadow-sm"
                                    onClick={() => onManagePreferences?.(client)}
                                    title="Custom governance preferences configured"
                                >
                                    <Sliders size={16} />
                                    <span>Configured</span>
                                </div>
                            )}
                        </div>

                        {/* Quick Assessment Button */}
                        <button
                            onClick={() => onNewAssessment?.(client)}
                            className="flex items-center space-x-2 px-3 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded-lg text-sm font-medium transition-colors opacity-0 group-hover:opacity-100"
                            title="Start new assessment"
                        >
                            <Plus size={16} />
                            <span>Quick Assessment</span>
                        </button>
                    </div>
                </div>

                {/* Contact Information */}
                <div className="space-y-2 mb-4">
                    <div className="flex items-center justify-between">
                        {/* Left side - Phone and Location */}
                        <div className="flex items-center space-x-4 text-sm">
                            {client.ContactPhone && (
                                <div className="flex items-center space-x-2 group/copy cursor-pointer"
                                    onClick={() => handleCopyToClipboard(client.ContactPhone, 'phone')}>
                                    <Phone size={14} className="text-gray-500" />
                                    <span className="text-gray-300">{client.ContactPhone}</span>
                                    {copiedField === 'phone' ? (
                                        <CheckCircle size={12} className="text-green-400" />
                                    ) : (
                                        <Copy size={10} className="text-gray-500 opacity-0 group-hover/copy:opacity-100 transition-opacity" />
                                    )}
                                </div>
                            )}

                            {(client.City || client.State || client.Country) && (
                                <div className="flex items-center space-x-2">
                                    <MapPin size={14} className="text-gray-500" />
                                    <span className="text-gray-300 font-medium">
                                        {[client.City, client.State, client.Country].filter(Boolean).join(', ')}
                                    </span>
                                </div>
                            )}
                        </div>

                        {/* Right side - Last Assessment */}
                        {lastAssessment && (
                            <div className="text-xs text-gray-400">
                                Last assessment: {lastAssessment}
                            </div>
                        )}
                    </div>
                </div>

                {/* Statistics - Clickable */}
                <div className="grid grid-cols-3 gap-4">
                    <div
                        className="text-center cursor-pointer hover:bg-gray-800 rounded-lg p-2 transition-colors"
                        onClick={() => onViewDetails?.(client)} // Navigate to assessments
                        title="View assessments"
                    >
                        <div className="flex items-center justify-center space-x-1 mb-1">
                            <Activity size={14} className="text-blue-400" />
                            <span className="text-xs text-gray-400">Assessments</span>
                        </div>
                        <p className={`text-lg font-semibold ${client.AssessmentCount > 0 ? 'text-white' : 'text-gray-500'}`}>
                            {client.AssessmentCount || 0}
                        </p>
                    </div>

                    <div
                        className="text-center cursor-pointer hover:bg-gray-800 rounded-lg p-2 transition-colors"
                        onClick={() => onManageSubscriptions?.(client)} // Navigate to environments
                        title="Manage environments"
                    >
                        <div className="flex items-center justify-center space-x-1 mb-1">
                            <Database size={14} className="text-green-400" />
                            <span className="text-xs text-gray-400">Environments</span>
                        </div>
                        <p className={`text-lg font-semibold ${client.EnvironmentCount > 0 ? 'text-white' : 'text-gray-500'}`}>
                            {client.EnvironmentCount || 0}
                        </p>
                    </div>

                    <div
                        className="text-center cursor-pointer hover:bg-gray-800 rounded-lg p-2 transition-colors"
                        onClick={() => onManageSubscriptions?.(client)} // Navigate to subscriptions
                        title="Manage subscriptions"
                    >
                        <div className="flex items-center justify-center space-x-1 mb-1">
                            <Shield size={14} className="text-purple-400" />
                            <span className="text-xs text-gray-400">Subscriptions</span>
                        </div>
                        <p className={`text-lg font-semibold ${client.SubscriptionCount > 0 ? 'text-white' : 'text-gray-500'}`}>
                            {client.SubscriptionCount || 0}
                        </p>
                    </div>
                </div>
            </div>

            {/* Delete Confirmation Modal */}
            <ConfirmDeleteModal
                isOpen={showDeleteModal}
                onClose={handleDeleteCancel}
                onConfirm={handleDeleteConfirm}
                title="Delete Client"
                message={hasAssociatedData
                    ? "This client has associated data. Deleting may not be possible."
                    : "Are you sure you want to delete this client? This action cannot be undone."
                }
                itemName={client.Name}
                deleteButtonText="Delete Client"
                isLoading={isDeleting}
                details={deleteError?.details}
            />
        </>
    );
};

export default ClientCard;