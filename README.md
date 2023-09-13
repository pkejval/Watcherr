# Watcherr
This project was inspired by https://github.com/MattDGTL/sonarr-radarr-queue-cleaner

Watcherr is "hopefully" better. It can manage unlimited number of Radarr/Sonarr instances each with own settings.

### What it is for?
Watcherr can watch at your Radarr/Sonarr instances and can:
* Delete unmonitored shows from collection
* Delete stalled downloads (with configurable threshold of % already downloaded) and queued downloads with size of 0 (probably not seeded at all).

# How to use it?
## Docker
Just pull it from DockerHub *pkejval/watcherr:tagname* set Environment variables, start it and let it do its job.
## Linux/Windows
* Clone repo
* Call "dotnet publish -c release" in repo directory
* Set envirnoment variables to configure app or create .env file in app root folder.
* Run app

# Configuration
Configuration is done with Envirnoment variables set before Watcherr is started.

## Variables
### INTERVAL (default 600)
How often (in seconds) should Watcherr check.

### APIS
**This variable contains list of all instances which will be watched. You can set any name you want. You'll use these names as prefix for their configuration variables. I'll be refering to this variable as "INSTANCE" below.**

Example: APIS=RADARR1,RADARR2,SONARR

### INSTANCE_URL
URL to instance. **MUST** include URL Base if set in Radarr/Sonarr instance.

Examples without URL base set:
* INSTANCE_URL=https://radarr.domain.tld
* INSTANCE_URL=http://127.0.0.1:7878

Examples with URL base set:  
* INSTANCE_URL=https://radarr.domain.tld/radarr
* INSTANCE_URL=http://127.0.0.1:7878/radarr

### INSTANCE_KEY
Your API key. You can get it in **Settings/General/Api Key** in your instance.

### INSTANCE_UNMONITORED_REMOVE (default false)
Toggles if Watcherr will remove unmonitored shows from collection.

### INSTANCE_UNMONITORED_DELETE_FILES (default false)
Toggles if Watcherr will remove files when removing unmonitored shows from collection. Needs **INSTANCE_UNMONITORED_REMOVE** to be turn on otherwise won't do anything.

### INSTANCE_STALLED_REMOVE (default false)
Toggles if Watcherr will watch and remove stalled and queued (threshold is set by **INSTANCE_STALLED_REMOVE_PERCENT_THRESHOLD**) downloads.

### INSTANCE_STALLED_REMOVE_FROM_CLIENT (default true)
Toggles if instance should remove file from download client when removing show from queue.

### INSTANCE_STALLED_BLOCKLIST_RELEASE (default false)
Toggles if instance should blocklist release when removing show from queue.

### INSTANCE_STALLED_REMOVE_PERCENT_THRESHOLD (default 2)
When Watcherr finds stalled show with % downloaded greater than value of this variable, Watcherr won't remove show from queue nor from download client.

## Configuration example
```
INTERVAL=600

APIS=RADARR1,RADARR2,SONARR

RADARR1_URL=https://radarr1.domain.tld/radarr
RADARR1_KEY=yourkey
RADARR1_UNMONITORED_REMOVE=true
RADARR1_STALLED_REMOVE=true
RADARR1_STALLED_REMOVE_FROM_CLIENT=true

RADARR2_URL=https://radarr2.domain.tld/radarr
RADARR2_KEY=yourkey
RADARR2_UNMONITORED_REMOVE=true
RADARR2_STALLED_REMOVE=false

SONARR_URL=https://sonarr.domain.tld/sonarr
SONARR_KEY=yourkey
SONARR_STALLED_REMOVE=true
SONARR_STALLED_REMOVE_FROM_CLIENT=true
```