import React, { useState } from 'react';
import { createPortal } from 'react-dom';
import { useAuth } from '../../contexts/AuthContext';

const MfaVerificationModal = ({ isOpen, onClose, onVerificationSuccess }) => {
    const [code, setCode] = useState('');
    const [useBackupCode, setUseBackupCode] = useState(false);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    const { verifyMfa } = useAuth();

    const handleVerify = async () => {
        if (!code) {
            setError('Please enter a code');
            return;
        }

        // Validate code format
        if (useBackupCode) {
            // Backup codes should be in format xxxx-xxxx (9 characters total)
            if (code.length !== 9 || !code.includes('-')) {
                setError('Please enter a valid backup code in format XXXX-XXXX');
                return;
            }
        } else {
            // TOTP codes should be 6 digits
            if (code.length !== 6) {
                setError('Please enter a valid 6-digit code');
                return;
            }
        }

        try {
            setLoading(true);
            setError('');

            // Use the AuthContext verifyMfa method which calls login with MFA code
            const result = await verifyMfa(code, useBackupCode);

            onVerificationSuccess(result);
            handleClose();
        } catch (err) {
            setError(err.message);
        } finally {
            setLoading(false);
        }
    };

    const handleClose = () => {
        setCode('');
        setUseBackupCode(false);
        setError('');
        setLoading(false);
        
        // Call onClose to reset MFA state and return to login
        onClose();
    };

    const handleKeyPress = (e) => {
        if (e.key === 'Enter') {
            handleVerify();
        }
    };

    if (!isOpen) return null;

    return createPortal(
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-[9999]">
            <div className="bg-gray-900 border border-gray-800 rounded-lg p-6 w-full max-w-md mx-4">
                <div className="flex justify-between items-center mb-6">
                    <h2 className="text-xl font-semibold text-white">Two-Factor Authentication</h2>
                    <button
                        onClick={handleClose}
                        className="text-gray-400 hover:text-gray-300 transition-colors"
                        title="Cancel MFA and return to login"
                    >
                        ✕
                    </button>
                </div>

                {error && (
                    <div className="bg-red-900 border border-red-800 text-red-300 px-4 py-3 rounded mb-4">
                        {error}
                    </div>
                )}

                <div className="space-y-4">
                    <div>
                        <p className="text-sm text-gray-300 mb-3">
                            {useBackupCode
                                ? 'Enter one of your 8-character backup codes:'
                                : 'Enter the 6-digit code from your authenticator app:'
                            }
                        </p>

                        <input
                            type="text"
                            placeholder={useBackupCode ? 'XXXX-XXXX' : '000000'}
                            value={code}
                            onChange={(e) => {
                                if (useBackupCode) {
                                    // Allow alphanumeric and hyphens, auto-format as XXXX-XXXX
                                    let value = e.target.value.toUpperCase().replace(/[^A-Z0-9-]/g, '');

                                    // Auto-add hyphen after 4 characters
                                    if (value.length === 4 && !value.includes('-')) {
                                        value = value + '-';
                                    }

                                    // Limit to 9 characters (XXXX-XXXX format)
                                    setCode(value.slice(0, 9));
                                } else {
                                    // Only numbers for TOTP codes
                                    setCode(e.target.value.replace(/\D/g, '').slice(0, 6));
                                }
                            }}
                            onKeyPress={handleKeyPress}
                            className="w-full px-3 py-2 bg-gray-800 border border-gray-700 rounded-md text-white focus:outline-none focus:ring-2 focus:ring-yellow-500 text-center text-lg tracking-widest"
                            maxLength={useBackupCode ? 9 : 6}
                            autoComplete="off"
                            autoFocus
                            disabled={loading}
                        />
                    </div>

                    <div className="text-center">
                        <button
                            onClick={() => {
                                setUseBackupCode(!useBackupCode);
                                setCode('');
                                setError('');
                            }}
                            className="text-sm text-yellow-400 hover:text-yellow-300 transition-colors"
                            disabled={loading}
                        >
                            {useBackupCode
                                ? 'Use authenticator app instead'
                                : 'Use backup code instead'
                            }
                        </button>
                    </div>

                    <button
                        onClick={handleVerify}
                        disabled={loading || !code || (!useBackupCode && code.length !== 6) || (useBackupCode && (code.length !== 9 || !code.includes('-')))}
                        className="w-full bg-yellow-600 hover:bg-yellow-700 text-black py-2 px-4 rounded disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                    >
                        {loading ? 'Verifying...' : 'Verify'}
                    </button>

                    <div className="text-xs text-gray-400 text-center">
                        {useBackupCode
                            ? 'Each backup code can only be used once.'
                            : 'Having trouble? Try using a backup code instead.'
                        }
                    </div>
                </div>
            </div>
        </div>

        ,document.body
    );
};

export default MfaVerificationModal;