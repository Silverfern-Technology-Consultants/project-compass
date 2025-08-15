import React, { useState } from 'react';
import { AlertCircle, CheckCircle, RefreshCw, Copy } from 'lucide-react';

const PermissionSetupScreen = ({ 
    selectedClient, 
    environmentsNeedingSetup, 
    onGetInstructions, 
    onCheckPermissions, 
    onCopyCommand, 
    copiedCommand,
    onRecheckPermissions,
    onChangeClient,
    isLoading 
}) => {
    return (
        <div className="space-y-6">
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-white">Cost Analysis</h1>
                    <p className="text-gray-400">Azure cost trends for {selectedClient.Name}</p>
                </div>
                <button
                    onClick={onChangeClient}
                    className="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded text-sm"
                >
                    Change Client
                </button>
            </div>

            <div className="bg-gray-900 border border-gray-800 rounded p-8">
                <div className="flex items-center space-x-3 text-amber-400 mb-6">
                    <AlertCircle className="h-8 w-8" />
                    <h2 className="text-2xl font-bold text-white">Cost Analysis Setup Required</h2>
                </div>

                <p className="text-gray-300 mb-6">
                    To enable cost analysis features, additional permissions are needed on your Azure subscriptions.
                    This is a one-time setup that takes about 30 seconds per environment.
                </p>

                <div className="space-y-6">
                    {environmentsNeedingSetup.map((env, index) => (
                        <EnvironmentSetupCard 
                            key={env.azureEnvironmentId}
                            environment={env}
                            index={index}
                            onGetInstructions={onGetInstructions}
                            onCheckPermissions={onCheckPermissions}
                            onCopyCommand={onCopyCommand}
                            copiedCommand={copiedCommand}
                        />
                    ))}
                </div>

                <div className="mt-8 p-4 bg-blue-900/20 border border-blue-800 rounded-lg">
                    <h3 className="font-semibold text-blue-400 mb-2">Why is this needed?</h3>
                    <p className="text-blue-300 text-sm">
                        Azure cost data requires the "Cost Management Reader" role to be assigned separately 
                        from OAuth consent. This is an Azure security requirement for billing data access.
                        The permission only grants read-only access to cost and usage data.
                    </p>
                </div>

                <div className="mt-6 flex space-x-4">
                    <button
                        onClick={onRecheckPermissions}
                        disabled={isLoading}
                        className="bg-yellow-600 text-black px-6 py-2 rounded-lg hover:bg-yellow-700 disabled:opacity-50 transition-colors font-medium"
                    >
                        {isLoading ? (
                            <RefreshCw className="h-4 w-4 animate-spin inline mr-2" />
                        ) : (
                            <CheckCircle className="h-4 w-4 inline mr-2" />
                        )}
                        Recheck Permissions
                    </button>
                </div>
            </div>
        </div>
    );
};

const EnvironmentSetupCard = ({ 
    environment, 
    index, 
    onGetInstructions, 
    onCheckPermissions, 
    onCopyCommand, 
    copiedCommand 
}) => {
    const [instructions, setInstructions] = useState(null);
    const [isChecking, setIsChecking] = useState(false);
    const [showInstructions, setShowInstructions] = useState(false);

    const loadInstructions = async () => {
        const instrData = await onGetInstructions(environment.azureEnvironmentId);
        setInstructions(instrData);
        setShowInstructions(true);
    };

    const checkPermissions = async () => {
        setIsChecking(true);
        await onCheckPermissions(environment.azureEnvironmentId);
        setIsChecking(false);
    };

    return (
        <div className="border border-gray-700 rounded-lg p-6">
            <div className="flex items-center justify-between mb-4">
                <div>
                    <h3 className="text-lg font-semibold text-white">
                        Environment: {environment.environmentName || 'Azure Environment'}
                    </h3>
                    <p className="text-sm text-gray-400">
                        {environment.subscriptionIds?.length || 0} subscription(s)
                    </p>
                </div>
                <div className="flex space-x-2">
                    <button
                        onClick={loadInstructions}
                        className="bg-blue-600 text-white px-4 py-2 rounded hover:bg-blue-700 transition-colors text-sm"
                    >
                        Show Setup Instructions
                    </button>
                    <button
                        onClick={checkPermissions}
                        disabled={isChecking}
                        className="bg-green-600 text-white px-4 py-2 rounded hover:bg-green-700 disabled:opacity-50 transition-colors text-sm"
                    >
                        {isChecking ? (
                            <RefreshCw className="h-4 w-4 animate-spin" />
                        ) : (
                            'Test Permissions'
                        )}
                    </button>
                </div>
            </div>

            {showInstructions && instructions && (
                <div className="mt-4 space-y-4">
                    <div className="bg-gray-800 text-green-400 p-4 rounded font-mono text-sm relative">
                        <pre className="whitespace-pre-wrap">{instructions.azureCliCommand}</pre>
                        <button
                            onClick={() => onCopyCommand(instructions.azureCliCommand, environment.azureEnvironmentId)}
                            className="absolute top-2 right-2 bg-gray-700 hover:bg-gray-600 text-white p-2 rounded"
                            title="Copy command"
                        >
                            <Copy className="h-4 w-4" />
                        </button>
                    </div>
                    {copiedCommand === environment.azureEnvironmentId && (
                        <p className="text-green-400 text-sm">✅ Command copied to clipboard!</p>
                    )}
                </div>
            )}
        </div>
    );
};

export default PermissionSetupScreen;