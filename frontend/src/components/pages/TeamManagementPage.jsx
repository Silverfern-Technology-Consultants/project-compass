import React, { useState, useEffect } from 'react';
import { Users, Mail, Shield, Edit, Trash2, Search, UserPlus, Crown, Settings, AlertCircle, Check, X } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import RoleChangeVerificationTool from '../Debug/RoleChangeVerificationTool';
import { teamApi, apiUtils } from '../../services/apiService';

const TeamMemberCard = ({ member, onEdit, onDelete, currentUserId, isOwnerOrAdmin }) => {
    const getRoleColor = (role) => {
        switch (role) {
            case 'Owner': return 'bg-purple-600 text-white';
            case 'Admin': return 'bg-yellow-600 text-black';
            case 'Member': return 'bg-blue-600 text-white';
            case 'Viewer': return 'bg-gray-600 text-white';
            default: return 'bg-gray-600 text-white';
        }
    };

    const getStatusColor = (status) => {
        switch (status) {
            case 'Active': return 'text-green-400';
            case 'Pending': return 'text-yellow-400';
            case 'Invited': return 'text-yellow-400';
            case 'Inactive': return 'text-gray-400';
            default: return 'text-gray-400';
        }
    };

    const isCurrentUser = member.id === currentUserId;
    const canEditMember = isOwnerOrAdmin && !isCurrentUser && member.role !== 'Owner';
    const canDeleteMember = isOwnerOrAdmin && !isCurrentUser && member.role !== 'Owner';

    return (
        <div className="bg-gray-900 border border-gray-800 rounded p-6">
            <div className="flex items-start justify-between mb-4">
                <div className="flex items-center space-x-4">
                    <div className="w-12 h-12 bg-gradient-to-br from-yellow-400 to-yellow-600 rounded flex items-center justify-center">
                        <span className="text-black font-bold text-lg">
                            {member.name.split(' ').map(n => n[0]).join('').substring(0, 2)}
                        </span>
                    </div>
                    <div>
                        <div className="flex items-center space-x-2">
                            <h3 className="text-lg font-semibold text-white">{member.name}</h3>
                            {isCurrentUser && (
                                <span className="text-xs bg-gray-700 text-gray-300 px-2 py-1 rounded">You</span>
                            )}
                            {member.role === 'Owner' && (
                                <Crown size={16} className="text-yellow-400" />
                            )}
                        </div>
                        <p className="text-gray-400">{member.email}</p>
                    </div>
                </div>
                <div className="flex items-center space-x-2">
                    <div className={`px-3 py-1 rounded text-sm font-medium ${getRoleColor(member.role)}`}>
                        {member.role}
                    </div>
                    {(canEditMember || canDeleteMember) && (
                        <div className="flex items-center space-x-1">
                            {canEditMember && (
                                <button
                                    onClick={() => onEdit(member)}
                                    className="p-2 rounded hover:bg-gray-800 text-gray-400 hover:text-white transition-colors"
                                    title="Edit member role"
                                >
                                    <Edit size={16} />
                                </button>
                            )}
                            {canDeleteMember && (
                                <button
                                    onClick={() => onDelete(member)}
                                    className="p-2 rounded hover:bg-gray-800 text-gray-400 hover:text-red-400 transition-colors"
                                    title="Remove member"
                                >
                                    <Trash2 size={16} />
                                </button>
                            )}
                        </div>
                    )}
                </div>
            </div>

            <div className="grid grid-cols-2 gap-4 mb-4">
                <div>
                    <p className="text-xs text-gray-400 uppercase tracking-wide">Status</p>
                    <p className={`font-medium ${getStatusColor(member.status)}`}>{member.status}</p>
                </div>
                <div>
                    <p className="text-xs text-gray-400 uppercase tracking-wide">Last Active</p>
                    <p className="font-medium text-white">{member.lastActive}</p>
                </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
                <div>
                    <p className="text-gray-400">Assessments Run</p>
                    <p className="font-semibold text-white">{member.assessmentsRun || 0}</p>
                </div>
                <div>
                    <p className="text-gray-400">Reports Generated</p>
                    <p className="font-semibold text-white">{member.reportsGenerated || 0}</p>
                </div>
                <div>
                    <p className="text-gray-400">Joined</p>
                    <p className="font-semibold text-white">{member.joinedDate || 'Unknown'}</p>
                </div>
            </div>
        </div>
    );
};

const InviteMemberModal = ({ isOpen, onClose, onInvite, loading }) => {
    const [formData, setFormData] = useState({
        email: '',
        role: 'Member',
        message: ''
    });
    const [errors, setErrors] = useState({});

    useEffect(() => {
        if (isOpen) {
            setFormData({ email: '', role: 'Member', message: '' });
            setErrors({});
        }
    }, [isOpen]);

    if (!isOpen) return null;

    const validateForm = () => {
        const newErrors = {};

        if (!formData.email.trim()) {
            newErrors.email = 'Email is required';
        } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email)) {
            newErrors.email = 'Please enter a valid email address';
        }

        if (!formData.role) {
            newErrors.role = 'Role is required';
        }

        setErrors(newErrors);
        return Object.keys(newErrors).length === 0;
    };

    const handleSubmit = (e) => {
        e.preventDefault();
        if (validateForm()) {
            onInvite(formData);
        }
    };

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-gray-900 border border-gray-800 rounded w-full max-w-md mx-4">
                <div className="p-6 border-b border-gray-800">
                    <h2 className="text-xl font-semibold text-white">Invite Team Member</h2>
                </div>

                <form onSubmit={handleSubmit} className="p-6 space-y-4">
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Email Address</label>
                        <input
                            type="email"
                            value={formData.email}
                            onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                            className={`w-full bg-gray-800 border rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600 ${errors.email ? 'border-red-500' : 'border-gray-700'
                                }`}
                            placeholder="colleague@company.com"
                            disabled={loading}
                        />
                        {errors.email && (
                            <p className="text-red-400 text-sm mt-1">{errors.email}</p>
                        )}
                    </div>

                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Role</label>
                        <select
                            value={formData.role}
                            onChange={(e) => setFormData({ ...formData, role: e.target.value })}
                            className={`w-full bg-gray-800 border rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600 ${errors.role ? 'border-red-500' : 'border-gray-700'
                                }`}
                            disabled={loading}
                        >
                            <option value="Member">Member - Can run assessments and view reports</option>
                            <option value="Admin">Admin - Full access except team management</option>
                            <option value="Viewer">Viewer - Read-only access to reports</option>
                        </select>
                        {errors.role && (
                            <p className="text-red-400 text-sm mt-1">{errors.role}</p>
                        )}
                    </div>

                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Personal Message (Optional)</label>
                        <textarea
                            value={formData.message}
                            onChange={(e) => setFormData({ ...formData, message: e.target.value })}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                            placeholder="Welcome to our team!"
                            rows="3"
                            disabled={loading}
                        />
                    </div>

                    <div className="flex items-center justify-end space-x-3 pt-4">
                        <button
                            type="button"
                            onClick={onClose}
                            className="px-4 py-2 text-gray-300 hover:text-white transition-colors"
                            disabled={loading}
                        >
                            Cancel
                        </button>
                        <button
                            type="submit"
                            disabled={loading}
                            className="flex items-center space-x-2 bg-yellow-600 hover:bg-yellow-700 disabled:bg-gray-600 text-black disabled:text-gray-400 px-4 py-2 rounded font-medium transition-colors"
                        >
                            <Mail size={16} />
                            <span>{loading ? 'Sending...' : 'Send Invitation'}</span>
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

const EditMemberModal = ({ isOpen, onClose, onSave, member, loading }) => {
    const [role, setRole] = useState(member?.role || 'Member');
    const [error, setError] = useState('');

    useEffect(() => {
        if (member) {
            setRole(member.role);
            setError('');
        }
    }, [member]);

    if (!isOpen || !member) return null;

    const handleSubmit = (e) => {
        e.preventDefault();
        if (!role) {
            setError('Role is required');
            return;
        }
        setError('');
        onSave({ ...member, role });
    };

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-gray-900 border border-gray-800 rounded w-full max-w-md mx-4">
                <div className="p-6 border-b border-gray-800">
                    <h2 className="text-xl font-semibold text-white">Edit Team Member</h2>
                    <p className="text-gray-400 text-sm mt-1">{member.name} ({member.email})</p>
                </div>

                <form onSubmit={handleSubmit} className="p-6 space-y-4">
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Role</label>
                        <select
                            value={role}
                            onChange={(e) => setRole(e.target.value)}
                            className={`w-full bg-gray-800 border rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600 ${error ? 'border-red-500' : 'border-gray-700'
                                }`}
                            disabled={loading}
                        >
                            <option value="Member">Member - Can run assessments and view reports</option>
                            <option value="Admin">Admin - Full access except team management</option>
                            <option value="Viewer">Viewer - Read-only access to reports</option>
                        </select>
                        {error && (
                            <p className="text-red-400 text-sm mt-1">{error}</p>
                        )}
                    </div>

                    <div className="bg-gray-800 border border-gray-700 rounded p-4">
                        <h4 className="font-medium text-white mb-2">Role Permissions</h4>
                        <div className="space-y-2 text-sm">
                            {role === 'Admin' && (
                                <>
                                    <div className="flex items-center space-x-2 text-green-400">
                                        <Shield size={14} />
                                        <span>Full access to all assessments and reports</span>
                                    </div>
                                    <div className="flex items-center space-x-2 text-green-400">
                                        <Shield size={14} />
                                        <span>Can modify settings and preferences</span>
                                    </div>
                                </>
                            )}
                            {role === 'Member' && (
                                <>
                                    <div className="flex items-center space-x-2 text-blue-400">
                                        <Shield size={14} />
                                        <span>Can run assessments and view reports</span>
                                    </div>
                                    <div className="flex items-center space-x-2 text-blue-400">
                                        <Shield size={14} />
                                        <span>Can export and share reports</span>
                                    </div>
                                </>
                            )}
                            {role === 'Viewer' && (
                                <div className="flex items-center space-x-2 text-gray-400">
                                    <Shield size={14} />
                                    <span>Read-only access to completed reports</span>
                                </div>
                            )}
                        </div>
                    </div>

                    <div className="flex items-center justify-end space-x-3 pt-4">
                        <button
                            type="button"
                            onClick={onClose}
                            className="px-4 py-2 text-gray-300 hover:text-white transition-colors"
                            disabled={loading}
                        >
                            Cancel
                        </button>
                        <button
                            type="submit"
                            disabled={loading}
                            className="bg-yellow-600 hover:bg-yellow-700 disabled:bg-gray-600 text-black disabled:text-gray-400 px-4 py-2 rounded font-medium transition-colors"
                        >
                            {loading ? 'Saving...' : 'Save Changes'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

const RemoveConfirmationModal = ({ isOpen, onClose, onConfirm, member, loading }) => {
    const [confirmText, setConfirmText] = useState('');
    const expectedText = 'REMOVE';

    if (!isOpen || !member) return null;

    const isValid = confirmText === expectedText;

    const handleSubmit = (e) => {
        e.preventDefault();
        if (isValid) {
            onConfirm();
        }
    };

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-gray-900 border border-red-800 rounded w-full max-w-md mx-4">
                <div className="p-6 border-b border-red-800">
                    <div className="flex items-center space-x-3">
                        <AlertCircle className="text-red-400" size={24} />
                        <h2 className="text-xl font-semibold text-white">Remove Team Member</h2>
                    </div>
                </div>

                <div className="p-6">
                    <div className="mb-6">
                        <p className="text-gray-300 mb-2">
                            You are about to remove <strong className="text-white">{member.name}</strong> from your team.
                        </p>
                        <p className="text-gray-400 text-sm mb-4">
                            This action will:
                        </p>
                        <ul className="text-sm text-gray-400 space-y-1 mb-4">
                            <li>• Remove their access to all assessments and reports</li>
                            <li>• Revoke their login permissions</li>
                            <li>• Cannot be undone automatically</li>
                        </ul>
                        {member.status === 'Pending' && (
                            <div className="bg-yellow-900 border border-yellow-800 rounded p-3 mb-4">
                                <p className="text-yellow-200 text-sm">
                                    <strong>Note:</strong> This will cancel their pending invitation.
                                </p>
                            </div>
                        )}
                    </div>

                    <form onSubmit={handleSubmit}>
                        <div className="mb-4">
                            <label className="block text-sm font-medium text-gray-300 mb-2">
                                Type <span className="font-bold text-red-400">{expectedText}</span> to confirm:
                            </label>
                            <input
                                type="text"
                                value={confirmText}
                                onChange={(e) => setConfirmText(e.target.value)}
                                className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-red-600"
                                placeholder={expectedText}
                                disabled={loading}
                                autoComplete="off"
                            />
                        </div>

                        <div className="flex items-center justify-end space-x-3">
                            <button
                                type="button"
                                onClick={onClose}
                                className="px-4 py-2 text-gray-300 hover:text-white transition-colors"
                                disabled={loading}
                            >
                                Cancel
                            </button>
                            <button
                                type="submit"
                                disabled={loading || !isValid}
                                className="flex items-center space-x-2 bg-red-600 hover:bg-red-700 disabled:bg-gray-600 text-white disabled:text-gray-400 px-4 py-2 rounded font-medium transition-colors"
                            >
                                <Trash2 size={16} />
                                <span>{loading ? 'Removing...' : 'Remove Member'}</span>
                            </button>
                        </div>
                    </form>
                </div>
            </div>
        </div>
    );
};

const SuccessAlert = ({ message, onClose }) => {
    useEffect(() => {
        const timer = setTimeout(onClose, 5000);
        return () => clearTimeout(timer);
    }, [onClose]);

    return (
        <div className="bg-green-900 border border-green-800 text-green-200 px-4 py-3 rounded flex items-center space-x-2">
            <Check size={20} />
            <span>{message}</span>
            <button
                onClick={onClose}
                className="ml-auto text-green-400 hover:text-green-200"
            >
                <X size={16} />
            </button>
        </div>
    );
};

const TeamManagementPage = () => {
    const { user, token } = useAuth();

    // Helper function to decode JWT and extract claims
    const decodeJWT = (token) => {
        try {
            if (!token) return null;
            const parts = token.split('.');
            if (parts.length !== 3) return null;
            const payload = parts[1];
            const paddedPayload = payload + '='.repeat((4 - payload.length % 4) % 4);
            const decodedPayload = atob(paddedPayload);
            return JSON.parse(decodedPayload);
        } catch (error) {
            console.error('Error decoding JWT token:', error);
            return null;
        }
    };

    // Extract user info from JWT token if user object is not available
    const getFromJWTOrUser = (userField, jwtClaim) => {
        if (user && user[userField] !== undefined) {
            return user[userField];
        }

        const decoded = decodeJWT(token);
        return decoded ? decoded[jwtClaim] : null;
    };

    const currentUserId = getFromJWTOrUser('customerId', 'nameid');
    const userRole = getFromJWTOrUser('role', 'role');
    const isOwnerOrAdmin = userRole === 'Owner' || userRole === 'Admin';

    // State management
    const [searchTerm, setSearchTerm] = useState('');
    const [showInviteModal, setShowInviteModal] = useState(false);
    const [showEditModal, setShowEditModal] = useState(false);
    const [showRemoveModal, setShowRemoveModal] = useState(false);
    const [selectedMember, setSelectedMember] = useState(null);
    const [teamMembers, setTeamMembers] = useState([]);
    const [teamStats, setTeamStats] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [success, setSuccess] = useState(null);
    const [actionLoading, setActionLoading] = useState(false);

    // Debug tools state
    const [showDebugTools, setShowDebugTools] = useState(false);

    // Load team data
    const loadTeamData = async () => {
        try {
            setLoading(true);
            setError(null);

            const [membersResponse, statsResponse] = await Promise.all([
                teamApi.getTeamMembers(),
                teamApi.getTeamStats()
            ]);

            setTeamMembers(membersResponse || []);
            setTeamStats(statsResponse || {});
        } catch (err) {
            console.error('Error loading team data:', err);
            const errorInfo = apiUtils.handleApiError(err);
            setError(`Failed to load team data: ${errorInfo.message}`);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        loadTeamData();
    }, []);

    // Filter members based on search
    const filteredMembers = teamMembers.filter(member =>
        member.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        member.email.toLowerCase().includes(searchTerm.toLowerCase()) ||
        member.role.toLowerCase().includes(searchTerm.toLowerCase())
    );

    // Handle invite member
    const handleInviteMember = async (formData) => {
        try {
            setActionLoading(true);
            setError(null);

            const newMember = await teamApi.inviteTeamMember({
                email: formData.email,
                role: formData.role,
                message: formData.message
            });

            setTeamMembers(prev => [...prev, newMember]);
            setShowInviteModal(false);
            setSuccess(`Invitation sent successfully to ${formData.email}`);

            // Reload stats
            const statsResponse = await teamApi.getTeamStats();
            setTeamStats(statsResponse);

        } catch (err) {
            console.error('Error inviting member:', err);
            const errorInfo = apiUtils.handleApiError(err);

            if (errorInfo.details?.includes?.('already exists')) {
                setError('A user with this email is already part of your team or has a pending invitation.');
            } else if (errorInfo.details?.includes?.('invalid email')) {
                setError('Please enter a valid email address.');
            } else {
                setError(`Failed to send invitation: ${errorInfo.message}`);
            }
        } finally {
            setActionLoading(false);
        }
    };

    // Handle edit member
    const handleEditMember = (member) => {
        setSelectedMember(member);
        setShowEditModal(true);
    };

    // Handle save member
    const handleSaveMember = async (updatedMember) => {
        try {
            setActionLoading(true);
            setError(null);

            const savedMember = await teamApi.updateTeamMember(updatedMember.id, {
                role: updatedMember.role
            });

            setTeamMembers(prev =>
                prev.map(member =>
                    member.id === updatedMember.id ? savedMember : member
                )
            );
            setShowEditModal(false);
            setSuccess(`${updatedMember.name}'s role has been updated to ${updatedMember.role}`);

        } catch (err) {
            console.error('Error updating member:', err);
            const errorInfo = apiUtils.handleApiError(err);
            setError(`Failed to update member role: ${errorInfo.message}`);
        } finally {
            setActionLoading(false);
        }
    };

    // Handle delete member
    const handleDeleteMember = (memberToDelete) => {
        setSelectedMember(memberToDelete);
        setShowRemoveModal(true);
    };

    // Handle confirm delete
    const handleConfirmDelete = async () => {
        if (!selectedMember) return;

        try {
            setActionLoading(true);
            setError(null);

            await teamApi.removeTeamMember(selectedMember.id);
            setTeamMembers(prev => prev.filter(member => member.id !== selectedMember.id));
            setShowRemoveModal(false);
            setSelectedMember(null);

            const actionType = selectedMember.status === 'Pending' ? 'Invitation cancelled' : 'Team member removed';
            setSuccess(`${actionType} successfully`);

            // Reload stats
            const statsResponse = await teamApi.getTeamStats();
            setTeamStats(statsResponse);

        } catch (err) {
            console.error('Error removing member:', err);
            const errorInfo = apiUtils.handleApiError(err);
            setError(`Failed to remove team member: ${errorInfo.message}`);
        } finally {
            setActionLoading(false);
        }
    };

    // Calculate role stats from current data
    const roleStats = teamStats?.roleDistribution || {
        Owner: teamMembers.filter(m => m.role === 'Owner').length,
        Admin: teamMembers.filter(m => m.role === 'Admin').length,
        Member: teamMembers.filter(m => m.role === 'Member').length,
        Viewer: teamMembers.filter(m => m.role === 'Viewer').length,
    };

    if (loading) {
        return (
            <div className="flex items-center justify-center min-h-96">
                <div className="text-center">
                    <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-yellow-600 mx-auto mb-4"></div>
                    <p className="text-gray-400">Loading team data...</p>
                </div>
            </div>
        );
    }

    return (
        <div className="space-y-6">
            {/* Success Alert */}
            {success && (
                <SuccessAlert
                    message={success}
                    onClose={() => setSuccess(null)}
                />
            )}

            {/* Error Alert */}
            {error && (
                <div className="bg-red-900 border border-red-800 text-red-200 px-4 py-3 rounded flex items-center space-x-2">
                    <AlertCircle size={20} />
                    <span>{error}</span>
                    <button
                        onClick={() => setError(null)}
                        className="ml-auto text-red-400 hover:text-red-200"
                    >
                        <X size={16} />
                    </button>
                </div>
            )}

            {/* Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-white">Team Management</h1>
                    <p className="text-gray-400">Manage your team members and their access permissions</p>
                </div>
                {isOwnerOrAdmin && (
                    <button
                        onClick={() => setShowInviteModal(true)}
                        disabled={actionLoading}
                        className="flex items-center space-x-2 bg-yellow-600 hover:bg-yellow-700 disabled:bg-gray-600 text-black disabled:text-gray-400 px-4 py-2 rounded font-medium transition-colors"
                    >
                        <UserPlus size={16} />
                        <span>Invite Member</span>
                    </button>
                )}
            </div>

            {/* Permission Warning for Non-Admins */}
            {!isOwnerOrAdmin && (
                <div className="bg-yellow-900 border border-yellow-800 text-yellow-200 px-4 py-3 rounded flex items-center space-x-2">
                    <AlertCircle size={20} />
                    <span>You have read-only access to team management. Contact an Owner or Admin to make changes.</span>
                </div>
            )}

            {/* Stats Cards */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                <div className="bg-gray-900 border border-gray-800 rounded p-4">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Total Members</p>
                            <p className="text-2xl font-bold text-white">{(teamStats?.totalMembers || teamMembers.length || 0)}</p>
                        </div>
                        <Users size={24} className="text-yellow-600" />
                    </div>
                </div>
                <div className="bg-gray-900 border border-gray-800 rounded p-4">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Active</p>
                            <p className="text-2xl font-bold text-green-400">
                                {(teamStats?.activeMembers || teamMembers.filter(m => m.status === 'Active').length || 0)}
                            </p>
                        </div>
                        <div className="w-3 h-3 bg-green-400 rounded-full"></div>
                    </div>
                </div>
                <div className="bg-gray-900 border border-gray-800 rounded p-4">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Pending</p>
                            <p className="text-2xl font-bold text-yellow-400">
                                {(teamStats?.pendingInvitations || teamMembers.filter(m => m.status === 'Pending').length || 0)}
                            </p>
                        </div>
                        <div className="w-3 h-3 bg-yellow-400 rounded-full"></div>
                    </div>
                </div>
                <div className="bg-gray-900 border border-gray-800 rounded p-4">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Admins</p>
                            <p className="text-2xl font-bold text-purple-400">
                                {(teamStats?.adminCount || ((roleStats.Admin || 0) + (roleStats.Owner || 0)) || 0)}
                            </p>
                        </div>
                        <Crown size={24} className="text-purple-400" />
                    </div>
                </div>
            </div>

            {/* Search */}
            <div className="bg-gray-900 border border-gray-800 rounded p-4">
                <div className="relative">
                    <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" size={16} />
                    <input
                        type="text"
                        placeholder="Search team members..."
                        value={searchTerm}
                        onChange={(e) => setSearchTerm(e.target.value)}
                        className="w-full bg-gray-800 border border-gray-700 rounded pl-10 pr-4 py-2 text-white focus:outline-none focus:border-yellow-600"
                    />
                </div>
            </div>

            {/* Team Members Grid */}
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                {filteredMembers.map(member => (
                    <TeamMemberCard
                        key={member.id}
                        member={member}
                        currentUserId={currentUserId}
                        isOwnerOrAdmin={isOwnerOrAdmin}
                        onEdit={handleEditMember}
                        onDelete={handleDeleteMember}
                    />
                ))}
            </div>

            {filteredMembers.length === 0 && !loading && (
                <div className="bg-gray-900 border border-gray-800 rounded p-12 text-center">
                    <Users size={48} className="text-gray-600 mx-auto mb-4" />
                    <h3 className="text-lg font-semibold text-white mb-2">No team members found</h3>
                    <p className="text-gray-400 mb-4">
                        {teamMembers.length === 0
                            ? "Get started by inviting your first team member."
                            : "Try adjusting your search or invite new team members."
                        }
                    </p>
                    {isOwnerOrAdmin && (
                        <button
                            onClick={() => setShowInviteModal(true)}
                            className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                        >
                            {teamMembers.length === 0 ? "Invite Your First Team Member" : "Invite Team Member"}
                        </button>
                    )}
                </div>
            )}

            {/* Role Distribution */}
            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <h3 className="text-lg font-semibold text-white mb-4">Role Distribution</h3>
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                    <div className="text-center">
                        <div className="w-12 h-12 bg-purple-600 rounded mx-auto mb-2 flex items-center justify-center">
                            <Crown size={24} className="text-white" />
                        </div>
                        <p className="text-sm text-gray-400">Owner</p>
                        <p className="text-lg font-bold text-white">{roleStats.Owner || 0}</p>
                    </div>
                    <div className="text-center">
                        <div className="w-12 h-12 bg-yellow-600 rounded mx-auto mb-2 flex items-center justify-center">
                            <Shield size={24} className="text-black" />
                        </div>
                        <p className="text-sm text-gray-400">Admin</p>
                        <p className="text-lg font-bold text-white">{roleStats.Admin || 0}</p>
                    </div>
                    <div className="text-center">
                        <div className="w-12 h-12 bg-blue-600 rounded mx-auto mb-2 flex items-center justify-center">
                            <Users size={24} className="text-white" />
                        </div>
                        <p className="text-sm text-gray-400">Member</p>
                        <p className="text-lg font-bold text-white">{roleStats.Member || 0}</p>
                    </div>
                    <div className="text-center">
                        <div className="w-12 h-12 bg-gray-600 rounded mx-auto mb-2 flex items-center justify-center">
                            <Settings size={24} className="text-white" />
                        </div>
                        <p className="text-sm text-gray-400">Viewer</p>
                        <p className="text-lg font-bold text-white">{roleStats.Viewer || 0}</p>
                    </div>
                </div>
            </div>

            {/* Debug Tools - Only show in development */}
            {process.env.NODE_ENV === 'development' && (
                <div className="mt-8 border-t border-gray-800 pt-6">
                    <button
                        onClick={() => setShowDebugTools(!showDebugTools)}
                        className="flex items-center space-x-2 text-gray-400 hover:text-white transition-colors text-sm"
                    >
                        <span>{showDebugTools ? '🔽' : '▶️'}</span>
                        <span>{showDebugTools ? 'Hide' : 'Show'} Debug Tools</span>
                        <span className="bg-gray-800 text-gray-300 px-2 py-1 rounded text-xs">DEV ONLY</span>
                    </button>

                    {showDebugTools && (
                        <div className="mt-4 bg-gray-950 border border-gray-800 rounded p-4">
                            <RoleChangeVerificationTool />
                        </div>
                    )}
                </div>
            )}

            {/* Modals */}
            <InviteMemberModal
                isOpen={showInviteModal}
                onClose={() => setShowInviteModal(false)}
                onInvite={handleInviteMember}
                loading={actionLoading}
            />

            <EditMemberModal
                isOpen={showEditModal}
                onClose={() => setShowEditModal(false)}
                onSave={handleSaveMember}
                member={selectedMember}
                loading={actionLoading}
            />

            <RemoveConfirmationModal
                isOpen={showRemoveModal}
                onClose={() => {
                    setShowRemoveModal(false);
                    setSelectedMember(null);
                }}
                onConfirm={handleConfirmDelete}
                member={selectedMember}
                loading={actionLoading}
            />
        </div>
    );
};

export default TeamManagementPage;