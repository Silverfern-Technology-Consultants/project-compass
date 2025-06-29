import React, { useState } from 'react';
import {
    Building2,
    Edit3,
    Settings,
    MoreVertical,
    Calendar,
    Mail,
    Phone,
    MapPin,
    Activity,
    Database,
    Shield,
    ExternalLink,
    Trash2
} from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { useClient } from '../../contexts/ClientContext';
import ConfirmDeleteModal from '../modals/ConfirmDeleteModal';

const ClientCard = ({ client, onEdit, onManageSubscriptions, onViewDetails }) => {
    const [showDropdown, setShowDropdown] = useState(false);
    const [showDeleteModal, setShowDeleteModal] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);
    const [deleteError, setDeleteError] = useState(null);
    const { user } = useAuth();
    const { deleteClient } = useClient();

    // Check if user can delete clients (Owner/Admin only based on backend logic)
    const canDeleteClients = () => {
        const role = user?.Role;
        return role === 'Owner' || role === 'Admin';
    };

    const getStatusColor = (status) => {
        switch (status?.toLowerCase()) {
            case 'active':
                return 'bg-green-900/30 text-green-400 border-green-800';
            case 'inactive':
                return 'bg-gray-900/30 text-gray-400 border-gray-700';
            case 'pending':
                return 'bg-yellow-900/30 text-yellow-400 border-yellow-800';
            case 'suspended':
                return 'bg-red-900/30 text-red-400 border-red-800';
            default:
                return 'bg-gray-900/30 text-gray-400 border-gray-700';
        }
    };

    const formatDate = (dateString) => {
        if (!dateString) return null;
        try {
            return new Date(dateString).toLocaleDateString('en-US', {
                year: 'numeric',
                month: 'short',
                day: 'numeric'
            });
        } catch {
            return null;
        }
    };

    const isContractActive = () => {
        if (!client.ContractStartDate || !client.ContractEndDate) return false;
        const now = new Date();
        const start = new Date(client.ContractStartDate);
        const end = new Date(client.ContractEndDate);
        return now >= start && now <= end;
    };

    const getContractStatus = () => {
        if (!client.ContractStartDate || !client.ContractEndDate) {
            return { text: 'No Contract', color: 'text-gray-400' };
        }

        const now = new Date();
        const start = new Date(client.ContractStartDate);
        const end = new Date(client.ContractEndDate);

        if (now < start) {
            return { text: 'Future Contract', color: 'text-blue-400' };
        } else if (now > end) {
            return { text: 'Expired Contract', color: 'text-red-400' };
        } else {
            const daysLeft = Math.ceil((end - now) / (1000 * 60 * 60 * 24));
            if (daysLeft <= 30) {
                return { text: `Expires in ${daysLeft} days`, color: 'text-yellow-400' };
            }
            return { text: 'Active Contract', color: 'text-green-400' };
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
                // Success - client has been removed from context automatically
            } else {
                // Handle deletion failure (e.g., client has associated data)
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

    const contractStatus = getContractStatus();
    const hasAssociatedData = (client.AssessmentCount || 0) > 0 ||
        (client.EnvironmentCount || 0) > 0 ||
        (client.SubscriptionCount || 0) > 0;

    return (
        <>
            <div className="bg-gray-900 border border-gray-800 rounded-lg p-6 hover:bg-gray-800/50 transition-all duration-200 group">
                {/* Header */}
                <div className="flex items-start justify-between mb-4">
                    <div className="flex items-center space-x-4">
                        <div className="w-12 h-12 bg-blue-600 rounded-lg flex items-center justify-center flex-shrink-0">
                            <Building2 size={24} className="text-white" />
                        </div>
                        <div className="min-w-0 flex-1">
                            <h3 className="text-lg font-semibold text-white truncate">{client.Name}</h3>
                            {client.Industry && (
                                <p className="text-sm text-gray-400">{client.Industry}</p>
                            )}
                            {client.Description && (
                                <p className="text-xs text-gray-500 mt-1 line-clamp-2">{client.Description}</p>
                            )}
                        </div>
                    </div>

                    {/* Status and Actions */}
                    <div className="flex items-center space-x-3">
                        <div className={`px-2 py-1 rounded text-xs font-medium border ${getStatusColor(client.Status)}`}>
                            {client.Status || 'Active'}
                        </div>

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
                                    {/* Backdrop */}
                                    <div
                                        className="fixed inset-0 z-10"
                                        onClick={() => setShowDropdown(false)}
                                    />

                                    {/* Dropdown Menu */}
                                    <div className="absolute right-0 top-full mt-1 w-56 bg-gray-800 border border-gray-700 rounded-lg shadow-xl z-20">
                                        <div className="py-1">
                                            <button
                                                onClick={() => {
                                                    onEdit?.(client);
                                                    setShowDropdown(false);
                                                }}
                                                className="w-full px-4 py-2 text-left text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-3"
                                            >
                                                <Edit3 size={16} />
                                                <span>Edit Client</span>
                                            </button>

                                            <button
                                                onClick={() => {
                                                    onManageSubscriptions?.(client);
                                                    setShowDropdown(false);
                                                }}
                                                className="w-full px-4 py-2 text-left text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-3"
                                            >
                                                <Settings size={16} />
                                                <span>Manage Subscriptions</span>
                                            </button>

                                            <div className="border-t border-gray-700 my-1"></div>

                                            <button
                                                onClick={() => {
                                                    onViewDetails?.(client);
                                                    setShowDropdown(false);
                                                }}
                                                className="w-full px-4 py-2 text-left text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-3"
                                            >
                                                <ExternalLink size={16} />
                                                <span>View Details</span>
                                            </button>

                                            {/* Delete Option - Only for Owner/Admin */}
                                            {canDeleteClients() && (
                                                <>
                                                    <div className="border-t border-gray-700 my-1"></div>
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

                {/* Contact Information */}
                <div className="space-y-2 mb-4">
                    {client.ContactName && (
                        <div className="flex items-center space-x-2 text-sm">
                            <span className="text-gray-400">Contact:</span>
                            <span className="text-gray-300">{client.ContactName}</span>
                        </div>
                    )}

                    {client.ContactEmail && (
                        <div className="flex items-center space-x-2 text-sm">
                            <Mail size={14} className="text-gray-500" />
                            <span className="text-gray-300 truncate">{client.ContactEmail}</span>
                        </div>
                    )}

                    {client.ContactPhone && (
                        <div className="flex items-center space-x-2 text-sm">
                            <Phone size={14} className="text-gray-500" />
                            <span className="text-gray-300">{client.ContactPhone}</span>
                        </div>
                    )}

                    {(client.City || client.State || client.Country) && (
                        <div className="flex items-center space-x-2 text-sm">
                            <MapPin size={14} className="text-gray-500" />
                            <span className="text-gray-300">
                                {[client.City, client.State, client.Country].filter(Boolean).join(', ')}
                            </span>
                        </div>
                    )}
                </div>

                {/* Statistics */}
                <div className="grid grid-cols-3 gap-4 mb-4">
                    <div className="text-center">
                        <div className="flex items-center justify-center space-x-1 mb-1">
                            <Activity size={14} className="text-blue-400" />
                            <span className="text-xs text-gray-400">Assessments</span>
                        </div>
                        <p className="text-lg font-semibold text-white">{client.AssessmentCount || 0}</p>
                    </div>

                    <div className="text-center">
                        <div className="flex items-center justify-center space-x-1 mb-1">
                            <Database size={14} className="text-green-400" />
                            <span className="text-xs text-gray-400">Environments</span>
                        </div>
                        <p className="text-lg font-semibold text-white">{client.EnvironmentCount || 0}</p>
                    </div>

                    <div className="text-center">
                        <div className="flex items-center justify-center space-x-1 mb-1">
                            <Shield size={14} className="text-purple-400" />
                            <span className="text-xs text-gray-400">Subscriptions</span>
                        </div>
                        <p className="text-lg font-semibold text-white">{client.SubscriptionCount || 0}</p>
                    </div>
                </div>

                {/* Contract Information */}
                <div className="border-t border-gray-700 pt-4">
                    <div className="flex items-center justify-between">
                        <div className="flex items-center space-x-2">
                            <Calendar size={14} className="text-gray-500" />
                            <span className="text-xs text-gray-400">Contract:</span>
                            <span className={`text-xs font-medium ${contractStatus.color}`}>
                                {contractStatus.text}
                            </span>
                        </div>

                        {client.ContractStartDate && client.ContractEndDate && (
                            <div className="text-xs text-gray-500">
                                {formatDate(client.ContractStartDate)} - {formatDate(client.ContractEndDate)}
                            </div>
                        )}
                    </div>

                    {/* Created Date */}
                    <div className="flex items-center justify-between mt-2">
                        <span className="text-xs text-gray-500">
                            Created {formatDate(client.CreatedDate)}
                        </span>

                        {/* Quick Actions */}
                        <div className="flex items-center space-x-2 opacity-0 group-hover:opacity-100 transition-opacity">
                            <button
                                onClick={() => onEdit?.(client)}
                                className="p-1.5 text-gray-400 hover:text-white hover:bg-gray-700 rounded transition-colors"
                                title="Edit Client"
                            >
                                <Edit3 size={14} />
                            </button>
                            <button
                                onClick={() => onManageSubscriptions?.(client)}
                                className="p-1.5 text-gray-400 hover:text-white hover:bg-gray-700 rounded transition-colors"
                                title="Manage Subscriptions"
                            >
                                <Settings size={14} />
                            </button>
                        </div>
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
                details={deleteError?.details || (hasAssociatedData ? {
                    hasAssessments: (client.AssessmentCount || 0) > 0,
                    hasEnvironments: (client.EnvironmentCount || 0) > 0,
                    hasSubscriptions: (client.SubscriptionCount || 0) > 0,
                    suggestion: "Consider deactivating the client instead of deleting"
                } : null)}
            />
        </>
    );
};

export default ClientCard;