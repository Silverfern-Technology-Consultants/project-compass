import React, { useState } from 'react';
import { Users, Mail, Shield, Edit, Trash2, Plus, Search, UserPlus, Crown, Settings } from 'lucide-react';

const TeamMemberCard = ({ member, onEdit, onDelete, currentUserId }) => {
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
            case 'Invited': return 'text-yellow-400';
            case 'Inactive': return 'text-gray-400';
            default: return 'text-gray-400';
        }
    };

    const isCurrentUser = member.id === currentUserId;

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
                    {!isCurrentUser && member.role !== 'Owner' && (
                        <div className="flex items-center space-x-1">
                            <button
                                onClick={() => onEdit(member)}
                                className="p-2 rounded hover:bg-gray-800 text-gray-400 hover:text-white transition-colors"
                                title="Edit member"
                            >
                                <Edit size={16} />
                            </button>
                            <button
                                onClick={() => onDelete(member)}
                                className="p-2 rounded hover:bg-gray-800 text-gray-400 hover:text-red-400 transition-colors"
                                title="Remove member"
                            >
                                <Trash2 size={16} />
                            </button>
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
                    <p className="font-semibold text-white">{member.assessmentsRun}</p>
                </div>
                <div>
                    <p className="text-gray-400">Reports Generated</p>
                    <p className="font-semibold text-white">{member.reportsGenerated}</p>
                </div>
                <div>
                    <p className="text-gray-400">Joined</p>
                    <p className="font-semibold text-white">{member.joinedDate}</p>
                </div>
            </div>
        </div>
    );
};

const InviteMemberModal = ({ isOpen, onClose, onInvite }) => {
    const [formData, setFormData] = useState({
        email: '',
        role: 'Member',
        message: ''
    });

    if (!isOpen) return null;

    const handleSubmit = (e) => {
        e.preventDefault();
        onInvite(formData);
        onClose();
        setFormData({ email: '', role: 'Member', message: '' });
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
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                            placeholder="colleague@company.com"
                            required
                        />
                    </div>

                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Role</label>
                        <select
                            value={formData.role}
                            onChange={(e) => setFormData({ ...formData, role: e.target.value })}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="Member">Member - Can run assessments and view reports</option>
                            <option value="Admin">Admin - Full access except team management</option>
                            <option value="Viewer">Viewer - Read-only access to reports</option>
                        </select>
                    </div>

                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Personal Message (Optional)</label>
                        <textarea
                            value={formData.message}
                            onChange={(e) => setFormData({ ...formData, message: e.target.value })}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                            placeholder="Welcome to our team!"
                            rows="3"
                        />
                    </div>

                    <div className="flex items-center justify-end space-x-3 pt-4">
                        <button
                            type="button"
                            onClick={onClose}
                            className="px-4 py-2 text-gray-300 hover:text-white transition-colors"
                        >
                            Cancel
                        </button>
                        <button
                            type="submit"
                            className="flex items-center space-x-2 bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                        >
                            <Mail size={16} />
                            <span>Send Invitation</span>
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

const EditMemberModal = ({ isOpen, onClose, onSave, member }) => {
    const [role, setRole] = useState(member?.role || 'Member');

    if (!isOpen || !member) return null;

    const handleSubmit = (e) => {
        e.preventDefault();
        onSave({ ...member, role });
        onClose();
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
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="Member">Member - Can run assessments and view reports</option>
                            <option value="Admin">Admin - Full access except team management</option>
                            <option value="Viewer">Viewer - Read-only access to reports</option>
                        </select>
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
                        >
                            Cancel
                        </button>
                        <button
                            type="submit"
                            className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                        >
                            Save Changes
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

const TeamManagementPage = () => {
    const currentUserId = 1; // This would come from auth context
    const [searchTerm, setSearchTerm] = useState('');
    const [showInviteModal, setShowInviteModal] = useState(false);
    const [showEditModal, setShowEditModal] = useState(false);
    const [selectedMember, setSelectedMember] = useState(null);

    const [teamMembers, setTeamMembers] = useState([
        {
            id: 1,
            name: 'John Smith',
            email: 'john@safehaven.com',
            role: 'Owner',
            status: 'Active',
            lastActive: '2 minutes ago',
            assessmentsRun: 24,
            reportsGenerated: 18,
            joinedDate: 'Jan 2024'
        },
        {
            id: 2,
            name: 'Sarah Johnson',
            email: 'sarah@safehaven.com',
            role: 'Admin',
            status: 'Active',
            lastActive: '1 hour ago',
            assessmentsRun: 15,
            reportsGenerated: 12,
            joinedDate: 'Feb 2024'
        },
        {
            id: 3,
            name: 'Mike Chen',
            email: 'mike@safehaven.com',
            role: 'Member',
            status: 'Active',
            lastActive: '3 hours ago',
            assessmentsRun: 8,
            reportsGenerated: 6,
            joinedDate: 'Mar 2024'
        },
        {
            id: 4,
            name: 'Emily Davis',
            email: 'emily@safehaven.com',
            role: 'Member',
            status: 'Invited',
            lastActive: 'Never',
            assessmentsRun: 0,
            reportsGenerated: 0,
            joinedDate: 'Pending'
        },
        {
            id: 5,
            name: 'Alex Wilson',
            email: 'alex@safehaven.com',
            role: 'Viewer',
            status: 'Active',
            lastActive: '2 days ago',
            assessmentsRun: 0,
            reportsGenerated: 0,
            joinedDate: 'Apr 2024'
        }
    ]);

    const filteredMembers = teamMembers.filter(member =>
        member.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        member.email.toLowerCase().includes(searchTerm.toLowerCase()) ||
        member.role.toLowerCase().includes(searchTerm.toLowerCase())
    );

    const handleInviteMember = (formData) => {
        const newMember = {
            id: Date.now(),
            name: formData.email.split('@')[0], // Temporary name
            email: formData.email,
            role: formData.role,
            status: 'Invited',
            lastActive: 'Never',
            assessmentsRun: 0,
            reportsGenerated: 0,
            joinedDate: 'Pending'
        };
        setTeamMembers([...teamMembers, newMember]);
    };

    const handleEditMember = (member) => {
        setSelectedMember(member);
        setShowEditModal(true);
    };

    const handleSaveMember = (updatedMember) => {
        setTeamMembers(teamMembers.map(member =>
            member.id === updatedMember.id ? updatedMember : member
        ));
    };

    const handleDeleteMember = (memberToDelete) => {
        if (window.confirm(`Are you sure you want to remove ${memberToDelete.name} from the team?`)) {
            setTeamMembers(teamMembers.filter(member => member.id !== memberToDelete.id));
        }
    };

    const roleStats = {
        Owner: teamMembers.filter(m => m.role === 'Owner').length,
        Admin: teamMembers.filter(m => m.role === 'Admin').length,
        Member: teamMembers.filter(m => m.role === 'Member').length,
        Viewer: teamMembers.filter(m => m.role === 'Viewer').length,
    };

    return (
        <div className="space-y-6">
            {/* Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-white">Team Management</h1>
                    <p className="text-gray-400">Manage your team members and their access permissions</p>
                </div>
                <button
                    onClick={() => setShowInviteModal(true)}
                    className="flex items-center space-x-2 bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                >
                    <UserPlus size={16} />
                    <span>Invite Member</span>
                </button>
            </div>

            {/* Stats Cards */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                <div className="bg-gray-900 border border-gray-800 rounded p-4">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Total Members</p>
                            <p className="text-2xl font-bold text-white">{teamMembers.length}</p>
                        </div>
                        <Users size={24} className="text-yellow-600" />
                    </div>
                </div>
                <div className="bg-gray-900 border border-gray-800 rounded p-4">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Active</p>
                            <p className="text-2xl font-bold text-green-400">
                                {teamMembers.filter(m => m.status === 'Active').length}
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
                                {teamMembers.filter(m => m.status === 'Invited').length}
                            </p>
                        </div>
                        <div className="w-3 h-3 bg-yellow-400 rounded-full"></div>
                    </div>
                </div>
                <div className="bg-gray-900 border border-gray-800 rounded p-4">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Admins</p>
                            <p className="text-2xl font-bold text-purple-400">{roleStats.Admin + roleStats.Owner}</p>
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
                        onEdit={handleEditMember}
                        onDelete={handleDeleteMember}
                    />
                ))}
            </div>

            {filteredMembers.length === 0 && (
                <div className="bg-gray-900 border border-gray-800 rounded p-12 text-center">
                    <Users size={48} className="text-gray-600 mx-auto mb-4" />
                    <h3 className="text-lg font-semibold text-white mb-2">No team members found</h3>
                    <p className="text-gray-400 mb-4">Try adjusting your search or invite new team members.</p>
                    <button
                        onClick={() => setShowInviteModal(true)}
                        className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                    >
                        Invite Your First Team Member
                    </button>
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
                        <p className="text-lg font-bold text-white">{roleStats.Owner}</p>
                    </div>
                    <div className="text-center">
                        <div className="w-12 h-12 bg-yellow-600 rounded mx-auto mb-2 flex items-center justify-center">
                            <Shield size={24} className="text-black" />
                        </div>
                        <p className="text-sm text-gray-400">Admin</p>
                        <p className="text-lg font-bold text-white">{roleStats.Admin}</p>
                    </div>
                    <div className="text-center">
                        <div className="w-12 h-12 bg-blue-600 rounded mx-auto mb-2 flex items-center justify-center">
                            <Users size={24} className="text-white" />
                        </div>
                        <p className="text-sm text-gray-400">Member</p>
                        <p className="text-lg font-bold text-white">{roleStats.Member}</p>
                    </div>
                    <div className="text-center">
                        <div className="w-12 h-12 bg-gray-600 rounded mx-auto mb-2 flex items-center justify-center">
                            <Settings size={24} className="text-white" />
                        </div>
                        <p className="text-sm text-gray-400">Viewer</p>
                        <p className="text-lg font-bold text-white">{roleStats.Viewer}</p>
                    </div>
                </div>
            </div>

            {/* Modals */}
            <InviteMemberModal
                isOpen={showInviteModal}
                onClose={() => setShowInviteModal(false)}
                onInvite={handleInviteMember}
            />

            <EditMemberModal
                isOpen={showEditModal}
                onClose={() => setShowEditModal(false)}
                onSave={handleSaveMember}
                member={selectedMember}
            />
        </div>
    );
};

export default TeamManagementPage;