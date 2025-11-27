# Ravuno

Ravuno combines "ravn" (raven in Nordic mythology—Odin's information bringer) with "uno" (one single place to do it). A unified service for fetching, filtering, and delivering activity information.

## Quick Start

```bash
docker compose up
```

Ensure `docker-compose.yml` is properly configured with your settings.

## Available Information Sources

- **Tekna** - Technical and scientific activities
- **DNT Activities** - Norwegian Trekking Association events

## Custom Data Filters

Copy `.sql` files into `/app/config/updates` of the docker container to define custom data filters and email recipients.

### Example Filter

<!--cspell:ignore Arrang julianday mbassadør rgruppe -->

```sql
-- DNT Aktiviteter: Multi day trip in Norway
-- receiver@example.com
SELECT *
FROM Items
WHERE Source = 'DntActivities'
  AND (Tags REGEXP 'Passer spesielt for:[^,]*\|(Voksne|Fjellsportsinteresserte)\|' 
    OR Tags NOT REGEXP 'Passer spesielt for:[^,]*\|(Barn|Utviklingshemmede|Funksjonshemmede|Ungdom|Seniorer)\|')
  AND Title NOT REGEXP '[aA]mbassadør[-\s]?kurs|Ambassador's course'
  AND Tags NOT REGEXP 'Arrang\\u00F8rgruppe:[^,]*\|Barnas Turlag\|'
  AND Tags NOT REGEXP 'Kategori:[^,]*\|Kurs\|'
  AND (julianday(EventEndDateTime) - julianday(EventStartDateTime)) > 1
```


