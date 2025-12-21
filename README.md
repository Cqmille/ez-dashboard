# Mamy Dashboard

Tableau d'affichage pour personnes âgées avec :
- 🕐 Heure et date en gros
- 📅 Événements du jour et du lendemain (via URL iCal)
- 💬 Messages de la famille
- 🌙 Mode sombre automatique (20h-7h)
- 🔄 Bouton refresh manuel (avec cooldown de 30s)

## Installation locale

```bash
dotnet restore
dotnet run
```

Ouvre http://localhost:5000

## Configuration

### appsettings.json

```json
{
  "AppSettings": {
    "AdminPin": "1234",
    "ICalUrl": "https://calendar.google.com/calendar/ical/xxx/basic.ics"
  }
}
```

### Récupérer l'URL iCal de Google Calendar

1. Va sur https://calendar.google.com/
2. Clique sur l'engrenage ⚙️ → **Paramètres**
3. Dans le menu de gauche, clique sur ton agenda
4. Descends jusqu'à **Adresse secrète au format iCal**
5. Copie l'URL (format: `https://calendar.google.com/calendar/ical/.../basic.ics`)
6. Colle cette URL dans `appsettings.json` → `ICalUrl`

> ⚠️ **Important** : Utilise l'adresse **secrète** (pas l'adresse publique) pour accéder aux événements privés.

## Déploiement VPS

```bash
# Sur le VPS
cd /var/www/apps
git clone https://github.com/TON_USER/mamy-dashboard.git
cd mamy-dashboard

# Modifier appsettings.json avec l'URL iCal
dotnet build -c Release

# Créer le service systemd (voir ci-dessous)
```

### Service systemd

```ini
[Unit]
Description=Mamy Dashboard
After=network.target

[Service]
WorkingDirectory=/var/www/apps/mamy-dashboard
ExecStart=/usr/bin/dotnet run --urls "http://localhost:5000"
Restart=always
User=www-data

[Install]
WantedBy=multi-user.target
```

## URLs

- `/` - Page tablette (affichage principal)
- `/admin.html` - Interface famille (envoyer messages)

## Mode Kiosk Android

1. Installe "Fully Kiosk Browser" sur la tablette
2. Configure l'URL de démarrage
3. Active le mode kiosk
