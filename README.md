# AccessGuard .NET — Identity, EF Core et SQLite

## Premier lancement local
1. Ouvrir AccessGuard.slnx dans Visual Studio avec le SDK .NET 10.
2. Restaurer les packages NuGet puis démarrer le profil http (Ctrl+F5).
3. Ouvrir http://localhost:5291. L’écran d’initialisation propose de créer le premier administrateur.
4. Choisir un mot de passe personnel : 12 caractères minimum, majuscule, minuscule, chiffre et symbole.
5. Se connecter, créer les comptes depuis Utilisateurs, puis associer Google Authenticator dans Sécurité 2FA.
6. Conserver les dix codes de récupération affichés une seule fois après activation.

Aucun mot de passe administrateur par défaut. Les anciennes données de démonstration ne sont pas importées et leur ancien stockage navigateur est effacé. Les migrations EF Core sont appliquées au démarrage. Pas de serveur SQL à installer.

## Stockage et configuration
- App_Data/accessguard.db : base SQLite.
- App_Data/keys : clés Data Protection, protégées par DPAPI sous Windows.
- AccessGuard__DataPath : variable d’environnement pour déplacer la base et les clés.
- ConnectionStrings__Default : facultatif, remplace la connexion SQLite.
- Bootstrap__Email, Bootstrap__Password et Bootstrap__Name : facultatifs pour initialiser le premier compte par configuration. En production vide, ces variables sont obligatoires ; l’initialisation via navigateur est désactivée.

Ne pas mettre ces valeurs sensibles dans Git. App_Data, fichiers de base, bin/obj et fichiers utilisateur sont exclus de Git et de l’archive source. Sauvegarder la base ET les clés Data Protection ; les clés DPAPI Windows sont liées au compte système qui exécute l’application.

## Autorisations
Trois rôles : Administrateur, Auditeur et Employé. Six permissions : users.read, users.write, roles.assign, audit.read, audit.export, roles.write. L’administrateur possède toutes les permissions protégées ; l’auditeur lit les utilisateurs et le journal, et peut exporter ; l’employé n’accède initialement qu’à son espace, son profil et sa 2FA. La matrice adapte les permissions des deux autres rôles. Les contrôles sont exécutés côté serveur sur chaque requête, sans se fier aux boutons affichés.

Désactiver un compte ou modifier son rôle invalide ses sessions via le security stamp. Les changements de matrice sont consultés sur chaque requête. La modification de comptes utilise un jeton de concurrence ; la suppression de son propre rôle administrateur et la perte du dernier administrateur actif sont refusées.

## Authentification et sécurité
- Mots de passe hachés par Identity ; cookies HttpOnly, SameSite=Lax, expiration glissante de 30 minutes et Secure en production.
- Cinq échecs de connexion : verrouillage temporaire de quinze minutes.
- Dix échecs depuis la même IP en cinq minutes déclenchent une alerte dans Audit & SOC et un blocage de quinze minutes sur la connexion et la vérification 2FA. Le blocage expire automatiquement et l’API renvoie HTTP 429 avec `Retry-After` pendant sa durée. Les alertes sont enregistrées dans SQLite et la protection reprend après un redémarrage avec la même configuration.
- La section `AccessGuard:BruteForce` permet de définir `MaxFailures`, `WindowSeconds` et `BlockSeconds`. Les valeurs par défaut sont `10`, `300` et `900`.
- Quatre échecs de vérification du mot de passe ou de la 2FA visant le même compte depuis au moins trois IP dans cette fenêtre déclenchent une alerte de brute force distribuée et un verrouillage Identity du compte. `DistributedMaxFailures` et `DistributedMinIps` règlent ces seuils. La durée suit `BlockSeconds`.
- Des mots de passe incorrects visant cinq comptes connus distincts depuis la même IP dans la fenêtre configurée déclenchent une alerte **Password spraying suspecté** et le blocage temporaire de cette IP. `SprayMinAccounts` règle le seuil. Le signal repose sur les cibles et la fréquence des échecs.
- Limitation de débit : 60 requêtes/minute/IP sur les endpoints de connexion et les opérations sensibles du compte ; dépassement = HTTP 429.
- Protection antiforgery sur les opérations POST/PUT ; jeton obtenu via /api/auth/session et transmis dans X-CSRF-TOKEN.
- Authenticator/TOTP vérifié par Identity, flux de mot de passe puis second facteur ; aucun cookie de session complet avant la vérification du second facteur.
- Codes de récupération utilisables une fois, affichés uniquement à l’activation.
- Les valeurs des tokens Identity (clé authenticator et codes de récupération) sont protégées par Data Protection dans la base.
- Le profil et la préparation/désactivation 2FA demandent le mot de passe actuel ; la désactivation demande aussi le code courant.
- Aucun secret ni mot de passe dans localStorage. sessionStorage sert à l’introduction visuelle uniquement.

## Journal d’audit
Connexions, échecs, verrouillages, déconnexions, modifications de comptes/permissions, configuration 2FA, exports et refus d’autorisation API. Chaque événement contient la date UTC, l’acteur, le type d’action, la ressource, le résultat et l’IP de la requête. Aucun mot de passe, secret TOTP ou code de récupération n’est journalisé.

Journal en ajout seul dans le code EF et sans endpoint de modification/suppression. L’administrateur de la base ou du système peut néanmoins modifier le fichier : ce journal n’est pas une preuve d’intégrité cryptographique ou une certification de conformité. Les listes renvoient au maximum 500 événements après filtrage ; l’export est limité à 10 000. Les filtres de dates portent sur des journées UTC.

Les alertes brute force et les adresses IP sont visibles dans le journal, recherchables et incluses dans l’export CSV. L’IP est celle observée par le serveur. En cas de déploiement derrière un reverse proxy, configurer le traitement des en-têtes transférés avec les adresses de proxy de confiance avant d’exposer l’application.

Le centre de sécurité d’Audit & SOC présente sept journées UTC d’activité de connexion, les alertes, la couverture 2FA des comptes actifs et des administrateurs, ainsi que les seuils en vigueur. Il affiche les blocages en cours et leur expiration. La file d’incidents permet de rechercher et de filtrer les alertes par gravité et statut. Chaque détail présente jusqu’à 100 tentatives. Les nouvelles alertes enregistrent la fenêtre de corrélation. Les alertes précédentes utilisent la fenêtre actuellement configurée. Les verrouillages Identity ordinaires affichent les tentatives récentes du compte.

La consultation requiert `audit.read`. Un administrateur autorisé à consulter l’audit peut régler la gravité et le statut, prendre en charge un incident et ajouter une note. La résolution demande une note. Le journal d’audit et l’historique de triage conservent chaque mise à jour. La fermeture administrative d’un incident reflète le triage. Le compte à rebours de blocage dépend de son expiration.

## Tests d’intégration
Les tests créent une base séparée sous tests/.data et ne touchent pas à votre base de travail.

```text
dotnet build AccessGuard/AccessGuard.csproj
npm install --prefix tests
npm test --prefix tests
npm run test:brute-force --prefix tests
```

Sous Windows, Brave est utilisé par défaut. Définir BROWSER_PATH pour un autre exécutable Chromium/Edge, ou installer Chromium de Playwright si aucun navigateur local n’est configuré. DOTNET_EXE peut préciser le chemin du SDK. Résultats dans tests/results.

Vérifications : initialisation unique, CSRF, cookies, droits employés/auditeurs/admins, concurrence, révocation de session, modification immédiate des permissions, verrouillage, audit, TOTP, codes de récupération non réutilisables, formulaires de création et configuration 2FA, déconnexion et persistance après redémarrage.

## Limites restantes avant production
Pas d’envoi d’e-mails, de récupération de mot de passe par e-mail, de politique de conservation automatisée, de sauvegarde planifiée, d’alertes SIEM, de multi-organisation ou de journal externe immuable. L’activation 2FA est disponible mais n’est pas encore imposée à tous les rôles. HTTPS et les secrets de configuration doivent être mis en place sur l’hébergement. SQLite convient au projet et à un déploiement limité, pas à une architecture multi-serveur sans adaptation.

## Références
- https://learn.microsoft.com/aspnet/core/security/authentication/identity-enable-qrcodes
- https://learn.microsoft.com/aspnet/core/security/anti-request-forgery
- https://learn.microsoft.com/ef/core/providers/sqlite/

Polices IBM Plex : licence SIL OFL dans wwwroot/fonts/OFL.txt. QR : node-qrcode/dijkstrajs, licences MIT dans wwwroot/js/vendor/qrcode.LICENSE.txt.
