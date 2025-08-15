import React from 'react';
import { DollarSign } from 'lucide-react';

const CostSummaryCards = ({ 
    costData, 
    formatCurrency, 
    getChangeIcon, 
    getChangeColor, 
    formatPercentage, 
    queryParams, 
    includePreviousPeriod 
}) => {
    if (!costData?.summary) return null;

    // Calculate date ranges for display
    const formatDateRange = (fromDate, toDate) => {
        try {
            // Extract date strings without timezone conversion
            const from = fromDate.split('T')[0]; // Get YYYY-MM-DD part
            const to = toDate.split('T')[0];     // Get YYYY-MM-DD part
            
            const fromObj = new Date(from + 'T12:00:00'); // Use noon to avoid timezone issues
            const toObj = new Date(to + 'T12:00:00');
            
            const options = { month: 'short', day: 'numeric' };
            const fromFormatted = fromObj.toLocaleDateString('en-US', options);
            const toFormatted = toObj.toLocaleDateString('en-US', options);
            
            // Add year if different from current year
            const currentYear = new Date().getFullYear();
            const fromYear = fromObj.getFullYear();
            const toYear = toObj.getFullYear();
            
            let result = `${fromFormatted} - ${toFormatted}`;
            
            if (fromYear !== currentYear || toYear !== currentYear) {
                if (fromYear === toYear) {
                    result += ` ${fromYear}`;
                } else {
                    result = `${fromFormatted} ${fromYear} - ${toFormatted} ${toYear}`;
                }
            }
            
            return result;
        } catch (error) {
            console.warn('Error formatting date range:', error);
            return 'Date Range';
        }
    };

    // Calculate previous period dates - must match backend CreatePreviousPeriodQuery logic
    const calculatePreviousPeriod = (fromDate, toDate) => {
        try {
            // Parse dates as UTC to avoid timezone issues
            const fromStr = fromDate.split('T')[0]; // Get YYYY-MM-DD part
            const toStr = toDate.split('T')[0];     // Get YYYY-MM-DD part
            
            const from = new Date(fromStr + 'T12:00:00Z'); // Use noon UTC to avoid timezone issues
            const to = new Date(toStr + 'T12:00:00Z');
            
            // FIXED: Use proper month arithmetic instead of day counting (matching backend logic)
            // Check if this is a full month period (starts on 1st and ends on last day of month)
            const isFullMonth = from.getDate() === 1 && to.getDate() === new Date(to.getFullYear(), to.getMonth() + 1, 0).getDate();
            
            let previousFromDate, previousToDate;
            
            if (isFullMonth) {
                // For full month periods (e.g., July 1-31), get the previous full month (June 1-30)
                const previousMonth = new Date(from.getFullYear(), from.getMonth() - 1, 1);
                previousFromDate = previousMonth;
                previousToDate = new Date(previousMonth.getFullYear(), previousMonth.getMonth() + 1, 0); // Last day of previous month
            } else {
                // For partial periods, subtract the same number of days
                const periodLength = Math.floor((to - from) / (24 * 60 * 60 * 1000));
                previousToDate = new Date(from.getTime() - 24 * 60 * 60 * 1000);
                previousFromDate = new Date(previousToDate.getTime() - periodLength * 24 * 60 * 60 * 1000);
            }
            
            console.log('FRONTEND PERIOD DEBUG:', {
                original: { from: fromStr, to: toStr, isFullMonth },
                calculated: { 
                    from: previousFromDate.toISOString().split('T')[0], 
                    to: previousToDate.toISOString().split('T')[0] 
                }
            });
            
            return {
                from: previousFromDate.toISOString(),
                to: previousToDate.toISOString()
            };
        } catch (error) {
            console.warn('Error calculating previous period:', error);
            return { from: fromDate, to: toDate };
        }
    };

    const currentPeriodLabel = queryParams?.timePeriod ? 
        `Current Period (${formatDateRange(queryParams.timePeriod.from, queryParams.timePeriod.to)})` : 
        'Current Period';

    let previousPeriodLabel = 'Previous Period';
    if (includePreviousPeriod && queryParams?.timePeriod) {
        const previousPeriod = calculatePreviousPeriod(queryParams.timePeriod.from, queryParams.timePeriod.to);
        previousPeriodLabel = `Previous Period (${formatDateRange(previousPeriod.from, previousPeriod.to)})`;
    }

    return (
        <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <div className="flex items-center justify-between">
                    <div>
                        <p className="text-sm text-gray-400">{previousPeriodLabel}</p>
                        <p className="text-2xl font-bold text-white">
                            {formatCurrency(costData.summary.totalPreviousPeriodCost, costData.summary.currency)}
                        </p>
                    </div>
                    <DollarSign size={24} className="text-blue-400" />
                </div>
            </div>

            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <div className="flex items-center justify-between">
                    <div>
                        <p className="text-sm text-gray-400">{currentPeriodLabel}</p>
                        <p className="text-2xl font-bold text-white">
                            {formatCurrency(costData.summary.totalCurrentPeriodCost, costData.summary.currency)}
                        </p>
                    </div>
                    <DollarSign size={24} className="text-green-400" />
                </div>
            </div>

            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <div className="flex items-center justify-between">
                    <div>
                        <p className="text-sm text-gray-400">Cost Difference</p>
                        <p className={`text-2xl font-bold ${getChangeColor(costData.summary.totalCostDifference)}`}>
                            {costData.summary.totalCostDifference >= 0 ? '+' : ''}
                            {formatCurrency(costData.summary.totalCostDifference, costData.summary.currency)}
                        </p>
                    </div>
                    {getChangeIcon(costData.summary.totalCostDifference)}
                </div>
            </div>

            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <div className="flex items-center justify-between">
                    <div>
                        <p className="text-sm text-gray-400">Percentage Change</p>
                        <p className={`text-2xl font-bold ${getChangeColor(costData.summary.totalPercentageChange)}`}>
                            {formatPercentage(costData.summary.totalPercentageChange)}
                        </p>
                    </div>
                    {getChangeIcon(costData.summary.totalPercentageChange)}
                </div>
            </div>
        </div>
    );
};

export default CostSummaryCards;