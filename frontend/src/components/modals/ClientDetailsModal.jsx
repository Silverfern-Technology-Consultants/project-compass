import React, { useState, useEffect } from 'react';
import { createPortal } from 'react-dom';
import {
    X,
    Building2,
    Edit3,
    Settings,
    Activity,
    Database,
    Shield,
    Calendar,
    Mail,
    Phone,
    MapPin,
    Clock,
    User,
    FileText,
    ExternalLink,
    Loader2,
    AlertCircle,
    CheckCircle
} from 'lucide-react';
import { clientApi } from '../../services/apiService';

const ClientDetailsModal = ({ isOpen, onClose, client, onEdit, onManageSubscriptions }) => {
    const [clientDetails, setClientDetails] = useState(null);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState(null);

    // Load full client details when modal opens
    useEffect(() => {
        if (isOpen && client?.ClientId) {
            loadClientDetails();
        }
    }, [isOpen, client?.ClientId]);

    // Reset state when modal closes
    useEffect(() => {
        if (!isOpen) {
            setClientDetails(null);
            setError(null);
        }
    }, [isOpen]);

    const loadClientDetails = async () => {
        setIsLoading(true);
        setError(null);

        try {
            const details = await clientApi.getClient(client.ClientId);
            setClientDetails(details);
        } catch (error) {
            console.error('Failed to load client details:', error);
            setError('Failed to load client details');
        } finally {
            setIsLoading(false);
        }
    };

    const formatDate = (dateString) => {
        if (!dateString) return 'Not set';
        try {
            return new Date(dateString).toLocaleDateString('en-US', {
                year: 'numeric',
                month: 'long',
                day: 'numeric'
            });
        } catch {
            return 'Invalid date';
        }
    };

    const getContractStatus = () => {
        if (!clientDetails?.ContractStartDate || !clientDetails?.ContractEndDate) {
            return { text: 'No Contract', color: 'text-gray-400', bgColor: 'bg-gray-900/30', borderColor: 'border-gray-700' };
        }

        const now = new Date();
        const start = new Date(clientDetails.ContractStartDate);
        const end = new Date(clientDetails.ContractEndDate);

        if (now < start) {
            return { text: 'Future Contract', color: 'text-blue-400', bgColor: 'bg-blue-900/30', borderColor: 'border-blue-800' };
        } else if (now > end) {
            return { text: 'Expired Contract', color: 'text-red-400', bgColor: 'bg-red-900/30', borderColor: 'border-red-800' };
        } else {
            const daysLeft = Math.ceil((end - now) / (1000 * 60 * 60 * 24));
            if (daysLeft <= 30) {
                return { text: `Expires in ${daysLeft} days`, color: 'text-yellow-400', bgColor: 'bg-yellow-900/30', borderColor: 'border-yellow-800' };
            }
            return { text: 'Active Contract', color: 'text-green-400', bgColor: 'bg-green-900/30', borderColor: 'border-green-800' };
        }
    };

    if (!isOpen || !client) return null;

    if (isLoading) {
        return createPortal(
            <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                <div className="bg-gray-800 rounded-lg shadow-xl w-full max-w-2xl">
                    <div className="flex items-center justify-between p-6 border-b border-gray-700">
                        <div className="flex items-center space-x-3">
                            <div className="w-10 h-10 bg-blue-600 rounded-lg flex items-center justify-center">
                                <Building2 size={20} className="text-white" />
                            </div>
                            <div>
                                <h2 className="text-lg font-semibold text-white">Client Details</h2>
                                <p className="text-sm text-gray-400">Loading client information...</p>
                            </div>
                        </div>
                        <button
                            onClick={onClose}
                            className="text-gray-400 hover:text-white p-2 rounded-lg hover:bg-gray-700"
                        >
                            <X size={20} />
                        </button>
                    </div>

                    <div className="p-6 text-center">
                        <div className="w-8 h-8 border-2 border-yellow-600 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
                        <p className="text-gray-400">Loading client details...</p>
                    </div>
                </div>
            </div>
            ,document.body
        );
    }

    if (error) {
        return createPortal(
            <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
                <div className="bg-gray-800 rounded-lg shadow-xl w-full max-w-2xl">
                    <div className="flex items-center justify-between p-6 border-b border-gray-700">
                        <div className="flex items-center space-x-3">
                            <div className="w-10 h-10 bg-red-600 rounded-lg flex items-center justify-center">
                                <AlertCircle size={20} className="text-white" />
                            </div>
                            <div>
                                <h2 className="text-lg font-semibold text-white">Error</h2>
                                <p className="text-sm text-gray-400">Failed to load client details</p>
                            </div>
                        </div>
                        <button
                            onClick={onClose}
                            className="text-gray-400 hover:text-white p-2 rounded-lg hover:bg-gray-700"
                        >
                            <X size={20} />
                        </button>
                    </div>

                    <div className="p-6">
                        <div className="bg-red-900/20 border border-red-800 rounded p-4 mb-4">
                            <p className="text-red-300">{error}</p>
                        </div>
                        <div className="flex justify-end space-x-3">
                            <button
                                onClick={loadClientDetails}
                                className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded font-medium"
                            >
                                Retry
                            </button>
                            <button
                                onClick={onClose}
                                className="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded"
                            >
                                Close
                            </button>
                        </div>
                    </div>
                </div>
            </div>
            ,document.body
        );
    }

    if (!clientDetails) return null;

    const contractStatus = getContractStatus();

    return createPortal (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
            <div className="bg-gray-800 rounded-lg shadow-xl w-full max-w-4xl max-h-[90vh] overflow-y-auto">
                {/* Header */}
                <div className="flex items-center justify-between p-6 border-b border-gray-700">
                    <div className="flex items-center space-x-3">
                        <div className="w-12 h-12 bg-blue-600 rounded-lg flex items-center justify-center">
                            <Building2 size={24} className="text-white" />
                        </div>
                        <div>
                            <h2 className="text-xl font-semibold text-white">{clientDetails.Name}</h2>
                            <p className="text-sm text-gray-400">{clientDetails.Industry || 'No industry specified'}</p>
                        </div>
                    </div>
                    <div className="flex items-center space-x-3">
                        <button
                            onClick={() => onManageSubscriptions?.(client)}
                            className="px-3 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded text-sm font-medium flex items-center space-x-2"
                        >
                            <Settings size={16} />
                            <span>Manage Subscriptions</span>
                        </button>
                        <button
                            onClick={() => onEdit?.(client)}
                            className="px-3 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded text-sm font-medium flex items-center space-x-2"
                        >
                            <Edit3 size={16} />
                            <span>Edit</span>
                        </button>
                        <button
                            onClick={onClose}
                            className="text-gray-400 hover:text-white p-2 rounded-lg hover:bg-gray-700"
                        >
                            <X size={20} />
                        </button>
                    </div>
                </div>

                <div className="p-6">
                    {/* Status and Description */}
                    <div className="mb-6">
                        <div className="flex items-center space-x-3 mb-3">
                            <div className={`px-3 py-1 rounded-full text-sm font-medium border ${clientDetails.Status?.toLowerCase() === 'active'
                                    ? 'bg-green-900/30 text-green-400 border-green-800'
                                    : 'bg-gray-900/30 text-gray-400 border-gray-700'
                                }`}>
                                {clientDetails.Status || 'Active'}
                            </div>
                            <div className={`px-3 py-1 rounded-full text-sm font-medium border ${contractStatus.bgColor} ${contractStatus.color} ${contractStatus.borderColor}`}>
                                {contractStatus.text}
                            </div>
                        </div>
                        {clientDetails.Description && (
                            <p className="text-gray-300 leading-relaxed">{clientDetails.Description}</p>
                        )}
                    </div>

                    {/* Statistics Grid */}
                    <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
                        <div className="bg-gray-900 border border-gray-700 rounded-lg p-4 text-center">
                            <Activity size={24} className="text-blue-400 mx-auto mb-2" />
                            <p className="text-2xl font-bold text-white">{clientDetails.TotalAssessments || 0}</p>
                            <p className="text-xs text-gray-400">Total Assessments</p>
                        </div>
                        <div className="bg-gray-900 border border-gray-700 rounded-lg p-4 text-center">
                            <CheckCircle size={24} className="text-green-400 mx-auto mb-2" />
                            <p className="text-2xl font-bold text-white">{clientDetails.CompletedAssessments || 0}</p>
                            <p className="text-xs text-gray-400">Completed</p>
                        </div>
                        <div className="bg-gray-900 border border-gray-700 rounded-lg p-4 text-center">
                            <Database size={24} className="text-purple-400 mx-auto mb-2" />
                            <p className="text-2xl font-bold text-white">{clientDetails.TotalEnvironments || 0}</p>
                            <p className="text-xs text-gray-400">Environments</p>
                        </div>
                        <div className="bg-gray-900 border border-gray-700 rounded-lg p-4 text-center">
                            <Shield size={24} className="text-yellow-400 mx-auto mb-2" />
                            <p className="text-2xl font-bold text-white">{clientDetails.TotalSubscriptions || 0}</p>
                            <p className="text-xs text-gray-400">Subscriptions</p>
                        </div>
                    </div>

                    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                        {/* Contact Information */}
                        <div className="bg-gray-900 border border-gray-700 rounded-lg p-6">
                            <h3 className="text-lg font-semibold text-white mb-4 flex items-center space-x-2">
                                <User size={20} className="text-blue-400" />
                                <span>Contact Information</span>
                            </h3>
                            <div className="space-y-4">
                                {clientDetails.ContactName && (
                                    <div className="flex items-center space-x-3">
                                        <User size={16} className="text-gray-400" />
                                        <div>
                                            <p className="text-sm text-gray-400">Contact Person</p>
                                            <p className="text-white">{clientDetails.ContactName}</p>
                                        </div>
                                    </div>
                                )}
                                {clientDetails.ContactEmail && (
                                    <div className="flex items-center space-x-3">
                                        <Mail size={16} className="text-gray-400" />
                                        <div>
                                            <p className="text-sm text-gray-400">Email</p>
                                            <a href={`mailto:${clientDetails.ContactEmail}`} className="text-white hover:text-yellow-400 transition-colors">
                                                {clientDetails.ContactEmail}
                                            </a>
                                        </div>
                                    </div>
                                )}
                                {clientDetails.ContactPhone && (
                                    <div className="flex items-center space-x-3">
                                        <Phone size={16} className="text-gray-400" />
                                        <div>
                                            <p className="text-sm text-gray-400">Phone</p>
                                            <a href={`tel:${clientDetails.ContactPhone}`} className="text-white hover:text-yellow-400 transition-colors">
                                                {clientDetails.ContactPhone}
                                            </a>
                                        </div>
                                    </div>
                                )}
                                {(clientDetails.Address || clientDetails.City || clientDetails.State || clientDetails.Country) && (
                                    <div className="flex items-start space-x-3">
                                        <MapPin size={16} className="text-gray-400 mt-1" />
                                        <div>
                                            <p className="text-sm text-gray-400">Address</p>
                                            <div className="text-white">
                                                {clientDetails.Address && <p>{clientDetails.Address}</p>}
                                                <p>
                                                    {[clientDetails.City, clientDetails.State, clientDetails.PostalCode].filter(Boolean).join(', ')}
                                                </p>
                                                {clientDetails.Country && <p>{clientDetails.Country}</p>}
                                            </div>
                                        </div>
                                    </div>
                                )}
                                {clientDetails.TimeZone && (
                                    <div className="flex items-center space-x-3">
                                        <Clock size={16} className="text-gray-400" />
                                        <div>
                                            <p className="text-sm text-gray-400">Time Zone</p>
                                            <p className="text-white">{clientDetails.TimeZone}</p>
                                        </div>
                                    </div>
                                )}
                            </div>
                        </div>

                        {/* Contract & Business Information */}
                        <div className="bg-gray-900 border border-gray-700 rounded-lg p-6">
                            <h3 className="text-lg font-semibold text-white mb-4 flex items-center space-x-2">
                                <FileText size={20} className="text-green-400" />
                                <span>Contract & Business</span>
                            </h3>
                            <div className="space-y-4">
                                <div className="flex items-center space-x-3">
                                    <Calendar size={16} className="text-gray-400" />
                                    <div>
                                        <p className="text-sm text-gray-400">Contract Period</p>
                                        <p className="text-white">
                                            {clientDetails.ContractStartDate
                                                ? `${formatDate(clientDetails.ContractStartDate)} - ${formatDate(clientDetails.ContractEndDate)}`
                                                : 'No contract dates set'
                                            }
                                        </p>
                                    </div>
                                </div>
                                <div className="flex items-center space-x-3">
                                    <Building2 size={16} className="text-gray-400" />
                                    <div>
                                        <p className="text-sm text-gray-400">Industry</p>
                                        <p className="text-white">{clientDetails.Industry || 'Not specified'}</p>
                                    </div>
                                </div>
                                <div className="flex items-center space-x-3">
                                    <Calendar size={16} className="text-gray-400" />
                                    <div>
                                        <p className="text-sm text-gray-400">Client Since</p>
                                        <p className="text-white">{formatDate(clientDetails.CreatedDate)}</p>
                                    </div>
                                </div>
                                {clientDetails.CreatedBy && (
                                    <div className="flex items-center space-x-3">
                                        <User size={16} className="text-gray-400" />
                                        <div>
                                            <p className="text-sm text-gray-400">Created By</p>
                                            <p className="text-white">{clientDetails.CreatedBy}</p>
                                        </div>
                                    </div>
                                )}
                            </div>
                        </div>
                    </div>

                    {/* Recent Assessments */}
                    {clientDetails.RecentAssessments && clientDetails.RecentAssessments.length > 0 && (
                        <div className="mt-6">
                            <h3 className="text-lg font-semibold text-white mb-4 flex items-center space-x-2">
                                <Activity size={20} className="text-purple-400" />
                                <span>Recent Assessments</span>
                            </h3>
                            <div className="bg-gray-900 border border-gray-700 rounded-lg overflow-hidden">
                                <div className="divide-y divide-gray-700">
                                    {clientDetails.RecentAssessments.slice(0, 5).map((assessment) => (
                                        <div key={assessment.AssessmentId} className="p-4 hover:bg-gray-800/50 transition-colors">
                                            <div className="flex items-center justify-between">
                                                <div className="flex-1">
                                                    <h4 className="text-white font-medium">{assessment.Name}</h4>
                                                    <p className="text-sm text-gray-400 mt-1">
                                                        Started {formatDate(assessment.StartedDate)}
                                                        {assessment.CompletedDate && ` • Completed ${formatDate(assessment.CompletedDate)}`}
                                                    </p>
                                                </div>
                                                <div className="flex items-center space-x-3">
                                                    {assessment.OverallScore && (
                                                        <div className="text-right">
                                                            <p className="text-lg font-bold text-yellow-400">{assessment.OverallScore}%</p>
                                                            <p className="text-xs text-gray-400">Score</p>
                                                        </div>
                                                    )}
                                                    <div className={`px-2 py-1 rounded text-xs font-medium ${assessment.Status === 'Completed'
                                                            ? 'bg-green-900/30 text-green-400 border border-green-800'
                                                            : assessment.Status === 'InProgress'
                                                                ? 'bg-blue-900/30 text-blue-400 border border-blue-800'
                                                                : 'bg-gray-900/30 text-gray-400 border border-gray-700'
                                                        }`}>
                                                        {assessment.Status}
                                                    </div>
                                                </div>
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            </div>
                        </div>
                    )}

                    {/* Team Access */}
                    {clientDetails.TeamAccess && clientDetails.TeamAccess.length > 0 && (
                        <div className="mt-6">
                            <h3 className="text-lg font-semibold text-white mb-4 flex items-center space-x-2">
                                <User size={20} className="text-orange-400" />
                                <span>Team Access</span>
                            </h3>
                            <div className="bg-gray-900 border border-gray-700 rounded-lg overflow-hidden">
                                <div className="divide-y divide-gray-700">
                                    {clientDetails.TeamAccess.map((access) => (
                                        <div key={access.CustomerId} className="p-4">
                                            <div className="flex items-center justify-between">
                                                <div>
                                                    <h4 className="text-white font-medium">{access.CustomerName}</h4>
                                                    <p className="text-sm text-gray-400">{access.CustomerEmail}</p>
                                                </div>
                                                <div className="text-right">
                                                    <div className={`inline-block px-2 py-1 rounded text-xs font-medium ${access.AccessLevel === 'Admin'
                                                            ? 'bg-red-900/30 text-red-400 border border-red-800'
                                                            : 'bg-blue-900/30 text-blue-400 border border-blue-800'
                                                        }`}>
                                                        {access.AccessLevel}
                                                    </div>
                                                    <p className="text-xs text-gray-500 mt-1">
                                                        Since {formatDate(access.GrantedDate)}
                                                    </p>
                                                </div>
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            </div>
                        </div>
                    )}
                </div>
            </div>
        </div>
        ,document.body
    );
};

export default ClientDetailsModal;