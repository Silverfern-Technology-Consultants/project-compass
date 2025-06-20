import React from 'react';
import { Edit, Trash2, Crown } from 'lucide-react';

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

export default TeamMemberCard;