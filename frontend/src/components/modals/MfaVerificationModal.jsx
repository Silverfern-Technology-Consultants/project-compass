import React, { useState } from 'react';
import apiService from '../../services/apiService';

const MfaVerificationModal = ({ isOpen, onClose, onVerificationSuccess }) => {
    const [code, setCode] = useState('');
    const [useBackupCode, setUseBackupCode] = useState(false);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    const handleVerify = async () => {
        if (!code || (!useBackupCode && code.length !== 6) || (useBackupCode && code.length !== 8)) {
            setError(useBackupCode ? 'Please enter a valid 8-character backup code' : 'Please enter a valid 6-digit code');
            return;
        }

        try {
            setLoading(true);
            setError('');
            const { MfaApi } = apiService;
            const result = await MfaApi.verifyMfa(code, useBackupCode);

            // Store the final JWT token
            if (result.token) {
                const { AuthApi } = apiService;
                AuthApi.setAuthToken(result.token);
            }

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
        onClose();
    };

    const handleKeyPress = (e) => {
        if (e.key === 'Enter') {
            handleVerify();
        }
    };

    if (!isOpen) return null;

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-gray-900 border border-gray-800 rounded-lg p-6 w-full max-w-md mx-4">
                <div className="flex justify-between items-center mb-6">
                    <h2 className="text-xl font-semibold text-white">Two-Factor Authentication</h2>
                    <button
                        onClick={handleClose}
                        className="text-gray-400 hover:text-gray-300"
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
                            placeholder={useBackupCode ? 'XXXXXXXX' : '000000'}
                            value={code}
                            onChange={(e) => {
                                const value = useBackupCode
                                    ? e.target.value.toUpperCase().slice(0, 8)
                                    : e.target.value.replace(/\D/g, '').slice(0, 6);
                                setCode(value);
                            }}
                            onKeyPress={handleKeyPress}
                            className="w-full px-3 py-2 bg-gray-800 border border-gray-700 rounded-md text-white focus:outline-none focus:ring-2 focus:ring-yellow-500 text-center text-lg tracking-widest"
                            maxLength={useBackupCode ? 8 : 6}
                            autoComplete="off"
                            autoFocus
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
                        >
                            {useBackupCode
                                ? 'Use authenticator app instead'
                                : 'Use backup code instead'
                            }
                        </button>
                    </div>

                    <button
                        onClick={handleVerify}
                        disabled={loading || !code || (!useBackupCode && code.length !== 6) || (useBackupCode && code.length !== 8)}
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
    );
};

export default MfaVerificationModal;