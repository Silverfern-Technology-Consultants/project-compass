// Custom JavaScript for Swagger UI Dark Theme
// Adds Silverfern branding and enhanced dark theme functionality

document.addEventListener('DOMContentLoaded', function() {
    // Add custom title with Silverfern branding
    const titleElement = document.querySelector('.info .title');
    if (titleElement) {
        titleElement.innerHTML = '🌿 Compass API - Silverfern Technology Consultants';
    }

    // Add custom description enhancement
    const descriptionElement = document.querySelector('.info .description p');
    if (descriptionElement) {
        descriptionElement.innerHTML += '<br><br><strong style="color: #c7ae6a;">Built with expertise in MSP governance and Azure security assessment.</strong>';
    }

    // Enhanced authorization section
    const authBtn = document.querySelector('.auth-wrapper .authorize');
    if (authBtn) {
        authBtn.addEventListener('click', function() {
            setTimeout(() => {
                const modal = document.querySelector('.dialog-ux');
                if (modal) {
                    modal.style.backgroundColor = '#242424';
                    modal.style.border = '2px solid #c7ae6a';
                }
            }, 100);
        });
    }

    // Add theme toggle functionality (optional)
    addThemeToggle();

    // Enhance response highlighting
    enhanceResponseHighlighting();

    // Add keyboard shortcuts info
    addKeyboardShortcuts();
});

function addThemeToggle() {
    const topbar = document.querySelector('.swagger-ui .topbar');
    if (topbar) {
        const themeToggle = document.createElement('button');
        themeToggle.innerHTML = '🌙 Dark';
        themeToggle.style.cssText = `
            background: #c7ae6a;
            color: #202020;
            border: none;
            padding: 8px 16px;
            border-radius: 4px;
            font-weight: bold;
            cursor: pointer;
            margin-left: 10px;
            position: absolute;
            right: 20px;
            top: 50%;
            transform: translateY(-50%);
        `;
        
        themeToggle.addEventListener('click', function() {
            // Theme is always dark for this implementation
            this.innerHTML = '🌙 Dark Mode Active';
            setTimeout(() => {
                this.innerHTML = '🌙 Dark';
            }, 2000);
        });
        
        topbar.style.position = 'relative';
        topbar.appendChild(themeToggle);
    }
}

function enhanceResponseHighlighting() {
    // Monitor for response updates and enhance styling
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            if (mutation.addedNodes) {
                mutation.addedNodes.forEach(function(node) {
                    if (node.nodeType === 1 && node.classList && node.classList.contains('response')) {
                        enhanceResponseSection(node);
                    }
                });
            }
        });
    });

    observer.observe(document.body, {
        childList: true,
        subtree: true
    });
}

function enhanceResponseSection(responseElement) {
    // Add success/error indicators with Silverfern styling
    const statusCode = responseElement.querySelector('.response-col_status');
    if (statusCode) {
        const code = statusCode.textContent.trim();
        if (code.startsWith('2')) {
            responseElement.style.borderLeft = '4px solid #6bff6b';
        } else if (code.startsWith('4') || code.startsWith('5')) {
            responseElement.style.borderLeft = '4px solid #ff6b6b';
        } else {
            responseElement.style.borderLeft = '4px solid #c7ae6a';
        }
    }
}

function addKeyboardShortcuts() {
    document.addEventListener('keydown', function(e) {
        // Alt + T: Focus on Try it out button
        if (e.altKey && e.key === 't') {
            e.preventDefault();
            const tryOutBtn = document.querySelector('.try-out__btn');
            if (tryOutBtn) {
                tryOutBtn.click();
                tryOutBtn.focus();
            }
        }

        // Alt + E: Focus on Execute button
        if (e.altKey && e.key === 'e') {
            e.preventDefault();
            const executeBtn = document.querySelector('.execute');
            if (executeBtn && executeBtn.style.display !== 'none') {
                executeBtn.click();
            }
        }

        // Alt + A: Open Authorization
        if (e.altKey && e.key === 'a') {
            e.preventDefault();
            const authBtn = document.querySelector('.authorize');
            if (authBtn) {
                authBtn.click();
            }
        }
    });

    // Add keyboard shortcuts help
    const infoSection = document.querySelector('.info');
    if (infoSection) {
        const shortcutsDiv = document.createElement('div');
        shortcutsDiv.style.cssText = `
            margin-top: 15px;
            padding: 10px;
            background: #2a2a2a;
            border-radius: 4px;
            border-left: 4px solid #c7ae6a;
            font-size: 12px;
        `;
        shortcutsDiv.innerHTML = `
            <strong style="color: #c7ae6a;">Keyboard Shortcuts:</strong><br>
            <span style="color: #e8e8e8;">
                Alt + T: Try it out | Alt + E: Execute | Alt + A: Authorize
            </span>
        `;
        infoSection.appendChild(shortcutsDiv);
    }
}

// Add loading state management
window.addEventListener('load', function() {
    // Ensure all custom styling is applied after full load
    setTimeout(() => {
        const allButtons = document.querySelectorAll('.swagger-ui .btn');
        allButtons.forEach(btn => {
            if (!btn.style.transition) {
                btn.style.transition = 'all 0.3s ease';
            }
        });
    }, 500);
});