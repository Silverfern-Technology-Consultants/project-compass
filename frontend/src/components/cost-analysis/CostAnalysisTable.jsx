import React, { useState, useMemo } from 'react';
import { BarChart3, Settings, ArrowUpDown, ArrowUp, ArrowDown, X } from 'lucide-react';

const CostAnalysisTable = ({ 
    costData, 
    queryParams, // NEW: Pass query params to determine column visibility
    includePreviousPeriod, // NEW: Pass previous period option
    formatCurrency, 
    getChangeColor, 
    getChangeIcon, 
    formatPercentage,
    onRefresh 
}) => {
    // Table options state
    const [showTableOptions, setShowTableOptions] = useState(false);
    const [hideZeroChanges, setHideZeroChanges] = useState(false);
    
    // Sorting state
    const [sortField, setSortField] = useState('currentPeriodCost');
    const [sortDirection, setSortDirection] = useState('desc');
    
    // Full precision currency formatter for table columns
    const formatTableCurrency = (amount, currency = 'USD') => {
        // Always show full precision (6 decimal places) for table display
        return new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency,
            minimumFractionDigits: 6,
            maximumFractionDigits: 6
        }).format(amount);
    };
    
    // Determine which columns to show based on selected grouping dimensions
    const selectedDimensions = queryParams?.dataset?.grouping?.map(g => g.name) || [];
    
    const shouldShowColumn = (dimensionName) => {
        return selectedDimensions.includes(dimensionName);
    };
    
    const shouldShowPreviousPeriod = includePreviousPeriod !== false;
    
    // Sorting function
    const handleSort = (field) => {
        if (sortField === field) {
            setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
        } else {
            setSortField(field);
            setSortDirection('desc'); // Default to descending for new fields
        }
    };
    
    // Get sort icon
    const getSortIcon = (field) => {
        if (sortField !== field) {
            return <ArrowUpDown size={14} className="text-gray-500" />;
        }
        return sortDirection === 'asc' ? 
            <ArrowUp size={14} className="text-yellow-600" /> : 
            <ArrowDown size={14} className="text-yellow-600" />;
    };
    
    // Sort and filter data
    const processedItems = useMemo(() => {
        let items = costData?.items || [];
        
        // Filter out zero change results if requested
        if (hideZeroChanges) {
            items = items.filter(item => item.percentageChange !== 0);
        }
        
        // Sort items
        items = [...items].sort((a, b) => {
            let aValue = a[sortField];
            let bValue = b[sortField];
            
            // Handle special cases for sorting
            if (sortField === 'name') {
                aValue = (aValue || '').toLowerCase();
                bValue = (bValue || '').toLowerCase();
            } else if (typeof aValue === 'number' && typeof bValue === 'number') {
                // Numeric sorting
            } else {
                // String sorting
                aValue = String(aValue || '').toLowerCase();
                bValue = String(bValue || '').toLowerCase();
            }
            
            if (sortDirection === 'asc') {
                return aValue < bValue ? -1 : aValue > bValue ? 1 : 0;
            } else {
                return aValue > bValue ? -1 : aValue < bValue ? 1 : 0;
            }
        });
        
        return items;
    }, [costData?.items, hideZeroChanges, sortField, sortDirection]);
        
    if (!costData?.items || costData.items.length === 0) {
        return (
            <div className="bg-gray-900 border border-gray-800 rounded p-12 text-center">
                <div className="w-16 h-16 bg-gray-600 rounded-full flex items-center justify-center mx-auto mb-6">
                    <BarChart3 size={32} className="text-gray-400" />
                </div>
                <h2 className="text-xl font-semibold text-white mb-3">No Cost Data Found</h2>
                <p className="text-gray-400 mb-6">
                    No cost data was found for the selected time period and filters. 
                    This could be because there were no costs during this period or the Azure subscriptions don't have cost data available.
                </p>
                <button
                    onClick={onRefresh}
                    className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded font-medium"
                >
                    Refresh Analysis
                </button>
            </div>
        );
    }

    return (
        <>
            <div className="bg-gray-900 border border-gray-800 rounded overflow-hidden">
                <div className="flex items-center justify-between p-4 border-b border-gray-800">
                    <h3 className="text-lg font-semibold text-white flex items-center space-x-2">
                        <BarChart3 size={20} />
                        <span>Cost Breakdown ({processedItems.length} items{hideZeroChanges ? ` of ${costData.items.length}` : ''})</span>
                    </h3>
                    <button
                        onClick={() => setShowTableOptions(true)}
                        className="px-3 py-1.5 bg-gray-700 hover:bg-gray-600 text-white rounded text-sm flex items-center space-x-2 transition-colors"
                    >
                        <Settings size={16} />
                        <span>Table Options</span>
                    </button>
                </div>
                
                <div className="overflow-x-auto">
                    <table className="w-full">
                        <thead className="bg-gray-800">
                            <tr>
                                <th 
                                    className="px-4 py-3 text-left text-sm font-medium text-gray-300 cursor-pointer hover:bg-gray-700 transition-colors"
                                    onClick={() => handleSort('name')}
                                >
                                    <div className="flex items-center space-x-2">
                                        <span>Name</span>
                                        {getSortIcon('name')}
                                    </div>
                                </th>
                                {shouldShowColumn('ResourceType') && (
                                    <th 
                                        className="px-4 py-3 text-left text-sm font-medium text-gray-300 cursor-pointer hover:bg-gray-700 transition-colors"
                                        onClick={() => handleSort('resourceType')}
                                    >
                                        <div className="flex items-center space-x-2">
                                            <span>Resource Type</span>
                                            {getSortIcon('resourceType')}
                                        </div>
                                    </th>
                                )}
                                {shouldShowColumn('ResourceGroup') && (
                                    <th 
                                        className="px-4 py-3 text-left text-sm font-medium text-gray-300 cursor-pointer hover:bg-gray-700 transition-colors"
                                        onClick={() => handleSort('resourceGroup')}
                                    >
                                        <div className="flex items-center space-x-2">
                                            <span>Resource Group</span>
                                            {getSortIcon('resourceGroup')}
                                        </div>
                                    </th>
                                )}
                                {shouldShowColumn('SubscriptionId') && (
                                    <th 
                                        className="px-4 py-3 text-left text-sm font-medium text-gray-300 cursor-pointer hover:bg-gray-700 transition-colors"
                                        onClick={() => handleSort('subscriptionName')}
                                    >
                                        <div className="flex items-center space-x-2">
                                            <span>Subscription</span>
                                            {getSortIcon('subscriptionName')}
                                        </div>
                                    </th>
                                )}
                                {shouldShowColumn('ServiceName') && (
                                    <th className="px-4 py-3 text-left text-sm font-medium text-gray-300">Service</th>
                                )}
                                {shouldShowColumn('ResourceLocation') && (
                                    <th 
                                        className="px-4 py-3 text-left text-sm font-medium text-gray-300 cursor-pointer hover:bg-gray-700 transition-colors"
                                        onClick={() => handleSort('resourceLocation')}
                                    >
                                        <div className="flex items-center space-x-2">
                                            <span>Location</span>
                                            {getSortIcon('resourceLocation')}
                                        </div>
                                    </th>
                                )}
                                {shouldShowColumn('MeterCategory') && (
                                    <th className="px-4 py-3 text-left text-sm font-medium text-gray-300">Meter Category</th>
                                )}
                                {shouldShowPreviousPeriod && (
                                    <th 
                                        className="px-4 py-3 text-right text-sm font-medium text-gray-300 cursor-pointer hover:bg-gray-700 transition-colors"
                                        onClick={() => handleSort('previousPeriodCost')}
                                    >
                                        <div className="flex items-center justify-end space-x-2">
                                            <span>Previous Period</span>
                                            {getSortIcon('previousPeriodCost')}
                                        </div>
                                    </th>
                                )}
                                <th 
                                    className="px-4 py-3 text-right text-sm font-medium text-gray-300 cursor-pointer hover:bg-gray-700 transition-colors"
                                    onClick={() => handleSort('currentPeriodCost')}
                                >
                                    <div className="flex items-center justify-end space-x-2">
                                        <span>Current Period</span>
                                        {getSortIcon('currentPeriodCost')}
                                    </div>
                                </th>
                                {shouldShowPreviousPeriod && (
                                    <>
                                        <th 
                                            className="px-4 py-3 text-right text-sm font-medium text-gray-300 cursor-pointer hover:bg-gray-700 transition-colors"
                                            onClick={() => handleSort('costDifference')}
                                        >
                                            <div className="flex items-center justify-end space-x-2">
                                                <span>Difference</span>
                                                {getSortIcon('costDifference')}
                                            </div>
                                        </th>
                                        <th 
                                            className="px-4 py-3 text-right text-sm font-medium text-gray-300 cursor-pointer hover:bg-gray-700 transition-colors"
                                            onClick={() => handleSort('percentageChange')}
                                        >
                                            <div className="flex items-center justify-end space-x-2">
                                                <span>Change</span>
                                                {getSortIcon('percentageChange')}
                                            </div>
                                        </th>
                                    </>
                                )}
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-800">
                            {processedItems.map((item, index) => (
                                <tr key={index} className="hover:bg-gray-800/50">
                                    <td className="px-4 py-3">
                                        <div className="text-white font-medium">{item.name}</div>
                                    </td>
                                    {shouldShowColumn('ResourceType') && (
                                        <td className="px-4 py-3 text-gray-300">
                                            {item.resourceType || 'Unknown'}
                                        </td>
                                    )}
                                    {shouldShowColumn('ResourceGroup') && (
                                        <td className="px-4 py-3 text-gray-300">
                                            {item.resourceGroup || 'N/A'}
                                        </td>
                                    )}
                                    {shouldShowColumn('SubscriptionId') && (
                                        <td className="px-4 py-3 text-gray-300">
                                            <div className="text-sm">{item.subscriptionName || 'Unknown'}</div>
                                            <div className="text-xs text-gray-500">{item.subscriptionId || 'N/A'}</div>
                                        </td>
                                    )}
                                    {shouldShowColumn('ServiceName') && (
                                        <td className="px-4 py-3 text-gray-300">
                                            {item.groupingValues?.ServiceName || item.name || 'Unknown'}
                                        </td>
                                    )}
                                    {shouldShowColumn('ResourceLocation') && (
                                        <td className="px-4 py-3 text-gray-300">
                                            {item.groupingValues?.ResourceLocation || item.resourceLocation || 'Unknown'}
                                        </td>
                                    )}
                                    {shouldShowColumn('MeterCategory') && (
                                        <td className="px-4 py-3 text-gray-300">
                                            {item.groupingValues?.MeterCategory || 'Unknown'}
                                        </td>
                                    )}
                                    {shouldShowPreviousPeriod && (
                                        <td className="px-4 py-3 text-right text-gray-300">
                                            {formatTableCurrency(item.previousPeriodCost, item.currency)}
                                        </td>
                                    )}
                                    <td className="px-4 py-3 text-right text-gray-300">
                                        {formatTableCurrency(item.currentPeriodCost, item.currency)}
                                    </td>
                                    {shouldShowPreviousPeriod && (
                                        <>
                                            <td className={`px-4 py-3 text-right font-medium ${getChangeColor(item.costDifference)}`}>
                                                {item.costDifference >= 0 ? '+' : ''}
                                                {formatCurrency(item.costDifference, item.currency)}
                                            </td>
                                            <td className="px-4 py-3 text-right">
                                                <div className="flex items-center justify-end space-x-2">
                                                    {getChangeIcon(item.percentageChange)}
                                                    <span 
                                                        className={`font-medium ${
                                                            item.percentageChange === -999 
                                                                ? 'text-gray-400 underline decoration-dotted cursor-help' 
                                                                : getChangeColor(item.percentageChange)
                                                        }`}
                                                        title={item.percentageChange === -999 
                                                            ? "Zero to anything cannot be expressed as a rate as it is outside the definition boundary of rate."
                                                            : undefined
                                                        }
                                                    >
                                                        {formatPercentage(item.percentageChange)}
                                                    </span>
                                                </div>
                                            </td>
                                        </>
                                    )}
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            </div>
            
            {/* Table Options Modal */}
            {showTableOptions && (
                <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
                    <div className="bg-gray-900 border border-gray-800 rounded-lg p-6 w-full max-w-md mx-4">
                        <div className="flex items-center justify-between mb-4">
                            <h3 className="text-lg font-semibold text-white">Table Options</h3>
                            <button
                                onClick={() => setShowTableOptions(false)}
                                className="text-gray-400 hover:text-white"
                            >
                                <X size={20} />
                            </button>
                        </div>
                        
                        <div className="space-y-4">
                            <div>
                                <label className="flex items-start space-x-3 cursor-pointer p-2 rounded hover:bg-gray-800 transition-colors">
                                    <input
                                        type="checkbox"
                                        checked={hideZeroChanges}
                                        onChange={(e) => setHideZeroChanges(e.target.checked)}
                                        className="w-4 h-4 text-yellow-600 bg-gray-700 border-gray-600 rounded focus:ring-yellow-600 focus:ring-2 mt-0.5"
                                    />
                                    <div>
                                        <span className="text-white font-medium">Hide Zero Change Results</span>
                                        <p className="text-sm text-gray-400 mt-1">
                                            Filter out resources with no cost changes between periods to focus on meaningful cost variations
                                        </p>
                                    </div>
                                </label>
                            </div>
                            
                            <div className="border-t border-gray-700 pt-4">
                                <h4 className="text-white font-medium mb-3">Current Sorting</h4>
                                <div className="bg-gray-800 rounded p-3">
                                    <div className="flex items-center justify-between">
                                        <span className="text-gray-300">Field:</span>
                                        <span className="text-white font-medium">
                                            {sortField === 'currentPeriodCost' ? 'Current Period Cost' :
                                             sortField === 'previousPeriodCost' ? 'Previous Period Cost' :
                                             sortField === 'costDifference' ? 'Cost Difference' :
                                             sortField === 'percentageChange' ? 'Percentage Change' :
                                             sortField === 'resourceType' ? 'Resource Type' :
                                             sortField === 'resourceGroup' ? 'Resource Group' :
                                             sortField === 'subscriptionName' ? 'Subscription' :
                                             sortField === 'resourceLocation' ? 'Location' :
                                             sortField === 'name' ? 'Name' : sortField}
                                        </span>
                                    </div>
                                    <div className="flex items-center justify-between mt-2">
                                        <span className="text-gray-300">Direction:</span>
                                        <span className="text-white font-medium flex items-center space-x-1">
                                            {sortDirection === 'asc' ? (
                                                <>
                                                    <ArrowUp size={14} className="text-yellow-600" />
                                                    <span>Ascending</span>
                                                </>
                                            ) : (
                                                <>
                                                    <ArrowDown size={14} className="text-yellow-600" />
                                                    <span>Descending</span>
                                                </>
                                            )}
                                        </span>
                                    </div>
                                </div>
                                <p className="text-sm text-gray-400 mt-2">
                                    Click any column header to sort by that field
                                </p>
                            </div>
                        </div>
                        
                        <div className="flex items-center justify-end space-x-3 mt-6">
                            <button
                                onClick={() => setShowTableOptions(false)}
                                className="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded transition-colors"
                            >
                                Close
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </>
    );
};

export default CostAnalysisTable;