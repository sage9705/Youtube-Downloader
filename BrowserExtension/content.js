function injectStyles() {
    if (document.getElementById('ytp-download-styles')) return;

    const style = document.createElement('style');
    style.id = 'ytp-download-styles';
    style.innerHTML = `
        .ytp-download-btn {
            background: linear-gradient(135deg, #ff3366 0%, #ff9933 100%);
            color: #ffffff;
            border: none;
            border-radius: 18px;
            padding: 0 16px;
            height: 36px;
            width: auto;
            flex: none;
            font-size: 14px;
            font-weight: 600;
            cursor: pointer;
            margin-right: 8px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-family: "Roboto", "Inter", sans-serif;
            transition: all 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
            box-shadow: 0 4px 12px rgba(255, 51, 102, 0.25);
            gap: 6px;
        }
        .ytp-download-btn svg {
            width: 18px;
            height: 18px;
            fill: currentColor;
            transition: transform 0.3s ease;
        }
        .ytp-download-btn:hover {
            background: linear-gradient(135deg, #ff1a53 0%, #ff8c1a 100%);
            box-shadow: 0 6px 16px rgba(255, 51, 102, 0.4);
            transform: translateY(-2px);
        }
        .ytp-download-btn:hover svg {
            transform: translateY(2px);
        }
        .ytp-download-btn:active {
            transform: translateY(1px);
            box-shadow: 0 2px 8px rgba(255, 51, 102, 0.3);
        }
        .ytp-download-btn.sent {
            background: linear-gradient(135deg, #00C9FF 0%, #92FE9D 100%) !important;
            box-shadow: 0 4px 15px rgba(0, 201, 255, 0.3) !important;
            animation: ytpPulse 0.5s cubic-bezier(0.175, 0.885, 0.32, 1.275);
        }
        @keyframes ytpPulse {
            0% { transform: scale(1); }
            50% { transform: scale(1.1); }
            100% { transform: scale(1); }
        }
    `;
    document.head.appendChild(style);
}

function sendUrlToApp() {
    const url = window.location.href;
    console.log("Sending URL to YTP Downloader:", url);
    
    fetch('http://127.0.0.1:44555/', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({ url: url })
    })
    .then(response => {
        if (response.ok) {
            const btn = document.getElementById('ytp-download-btn');
            if(btn) {
                const originalHtml = btn.innerHTML;
                btn.innerHTML = `<svg viewBox="0 0 24 24"><path d="M9 16.2L4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2z"/></svg> <span>Sent!</span>`;
                btn.classList.add('sent');
                setTimeout(() => {
                    btn.innerHTML = originalHtml;
                    btn.classList.remove('sent');
                }, 2000);
            }
        } else {
            console.error("YTP Downloader responded with status:", response.status);
        }
    })
    .catch(err => {
        console.error("Error communicating with YTP Downloader. Is the app running?", err);
        alert("Could not reach YTP Downloader. Make sure the app is running.");
    });
}

function injectButton() {
    if (document.getElementById('ytp-download-btn')) {
        return true;
    }

    const menuContainers = [
        document.querySelector('#top-level-buttons-computed'),
        document.querySelector('#flexible-item-buttons'),
        document.querySelector('ytd-menu-renderer #top-level-buttons'),
        document.querySelector('#actions-inner')
    ];
                          
    let menuContainer = null;
    for (let container of menuContainers) {
        if (container) {
            menuContainer = container;
            break;
        }
    }

    if (!menuContainer) {
        return false;
    }

    injectStyles();

    const btn = document.createElement('button');
    btn.id = 'ytp-download-btn';
    btn.className = 'ytp-download-btn';
    btn.innerHTML = `<svg viewBox="0 0 24 24"><path d="M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"/></svg> <span>YTP Download</span>`;

    btn.addEventListener('click', (e) => {
        e.preventDefault();
        e.stopPropagation();
        sendUrlToApp();
    });

    menuContainer.insertBefore(btn, menuContainer.firstChild);
    return true;
}

setInterval(() => {
    if (window.location.pathname === '/watch' || window.location.pathname === '/playlist') {
        injectButton();
    }
}, 1500);
