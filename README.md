# Mamy Dashboard

Tableau d'affichage pour personnes âgées avec :
- 🕐 Heure et date en gros
- 📅 Événements du jour et du lendemain (Google Calendar)
- 💬 Messages de la famille
- 🌙 Mode sombre automatique (20h-7h)

## Installation locale

```bash
cd MamyDashboard
dotnet restore
dotnet run
```

Ouvre http://localhost:5000

## Configuration

### 1. appsettings.json

```json
{
  "AppSettings": {
    "AdminPin": "1234",
    "GoogleCalendarId": "ton-calendar-id@group.calendar.google.com",
    "GoogleCredentialsPath": "credentials.json"
  }
}
```

### 2. Google Calendar API

1. Va sur https://console.cloud.google.com/
2. Crée un projet "MamyDashboard"
3. Active l'API "Google Calendar API"
4. Va dans "Identifiants" → "Créer des identifiants" → "Compte de service"
5. Télécharge le fichier JSON et renomme-le `credentials.json`
6. Place-le dans le dossier `MamyDashboard/`

### 3. Créer et partager l'agenda

1. Va sur https://calendar.google.com/
2. Crée un nouvel agenda "Mamy"
3. Dans les paramètres de l'agenda, copie l'"ID de l'agenda" (format: xxx@group.calendar.google.com)
4. Partage l'agenda avec l'email du compte de service (visible dans le JSON, champ `client_email`)
5. Colle l'ID dans `appsettings.json`

## Déploiement VPS

```bash
# Sur le VPS
cd /var/www/apps
git clone https://github.com/TON_USER/mamy-dashboard.git
cd mamy-dashboard/MamyDashboard

# Copier le credentials.json
# Modifier appsettings.json

dotnet build -c Release

# Créer le service systemd (voir README-deploy.md)
```

## URLs

- `/` - Page tablette (affichage principal)
- `/admin.html` - Interface famille (envoyer messages)

## Mode Kiosk Android

1. Installe "Fully Kiosk Browser" sur la tablette
2. Configure l'URL de démarrage
3. Active le mode kiosk
