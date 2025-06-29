import React, { useState } from 'react';
import { createPortal } from 'react-dom';
import { X, AlertTriangle, Trash2, Loader2 } from 'lucide-react';

const ConfirmDeleteModal = ({ 
    isOpen, 
    onClose, 
    onConfirm, 
    title = "Confirm Delete",
    message = "Are you sure you want to delete this item?",
    itemName = "",
    deleteButtonText = "Delete",
    isLoading = false,
    details = null
}) => {
    const [confirmText, setConfirmText] = useState('');
    const requiresConfirmation = itemName && itemName.length > 0;
    const canConfirm = !requiresConfirmation || confirmText === itemName;

    const handleConfirm = () => {
        if (canConfirm && !isLoading) {
            onConfirm();
        }
    };

    const handleClose = () => {
        if (!isLoading) {
            setConfirmText('');
            onClose();
        }
    };

    if (!isOpen) return null;

    return createPortal(
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
            <div className="bg-gray-800 rounded-lg shadow-xl w-full max-w-md">
                {/* Header */}
                <div className="flex items-center justify-between p-6 border-b border-gray-700">
                    <div className="flex items-center space-x-3">
                        <div className="w-10 h-10 bg-red-600 rounded-lg flex items-center justify-center">
                            <AlertTriangle size={20} className="text-white" />
                        </div>
                        <div>
                            <h2 className="text-lg font-semibold text-white">{title}</h2>
                            <p className="text-sm text-gray-400">This action cannot be undone</p>
                        </div>
                    </div>
                    <button
                        onClick={handleClose}
                        disabled={isLoading}
                        className="text-gray-400 hover:text-white p-2 rounded-lg hover:bg-gray-700 disabled:opacity-50"
                    >
                        <X size={20} />
                    </button>
                </div>

                {/* Content */}
                <div className="p-6">
                    <div className="mb-6">
                        <p className="text-gray-300 mb-4">{message}</p>
                        
                        {itemName && (
                            <div className="bg-gray-900 border border-gray-700 rounded p-3 mb-4">
                                <p className="text-sm text-gray-400 mb-1">Item to delete:</p>
                                <p className="text-white font-medium">{itemName}</p>
                            </div>
                        )}

                        {details && (
                            <div className="bg-yellow-900/20 border border-yellow-800 rounded p-3 mb-4">
                                <div className="flex items-start space-x-2">
                                    <AlertTriangle size={16} className="text-yellow-400 mt-0.5 flex-shrink-0" />
                                    <div>
                                        <p className="text-yellow-400 font-medium text-sm mb-1">Warning</p>
                                        <div className="text-yellow-300 text-sm space-y-1">
                                            {typeof details === 'string' ? (
                                                <p>{details}</p>
                                            ) : (
                                                <>
                                                    {details.hasAssessments && (
                                                        <p>• This client has associated assessments</p>
                                                    )}
                                                    {details.hasEnvironments && (
                                                        <p>• This client has Azure environments</p>
                                                    )}
                                                    {details.hasSubscriptions && (
                                                        <p>• This client has subscriptions</p>
                                                    )}
                                                    {details.suggestion && (
                                                        <p className="mt-2 font-medium">{details.suggestion}</p>
                                                    )}
                                                </>
                                            )}
                                        </div>
                                    </div>
                                </div>
                            </div>
                        )}

                        {requiresConfirmation && (
                            <div>
                                <label className="block text-sm font-medium text-gray-300 mb-2">
                                    Type <span className="text-red-400 font-mono">{itemName}</span> to confirm:
                                </label>
                                <input
                                    type="text"
                                    value={confirmText}
                                    onChange={(e) => setConfirmText(e.target.value)}
                                    disabled={isLoading}
                                    className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-red-600 disabled:opacity-50"
                                    placeholder={`Type "${itemName}" here`}
                                    autoComplete="off"
                                />
                            </div>
                        )}
                    </div>

                    {/* Actions */}
                    <div className="flex items-center justify-end space-x-3">
                        <button
                            type="button"
                            onClick={handleClose}
                            disabled={isLoading}
                            className="px-4 py-2 text-gray-400 hover:text-white transition-colors disabled:opacity-50"
                        >
                            Cancel
                        </button>
                        <button
                            onClick={handleConfirm}
                            disabled={!canConfirm || isLoading}
                            className="px-6 py-2 bg-red-600 hover:bg-red-700 text-white rounded font-medium flex items-center space-x-2 disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                            {isLoading ? (
                                <>
                                    <Loader2 size={16} className="animate-spin" />
                                    <span>Deleting...</span>
                                </>
                            ) : (
                                <>
                                    <Trash2 size={16} />
                                    <span>{deleteButtonText}</span>
                                </>
                            )}
                        </button>
                    </div>
                </div>
            </div>
        </div>,
        document.body
    );
};

export default ConfirmDeleteModal;