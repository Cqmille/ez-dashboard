// ========== CONFIGURATION ==========
const REFRESH_TIME_INTERVAL = 1000;      // 1 seconde
const REFRESH_EVENTS_INTERVAL = 60000;   // 1 minute
const REFRESH_MESSAGES_INTERVAL = 30000; // 30 secondes

// ========== ELEMENTS ==========
const timeEl = document.getElementById('time');
const momentEl = document.getElementById('moment');
const dateEl = document.getElementById('date');
const todayEventsEl = document.getElementById('today-events');
const tomorrowEventsEl = document.getElementById('tomorrow-events');
const messagesContainer = document.getElementById('messages-container');

// ========== FONCTIONS ==========

async function updateTime() {
    try {
        const response = await fetch('/api/time');
        const data = await response.json();
        
        timeEl.textContent = data.time;
        momentEl.textContent = data.moment;
        dateEl.textContent = data.date;
        
        // Gestion du mode sombre
        if (data.isDarkMode) {
            document.body.classList.add('dark-mode');
        } else {
            document.body.classList.remove('dark-mode');
        }
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
                <li>
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
            messagesContainer.innerHTML = '';
            return;
        }
        
        messagesContainer.innerHTML = messages.map(msg => `
            <div class="message-card">
                <div class="message-content">💬 "${msg.content}"</div>
                <div class="message-meta">— ${msg.author}, ${msg.timeAgo}</div>
            </div>
        `).join('');
    } catch (error) {
        console.error('Erreur updateMessages:', error);
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
