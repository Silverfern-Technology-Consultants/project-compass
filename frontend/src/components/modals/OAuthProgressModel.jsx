import React, { useState } from 'react';
import { Shield, Loader2, AlertCircle } from 'lucide-react';
import { oauthApi } from '../../services/apiService';
import OAuthProgressModal from '../modals/OAuthProgressModal';

const OAuthButton = ({ clientId, clientName, onSuccess, onError, disabled = false, className = '' }) => {
    const [isInitiating, setIsInitiating] = useState(false);
    const [showProgressModal, setShowProgressModal] = useState(false);
    const [progressId, setProgressId] = useState(null);
    const [error, setError] = useState(null);

    const handleOAuthInitiate = async () => {
        setIsInitiating(true);
        setError(null);

        try {
            console.log('[OAuthButton] Initiating OAuth for client:', clientName);

            const response = await oauthApi.initiateOAuth(clientId, clientName);

            if (response.requiresKeyVaultCreation) {
                // Show progress modal for Key Vault creation
                console.log('[OAuthButton] Key Vault creation required, showing progress modal');
                setProgressId(response.progressId);
                setShowProgressModal(true);
            } else {
                // Direct OAuth flow - redirect immediately
                console.log('[OAuthButton] Redirecting to OAuth authorization:', response.authorizationUrl);
                window.location.href = response.authorizationUrl;

                if (onSuccess) {
                    onSuccess(response);
                }
            }
        } catch (error) {
            console.error('[OAuthButton] OAuth initiation failed:', error);
            const errorMessage = error.message || 'Failed to initiate OAuth setup';
            setError(errorMessage);

            if (onError) {
                onError(error);
            }
        } finally {
            setIsInitiating(false);
        }
    };

    const handleProgressComplete = (oauthData) => {
        console.log('[OAuthButton] OAuth setup completed, redirecting:', oauthData.authorizationUrl);
        setShowProgressModal(false);

        // Redirect to OAuth authorization
        window.location.href = oauthData.authorizationUrl;

        if (onSuccess) {
            onSuccess(oauthData);
        }
    };

    const handleProgressClose = () => {
        setShowProgressModal(false);
        setProgressId(null);
    };

    return (
        <>
            <button
                onClick={handleOAuthInitiate}
                disabled={disabled || isInitiating}
                className={`
                    px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded font-medium 
                    flex items-center space-x-2 disabled:opacity-50 disabled:cursor-not-allowed
                    transition-colors ${className}
                `}
            >
                {isInitiating ? (
                    <>
                        <Loader2 size={16} className="animate-spin" />
                        <span>Setting up...</span>
                    </>
                ) : (
                    <>
                        <Shield size={16} />
                        <span>Authorize Azure Access</span>
                    </>
                )}
            </button>

            {error && (
                <div className="mt-2 p-3 bg-red-900/20 border border-red-800 rounded">
                    <div className="flex items-center space-x-2">
                        <AlertCircle size={16} className="text-red-400" />
                        <p className="text-red-300 text-sm">{error}</p>
                    </div>
                </div>
            )}

            <OAuthProgressModal
                isOpen={showProgressModal}
                onClose={handleProgressClose}
                progressId={progressId}
                onComplete={handleProgressComplete}
                clientName={clientName}
            />
        </>
    );
};

export default OAuthButton;