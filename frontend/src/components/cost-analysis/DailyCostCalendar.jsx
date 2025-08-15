import React, { useState, useMemo } from 'react';
import { ChevronLeft, ChevronRight, Calendar, DollarSign } from 'lucide-react';

const DailyCostCalendar = ({ costData, formatCurrency }) => {
    const [selectedResource, setSelectedResource] = useState(null);
    
    // Extract all daily costs and create a calendar structure
    const calendarData = useMemo(() => {
        console.log('🗓️ Building calendar data from:', costData?.items?.length, 'items');
        
        if (!costData?.items?.length) return null;
        
        // Get all dates from all resources
        const allDates = new Set();
        const resourceData = {};
        
        costData.items.forEach(item => {
            if (item.dailyCosts?.length) {
                console.log('📊 Processing daily costs for:', item.name, 'with', item.dailyCosts.length, 'daily entries');
                resourceData[item.name] = {};
                item.dailyCosts.forEach((daily, index) => {
                    // FIXED: Handle UTC dates without timezone conversion
                    let dateStr;
                    
                    if (typeof daily.Date === 'string') {
                        // Parse the UTC date string and extract just the date part without timezone conversion
                        if (daily.Date.includes('T') || daily.Date.includes('Z')) {
                            // Extract YYYY-MM-DD from ISO string without creating Date object (avoids timezone issues)
                            dateStr = daily.Date.split('T')[0];
                        } else {
                            // Already in YYYY-MM-DD format
                            dateStr = daily.Date;
                        }
                    } else {
                        // Handle Date object - extract components directly to avoid timezone conversion
                        const date = daily.Date;
                        const year = date.getUTCFullYear();
                        const month = String(date.getUTCMonth() + 1).padStart(2, '0');
                        const day = String(date.getUTCDate()).padStart(2, '0');
                        dateStr = `${year}-${month}-${day}`;
                    }
                    
                    console.log(`📅 Daily cost ${index}:`, daily.Date, '→', dateStr, '→', daily.Cost);
                    allDates.add(dateStr);
                    resourceData[item.name][dateStr] = daily.Cost;
                });
            }
        });
        
        console.log('📅 All unique dates found:', Array.from(allDates).sort());
        console.log('📅 Total days with cost data:', allDates.size);
        console.log('📅 Resource data keys:', Object.keys(resourceData));
        console.log('📅 Sample resource data:', Object.keys(resourceData).length > 0 ? resourceData[Object.keys(resourceData)[0]] : 'none');
        
        if (allDates.size === 0) return null;
        
        const sortedDates = Array.from(allDates).sort();
        const startDate = new Date(sortedDates[0]);
        const endDate = new Date(sortedDates[sortedDates.length - 1]);
        
        console.log('📅 Date range:', startDate, 'to', endDate);
        
        // Create calendar structure by month - only include months that have actual data
        const months = {};
        
        // Process each date that has actual cost data
        sortedDates.forEach(dateStr => {
            // Parse date string directly to avoid timezone issues
            const [year, month, day] = dateStr.split('-').map(Number);
            const date = new Date(year, month - 1, day); // month is 0-indexed
            const monthKey = `${year}-${String(month).padStart(2, '0')}`;
            
            if (!months[monthKey]) {
                months[monthKey] = {
                    year: year,
                    month: month - 1, // 0-indexed for JavaScript Date
                    monthName: date.toLocaleDateString('en-US', { month: 'long', year: 'numeric' }),
                    days: []
                };
                console.log('🗓️ Created month:', monthKey, months[monthKey].monthName);
            }
            
        });
        
        console.log('📅 Total months created:', Object.keys(months).length);
        console.log('📅 Month keys:', Object.keys(months));
        
        // Now populate the days for each month that was created
        Object.keys(months).forEach(monthKey => {
            const monthData = months[monthKey];
            const firstDay = new Date(monthData.year, monthData.month, 1);
            const lastDay = new Date(monthData.year, monthData.month + 1, 0);
            
            // Generate all days for this month
            for (let day = 1; day <= lastDay.getDate(); day++) {
                const currentDate = new Date(monthData.year, monthData.month, day);
                const dateStr = currentDate.toISOString().split('T')[0];
                
                // Calculate total cost for this day across all resources
                let totalDayCost = 0;
                const dayResourceCosts = {};
                
                Object.keys(resourceData).forEach(resourceName => {
                    const cost = resourceData[resourceName][dateStr] || 0;
                    dayResourceCosts[resourceName] = cost;
                    totalDayCost += cost;
                });
                
                const hasData = allDates.has(dateStr);
                
                monthData.days.push({
                    date: new Date(currentDate),
                    dateStr,
                    totalCost: totalDayCost,
                    resourceCosts: dayResourceCosts,
                    hasData: hasData
                });
                
                // Debug log days with actual cost data
                if (hasData && totalDayCost > 0) {
                    console.log(`💰 Day ${dateStr} has ${totalDayCost.toFixed(2)} total cost`);
                }
            }
        });
        
        return { months, resourceData, sortedDates };
    }, [costData]);
    
    const [currentMonthIndex, setCurrentMonthIndex] = useState(() => {
        if (!calendarData) return 0;
        
        // Find the month that contains most of the data (likely July 2025)
        const monthKeys = Object.keys(calendarData.months).sort();
        const monthDataCounts = monthKeys.map(monthKey => {
            const month = calendarData.months[monthKey];
            const daysWithData = month.days.filter(day => day.hasData).length;
            return { monthKey, count: daysWithData };
        });
        
        // Default to the month with the most data
        const primaryMonth = monthDataCounts.reduce((max, current) => 
            current.count > max.count ? current : max
        );
        
        const primaryIndex = monthKeys.indexOf(primaryMonth.monthKey);
        console.log('📅 Defaulting to month with most data:', primaryMonth.monthKey, 'at index', primaryIndex);
        return primaryIndex >= 0 ? primaryIndex : 0;
    });
    
    if (!calendarData) {
        return (
            <div className="bg-gray-900 border border-gray-800 rounded-lg p-8 text-center">
                <Calendar className="h-12 w-12 text-gray-400 mx-auto mb-4" />
                <h3 className="text-lg font-semibold text-white mb-2">No Daily Cost Data</h3>
                <p className="text-gray-400">Daily cost data is only available when using Daily granularity in your query.</p>
            </div>
        );
    }
    
    const monthKeys = Object.keys(calendarData.months).sort();
    
    // Log the months for debugging
    console.log('📅 Available months:', monthKeys);
    console.log('📅 Current month index:', currentMonthIndex);
    console.log('📅 Selected month key:', monthKeys[currentMonthIndex]);
    
    const currentMonth = calendarData.months[monthKeys[currentMonthIndex]];
    
    const goToPreviousMonth = () => {
        setCurrentMonthIndex(Math.max(0, currentMonthIndex - 1));
    };
    
    const goToNextMonth = () => {
        setCurrentMonthIndex(Math.min(monthKeys.length - 1, currentMonthIndex + 1));
    };
    
    // Generate calendar grid (including empty cells for proper week alignment)
    const generateCalendarGrid = (month) => {
        const firstDay = new Date(month.year, month.month, 1);
        const lastDay = new Date(month.year, month.month + 1, 0);
        const startOfWeek = new Date(firstDay);
        startOfWeek.setDate(firstDay.getDate() - firstDay.getDay()); // Start from Sunday
        
        const grid = [];
        const current = new Date(startOfWeek);
        
        while (current <= lastDay || current.getDay() !== 0) {
            const weekRow = [];
            for (let i = 0; i < 7; i++) {
                const isCurrentMonth = current.getMonth() === month.month;
                const dateStr = current.toISOString().split('T')[0];
                const dayData = month.days.find(d => d.dateStr === dateStr);
                
                weekRow.push({
                    date: new Date(current),
                    dateStr,
                    isCurrentMonth,
                    dayData
                });
                
                current.setDate(current.getDate() + 1);
            }
            grid.push(weekRow);
            
            if (current > lastDay && current.getDay() === 0) break;
        }
        
        return grid;
    };
    
    const calendarGrid = generateCalendarGrid(currentMonth);
    const maxDailyCost = Math.max(...Object.values(calendarData.months).flatMap(m => m.days.map(d => d.totalCost)));
    
    const getCostIntensity = (cost) => {
        if (!cost || maxDailyCost === 0) return 0;
        return Math.min(Math.max(cost / maxDailyCost, 0.1), 1);
    };
    
    const getIntensityColor = (intensity) => {
        if (intensity === 0) return 'bg-gray-800';
        if (intensity < 0.25) return 'bg-green-900/40';
        if (intensity < 0.5) return 'bg-yellow-900/40';
        if (intensity < 0.75) return 'bg-orange-900/40';
        return 'bg-red-900/40';
    };
    
    return (
        <div className="bg-gray-900 border border-gray-800 rounded-lg overflow-hidden">
            <div className="p-4 border-b border-gray-800">
                <div className="flex items-center justify-between mb-4">
                    <h3 className="text-lg font-semibold text-white flex items-center space-x-2">
                        <Calendar size={20} />
                        <span>Daily Cost Calendar</span>
                    </h3>
                    
                    <div className="flex items-center space-x-4">
                        {/* Resource Filter */}
                        <div className="flex items-center space-x-2">
                            <label className="text-sm text-gray-400">Filter by resource:</label>
                            <select
                                value={selectedResource || ''}
                                onChange={(e) => setSelectedResource(e.target.value || null)}
                                className="px-3 py-1 bg-gray-700 border border-gray-600 rounded text-white text-sm"
                            >
                                <option value="">All Resources</option>
                                {costData.items.map(item => (
                                    <option key={item.name} value={item.name}>{item.name}</option>
                                ))}
                            </select>
                        </div>
                        
                        {/* Month Navigation */}
                        <div className="flex items-center space-x-2">
                            <button
                                onClick={goToPreviousMonth}
                                disabled={currentMonthIndex === 0}
                                className="p-1 hover:bg-gray-700 rounded disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                <ChevronLeft size={20} className="text-white" />
                            </button>
                            
                            <span className="text-white font-medium min-w-[200px] text-center">
                                {currentMonth.monthName}
                            </span>
                            
                            <button
                                onClick={goToNextMonth}
                                disabled={currentMonthIndex === monthKeys.length - 1}
                                className="p-1 hover:bg-gray-700 rounded disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                <ChevronRight size={20} className="text-white" />
                            </button>
                        </div>
                    </div>
                </div>
                
                {/* Legend */}
                <div className="flex items-center space-x-4 text-xs text-gray-400">
                    <span>Cost intensity:</span>
                    <div className="flex items-center space-x-1">
                        <div className="w-3 h-3 bg-gray-800 rounded"></div>
                        <span>None</span>
                    </div>
                    <div className="flex items-center space-x-1">
                        <div className="w-3 h-3 bg-green-900/40 rounded"></div>
                        <span>Low</span>
                    </div>
                    <div className="flex items-center space-x-1">
                        <div className="w-3 h-3 bg-yellow-900/40 rounded"></div>
                        <span>Medium</span>
                    </div>
                    <div className="flex items-center space-x-1">
                        <div className="w-3 h-3 bg-orange-900/40 rounded"></div>
                        <span>High</span>
                    </div>
                    <div className="flex items-center space-x-1">
                        <div className="w-3 h-3 bg-red-900/40 rounded"></div>
                        <span>Highest</span>
                    </div>
                </div>
            </div>
            
            <div className="p-4">
                {/* Calendar Header */}
                <div className="grid grid-cols-7 gap-1 mb-2">
                    {['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'].map(day => (
                        <div key={day} className="p-2 text-center text-sm font-medium text-gray-400">
                            {day}
                        </div>
                    ))}
                </div>
                
                {/* Calendar Grid */}
                <div className="space-y-1">
                    {calendarGrid.map((week, weekIndex) => (
                        <div key={weekIndex} className="grid grid-cols-7 gap-1">
                            {week.map((day, dayIndex) => {
                                const displayCost = selectedResource 
                                    ? (day.dayData?.resourceCosts[selectedResource] || 0)
                                    : (day.dayData?.totalCost || 0);
                                    
                                const intensity = getCostIntensity(displayCost);
                                const intensityColor = getIntensityColor(intensity);
                                
                                return (
                                    <div
                                        key={dayIndex}
                                        className={`
                                            relative p-2 min-h-[60px] rounded border border-gray-700 transition-all duration-200
                                            ${
                                                day.isCurrentMonth 
                                                    ? `${intensityColor} hover:bg-gray-700` 
                                                    : 'bg-gray-800/50 opacity-50'
                                            }
                                            ${
                                                displayCost > 0 
                                                    ? 'cursor-pointer hover:border-yellow-600' 
                                                    : ''
                                            }
                                        `}
                                        title={
                                            day.isCurrentMonth && displayCost > 0
                                                ? `${day.date.getDate()} - ${formatCurrency(displayCost)}`
                                                : undefined
                                        }
                                    >
                                        {/* Day Number */}
                                        <div className={
                                            `text-sm font-medium ${
                                                day.isCurrentMonth ? 'text-white' : 'text-gray-500'
                                            }`
                                        }>
                                            {day.date.getDate()}
                                        </div>
                                        
                                        {/* Cost Display */}
                                        {day.isCurrentMonth && displayCost > 0 && (
                                            <div className="absolute bottom-1 left-1 right-1">
                                                <div className="flex items-center space-x-1">
                                                    <DollarSign size={8} className="text-yellow-400" />
                                                    <span className="text-xs text-white font-medium truncate">
                                                        {displayCost < 0.01 ? displayCost.toFixed(4) : displayCost.toFixed(2)}
                                                    </span>
                                                </div>
                                            </div>
                                        )}
                                    </div>
                                );
                            })}
                        </div>
                    ))}
                </div>
                
                {/* Summary */}
                <div className="mt-4 pt-4 border-t border-gray-700">
                    <div className="flex items-center justify-between text-sm">
                        <span className="text-gray-400">
                            {selectedResource ? `${selectedResource} costs for` : 'Total costs for'} {currentMonth.monthName}
                        </span>
                        <span className="text-white font-medium">
                            {formatCurrency(
                                currentMonth.days.reduce((sum, day) => {
                                    return sum + (selectedResource 
                                        ? (day.resourceCosts[selectedResource] || 0)
                                        : day.totalCost);
                                }, 0)
                            )}
                        </span>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default DailyCostCalendar;