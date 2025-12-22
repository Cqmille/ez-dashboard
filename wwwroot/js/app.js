// ========== CONFIGURATION ==========
const REFRESH_TIME_INTERVAL = 1000;      // 1 seconde
const REFRESH_EVENTS_INTERVAL = 60000;   // 1 minute
const REFRESH_MESSAGES_INTERVAL = 30000; // 30 secondes
const REFRESH_COOLDOWN = 30;             // 30 secondes de cooldown

// ========== ELEMENTS ==========
const timeEl = document.getElementById('time');
const momentEl = document.getElementById('moment');
const dateEl = document.getElementById('date');
const todayEventsEl = document.getElementById('today-events');
const tomorrowEventsEl = document.getElementById('tomorrow-events');
const messageBadgesEl = document.getElementById('message-badges');
const refreshBtn = document.getElementById('refresh-btn');

// ========== STATE ==========
let refreshCooldown = 0;

// ========== FONCTIONS ==========

async function updateTime() {
    try {
        const response = await fetch('/api/time');
        const data = await response.json();

        timeEl.textContent = data.time;
        momentEl.textContent = data.moment;
        dateEl.textContent = data.date;
    } catch (error) {
        console.error('Erreur updateTime:', error);
    }
}

async function updateEvents() {
    try {
        const response = await fetch('/api/events');
        const data = await response.json();

        // Aujourd'hui
        if (data.today && data.today.length > 0) {
            todayEventsEl.innerHTML = data.today.map(evt => `
                <li class="${evt.isPast ? 'past' : ''}">
                    <span class="event-time">${evt.time}</span>
                    <span class="event-title">${evt.title}</span>
                </li>
            `).join('');
        } else {
            todayEventsEl.innerHTML = '<li class="no-events">Aucun événement</li>';
        }

        // Demain
        if (data.tomorrow && data.tomorrow.length > 0) {
            tomorrowEventsEl.innerHTML = data.tomorrow.map(evt => `
                <li>
                    <span class="event-time">${evt.time}</span>
                    <span class="event-title">${evt.title}</span>
                </li>
            `).join('');
        } else {
            tomorrowEventsEl.innerHTML = '<li class="no-events">Aucun événement</li>';
        }
    } catch (error) {
        console.error('Erreur updateEvents:', error);
        todayEventsEl.innerHTML = '<li class="no-events">Erreur de chargement</li>';
    }
}

async function updateMessages() {
    try {
        const response = await fetch('/api/messages');
        const messages = await response.json();

        if (messages.length === 0) {
            messageBadgesEl.innerHTML = '';
            return;
        }

        // Afficher max 3 messages en badges (haut droite)
        messageBadgesEl.innerHTML = messages.slice(0, 3).map(msg => `
            <div class="message-badge" data-id="${msg.id}">
                <span class="message-badge-icon">💬</span>
                <div class="message-badge-content">
                    <div class="message-badge-text">${msg.content}</div>
                    <div class="message-badge-author">— ${msg.author}</div>
                </div>
                <button class="message-badge-close" onclick="deleteMessage(${msg.id})" title="Supprimer">✕</button>
            </div>
        `).join('');
    } catch (error) {
        console.error('Erreur updateMessages:', error);
    }
}

async function deleteMessage(id) {
    const pin = prompt('Entrez le code PIN admin pour supprimer ce message :');
    if (!pin) return;

    try {
        const response = await fetch(`/api/messages/${id}`, {
            method: 'DELETE',
            headers: {
                'X-Admin-Pin': pin
            }
        });

        if (response.ok) {
            updateMessages();
        } else if (response.status === 401) {
            alert('Code PIN incorrect !');
        } else {
            alert('Erreur lors de la suppression');
        }
    } catch (error) {
        console.error('Erreur deleteMessage:', error);
        alert('Erreur de connexion');
    }
}

// ========== INITIALISATION ==========

// Premier chargement
updateTime();
updateEvents();
updateMessages();

// Rafraîchissements périodiques
setInterval(updateTime, REFRESH_TIME_INTERVAL);
setInterval(updateEvents, REFRESH_EVENTS_INTERVAL);
setInterval(updateMessages, REFRESH_MESSAGES_INTERVAL);

// Empêcher le zoom sur mobile
document.addEventListener('gesturestart', e => e.preventDefault());
document.addEventListener('gesturechange', e => e.preventDefault());

// ========== REFRESH MANUEL ==========

function manualRefresh() {
    if (refreshCooldown > 0) return;

    // Lancer les mises à jour
    updateEvents();
    updateMessages();

    // Démarrer le cooldown
    refreshCooldown = REFRESH_COOLDOWN;
    refreshBtn.disabled = true;
    updateRefreshButton();

    const interval = setInterval(() => {
        refreshCooldown--;
        updateRefreshButton();

        if (refreshCooldown <= 0) {
            clearInterval(interval);
            refreshBtn.disabled = false;
            refreshBtn.textContent = '🔄';
        }
    }, 1000);
}

function updateRefreshButton() {
    if (refreshCooldown > 0) {
        refreshBtn.textContent = `⏳ ${refreshCooldown}s`;
    }
}

// ========== UI CUSTOMIZER ==========

const settingsBtn = document.getElementById('settings-btn');
const uiMenu = document.getElementById('ui-menu');

// Toggle menu visibility
settingsBtn.addEventListener('click', () => {
    const isVisible = uiMenu.style.display !== 'none';
    uiMenu.style.display = isVisible ? 'none' : 'block';
    settingsBtn.classList.toggle('active', !isVisible);
});

// Configuration des contrôles UI
const uiControls = [
    // Heure
    { id: 'time-size', cssVar: '--time-size', unit: 'rem', type: 'range' },
    { id: 'time-color', cssVar: '--time-color', unit: '', type: 'color' },
    // Moment
    { id: 'moment-size', cssVar: '--moment-size', unit: 'rem', type: 'range' },
    { id: 'moment-color', cssVar: '--moment-color', unit: '', type: 'color' },
    // Date
    { id: 'date-size', cssVar: '--date-size', unit: 'rem', type: 'range' },
    { id: 'date-color', cssVar: '--date-color', unit: '', type: 'color' },
    // Événements
    { id: 'event-size', cssVar: '--event-font-size', unit: 'rem', type: 'range' },
    // Verre
    { id: 'glass-opacity', cssVar: '--glass-bg-opacity', unit: '', type: 'range', special: 'opacity' },
    { id: 'glass-blur', cssVar: '--glass-blur', unit: 'px', type: 'range' },
    // Accents
    { id: 'accent-today', cssVar: '--accent-today', unit: '', type: 'color' },
    { id: 'accent-tomorrow', cssVar: '--accent-tomorrow', unit: '', type: 'color' },
    // Titres
    { id: 'title-size', cssVar: '--title-font-size', unit: 'rem', type: 'range' }
];

// Fonction de mise à jour en temps réel
function updateCSSVar(control, value) {
    const root = document.documentElement;

    if (control.special === 'opacity') {
        // Mise à jour spéciale pour l'opacité du verre
        root.style.setProperty('--glass-bg', `rgba(0, 0, 0, ${value})`);
        root.style.setProperty('--glass-bg-hover', `rgba(0, 0, 0, ${parseFloat(value) + 0.1})`);
    } else {
        root.style.setProperty(control.cssVar, value + control.unit);
    }
}

// Fonction pour mettre à jour le label de valeur
function updateValueLabel(control, value) {
    const valueEl = document.getElementById(`val-${control.id}`);
    if (valueEl) {
        if (control.type === 'color') {
            valueEl.textContent = value.toUpperCase();
        } else {
            valueEl.textContent = value + control.unit;
        }
    }
}

// Initialiser tous les contrôles
uiControls.forEach(control => {
    const inputEl = document.getElementById(`ctrl-${control.id}`);
    if (!inputEl) return;

    // Écouter les changements
    inputEl.addEventListener('input', (e) => {
        const value = e.target.value;
        updateCSSVar(control, value);
        updateValueLabel(control, value);
    });

    // Pour les color pickers, aussi écouter 'change' pour compatibilité
    if (control.type === 'color') {
        inputEl.addEventListener('change', (e) => {
            const value = e.target.value;
            updateCSSVar(control, value);
            updateValueLabel(control, value);
        });
    }
});
