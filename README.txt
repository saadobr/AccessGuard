ACCESSGUARD .NET — GUIDE DE REPRISE DU PROJET
État du projet : 15 novembre 2026
============================================================

1. À QUOI SERT LE PROJET ?
------------------------------------------------------------
AccessGuard est un portail de gestion des identités et des accès (IAM),
réalisé pour un projet académique .NET.

Il permet à une entreprise, une école ou une association de gérer ses
utilisateurs, leurs rôles, leurs permissions et la double authentification,
puis de consulter les événements de sécurité dans un journal d'audit.

Public principal : administrateurs et personnes chargées de la sécurité.
Les auditeurs et les employés disposent d'un accès adapté à leur rôle.

Actuellement, une installation correspond à UNE organisation. Le texte
« Organisation AccessGuard » est un libellé de l'interface ; il n'existe
pas encore de création ni de sélection de plusieurs organisations.


2. CE QUI A ÉTÉ RÉALISÉ DU DÉBUT À MAINTENANT
------------------------------------------------------------
A. Structure du projet
- Application ASP.NET Core MVC avec des vues Razor (.cshtml).
- Séparation entre pages, contrôleurs, données, services et sécurité.
- Pages : vue d'ensemble, utilisateurs, permissions, audit, connexion,
  compte personnel, configuration 2FA et initialisation administrateur.

B. Interface et expérience utilisateur
- Plusieurs pistes visuelles ont été explorées : univers cybersécurité
  sombre, verre dépoli et effets inspirés des interfaces de bureau.
- L'interface actuelle utilise une présentation claire, bleu-gris et teal,
  adaptée à une console professionnelle de gestion des accès.
- Dock magnétique en haut avec agrandissement au survol, infobulles et
  indicateur de page active ; profil accessible depuis le dock.
- Recherche rapide des écrans par le bouton Rechercher ou Ctrl + K.
- Tableaux, badges de statut, formulaires, fenêtres de dialogue et retours
  d'action ; mise en page adaptée aux écrans mobiles.
- Connexion avec carte suspendue et effet de lampe.
- Introduction visuelle de cinq secondes à la première entrée de la session,
  sans bouton Passer ; adaptation aux préférences de réduction du mouvement.
- Les interfaces ont d'abord fonctionné avec des données de démonstration.
  Elles sont maintenant reliées aux API et aux données du serveur.

C. Authentification réelle
- ASP.NET Core Identity pour les comptes et les mots de passe hachés.
- Connexion/déconnexion par cookie HttpOnly.
- Mot de passe : au moins 12 caractères, majuscule, minuscule, chiffre
  et caractère spécial.
- Verrouillage temporaire de 15 minutes après cinq échecs de connexion.
- Protections antiforgery/CSRF et limitation de débit des opérations sensibles.
- Aucun compte administrateur ni mot de passe par défaut dans le code.

D. Base de données
- Entity Framework Core avec SQLite : aucun serveur SQL à installer.
- Migration initiale incluse et appliquée automatiquement au démarrage.
- Conservation des comptes, rôles, permissions et événements après redémarrage.
- L'ancien stockage navigateur de démonstration n'est pas une base réelle
  et n'est pas importé dans SQLite.

E. Autorisations
- Trois rôles : Administrateur, Auditeur et Employé.
- Six permissions : users.read, users.write, roles.assign, audit.read,
  audit.export et roles.write.
- Contrôle côté serveur sur chaque requête, en plus des boutons masqués.
- Désactivation d'un compte : révocation de sa session existante.
- Modification de la matrice : droits pris en compte à la requête suivante.
- Protection du dernier administrateur actif et des permissions administrateur.
- Détection des modifications concurrentes d'un compte.

F. Double authentification
- Association de Google Authenticator par QR code ou clé manuelle.
- Vérification réelle du code TOTP côté serveur.
- Connexion en deux étapes lorsqu'un compte a activé la 2FA.
- Dix codes de récupération affichés une seule fois, chacun à usage unique.
- Protection des valeurs des tokens Identity par Data Protection.

G. Journal d'audit
- Enregistrement des connexions, échecs, verrouillages, déconnexions,
  modifications de comptes/permissions, opérations 2FA, exports et refus API.
- Date UTC, acteur, action, ressource, résultat et adresse IP.
- Recherche et filtres côté serveur ; export CSV.
- Aucun mot de passe, secret TOTP ou code de récupération dans le journal.
- Journal en ajout seul dans l'application. Un administrateur du système
  peut cependant modifier le fichier de base : ce n'est pas un journal
  cryptographiquement immuable ni une certification de conformité.


3. CE QU'IL FAUT RECEVOIR
------------------------------------------------------------
Envoyer ce README.txt avec l'archive AccessGuard-Backend.zip.
Cette archive contient le projet actuel, le backend, les vues, les ressources,
les migrations EF Core, les tests et le README technique.

Utiliser cette archive pour reprendre le travail. Les anciennes archives
nommées Frontend correspondent à une étape antérieure.

L'archive exclut volontairement la base de travail, les clés, les mots de
passe, les dossiers bin/obj, .vs et node_modules.
Le collègue disposera donc d'une installation indépendante et vide.
Il ne retrouvera pas automatiquement les comptes ou événements de mon PC.


4. PRÉREQUIS SUR LE NOUVEL ORDINATEUR
------------------------------------------------------------
- SDK .NET 10 (le runtime seul ne suffit pas pour compiler).
- Visual Studio compatible avec .NET 10 et la charge de travail
  « Développement ASP.NET et web », ou un terminal et un éditeur.
- Connexion Internet pour restaurer les packages NuGet au premier lancement.
- Navigateur web récent.
- Google Authenticator sur téléphone pour essayer la double authentification.

Node.js n'est PAS nécessaire pour lancer l'application.
Il est nécessaire uniquement pour les tests automatisés JavaScript.
Les packages Identity et EF Core référencés dans le projet sont en 10.0.15.


5. LANCEMENT AVEC VISUAL STUDIO
------------------------------------------------------------
1) Extraire entièrement AccessGuard-Backend.zip dans un dossier local.
   Ne pas ouvrir le projet directement à l'intérieur du ZIP.
2) Ouvrir AccessGuard.slnx dans le dossier extrait AccessGuard.
3) Si nécessaire, définir AccessGuard comme projet de démarrage.
4) Laisser Visual Studio restaurer les packages NuGet.
5) Choisir le profil « http » puis lancer avec Ctrl + F5.
6) Ouvrir http://localhost:5291 dans le navigateur si celui-ci ne s'ouvre pas.
7) Garder l'application en cours d'exécution pendant son utilisation.

Si l'éditeur ne sait pas ouvrir le fichier .slnx, utiliser la méthode terminal
ci-dessous ; il est aussi possible d'ouvrir AccessGuard/AccessGuard.csproj.


6. LANCEMENT AVEC UN TERMINAL
------------------------------------------------------------
Ouvrir PowerShell dans le dossier qui contient AccessGuard.slnx et README.md.
Exécuter, une ligne à la fois :

  dotnet --version
  dotnet restore AccessGuard/AccessGuard.csproj
  dotnet build AccessGuard/AccessGuard.csproj
  dotnet run --project AccessGuard/AccessGuard.csproj --launch-profile http

Puis ouvrir : http://localhost:5291
Pour arrêter le serveur dans le terminal : Ctrl + C.

La commande doit être exécutée depuis le dossier indiqué, car les chemins
ci-dessus sont relatifs à ce dossier. Le profil http définit l'environnement
Development, nécessaire à l'initialisation locale par navigateur.


7. PREMIÈRE CONNEXION ET DÉMONSTRATION
------------------------------------------------------------
Au premier lancement sur la nouvelle machine :
1) L'application crée la base et les rôles.
2) L'écran « Créez votre administrateur » apparaît.
3) Saisir un nom, une adresse e-mail et un mot de passe personnel conforme.
4) Se connecter avec ces identifiants.

Il n'y a pas de mot de passe universel à demander au propriétaire du projet.
L'initialisation par navigateur fonctionne uniquement en développement,
depuis la machine locale, et uniquement tant qu'aucun compte n'existe.

Parcours conseillé pour découvrir l'application :
- Vue d'ensemble : consulter les indicateurs et l'activité récente.
- Utilisateurs : créer un Employé puis un Auditeur avec un mot de passe initial.
- Permissions : examiner la matrice des droits par rôle.
- Sécurité 2FA : entrer son mot de passe, préparer la clé, scanner le QR code,
  puis saisir le code à six chiffres de Google Authenticator.
- Conserver les dix codes de récupération lors de leur affichage.
- Mon compte : se déconnecter, puis tester la connexion avec second facteur.
- Audit : retrouver les actions, appliquer les filtres et exporter le journal.
- Tester un compte Employé pour constater la restriction des écrans et API.

Accès par défaut :
Administrateur : gestion complète des comptes, permissions et audit.
Auditeur       : lecture des utilisateurs et du journal, export du journal.
Employé        : espace personnel, profil et sécurité 2FA.

La page Permissions permet de modifier les droits des deux derniers rôles.


8. REPÈRES DANS LE CODE
------------------------------------------------------------
AccessGuard.slnx                  Solution Visual Studio.
AccessGuard/AccessGuard.csproj    Projet et dépendances NuGet.
AccessGuard/Program.cs            Services, Identity, SQLite, cookies,
                                 autorisations, antiforgery et middleware.
AccessGuard/Controllers/          Pages MVC et endpoints des API.
  AuthController.cs              Session, initialisation, connexion, 2FA, logout.
  AccountApiController.cs        Profil et configuration 2FA.
  WorkspaceController.cs         Données, utilisateurs, permissions et audit.
  PortalController.cs            Navigation vers les vues du portail.
AccessGuard/Data/                 Entités et contexte Entity Framework Core.
AccessGuard/Data/Migrations/      Migration initiale et snapshot du modèle.
AccessGuard/Security/             Permissions, autorisation et tokens protégés.
AccessGuard/Services/             Initialisation de la base et service d'audit.
AccessGuard/Models/               Modèles de requêtes et validations.
AccessGuard/Views/                Pages Razor et layout partagé.
AccessGuard/Views/Shared/
  _Layout.cshtml                 Structure commune, dock et dialogues.
AccessGuard/wwwroot/css/
  professional.css               Style actuel de l'interface.
AccessGuard/wwwroot/js/
  site.js                        API, formulaires et rendu des données.
  professional.js                Interactions visuelles et navigation rapide.
tests/backend.cjs                Tests d'intégration API et navigateur.
README.md                        Documentation technique complémentaire.

Les autres fichiers de style éventuellement conservés ne sont pas tous
chargés par le layout actuel. Vérifier _Layout.cshtml avant une modification.


9. BASE, CLÉS ET DONNÉES PERSONNELLES
------------------------------------------------------------
Par défaut, sous le dossier du projet web AccessGuard :
  App_Data/accessguard.db  : base SQLite locale.
  App_Data/keys/           : clés ASP.NET Core Data Protection.

Ne pas envoyer ces éléments avec le code et ne pas les ajouter à Git.
Une copie du code ne donne pas accès à la base présente sur un autre PC.
Chaque installation conserve ses propres données.

Sous Windows, les clés sont protégées par DPAPI et liées au compte système.
Copier simplement la base et les clés vers un autre ordinateur ne garantit
pas le déchiffrement des secrets 2FA ou la réutilisation des sessions.
Pour découvrir et développer, créer une nouvelle base locale est le parcours
prévu. Un transfert des données réelles nécessiterait une procédure dédiée.

Configuration facultative par variables d'environnement :
  AccessGuard__DataPath       Déplace le dossier des données et des clés.
  ConnectionStrings__Default Remplace la connexion SQLite.
  Bootstrap__Email           Adresse du premier administrateur.
  Bootstrap__Password        Mot de passe du premier administrateur.
  Bootstrap__Name            Nom du premier administrateur.

Les valeurs Bootstrap servent à l'initialisation par configuration, notamment
en production. Ne pas inscrire un mot de passe réel dans le code partagé.


10. VALIDATIONS RÉALISÉES ET TESTS À RELANCER
------------------------------------------------------------
Au moment de cette livraison : compilation réussie, zéro avertissement
et zéro erreur. Les tests d'intégration ont réussi sur une base isolée.

Ils couvrent notamment : initialisation unique, protection CSRF, cookies,
droits par rôle, désactivation de compte, concurrence des modifications,
permissions modifiées en cours de session, verrouillage, journal d'audit,
TOTP, codes de récupération à usage unique, formulaires et persistance
après redémarrage. Ces résultats concernent la version livrée.

Pour relancer sur le nouveau PC, installer Node.js puis, depuis la racine :
  dotnet build AccessGuard/AccessGuard.csproj
  npm install --prefix tests

Si Brave est installé à son emplacement Windows habituel, il est utilisé.
Sinon, indiquer le chemin d'un navigateur Chromium/Edge en PowerShell :
  $env:BROWSER_PATH = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'

Adapter ce chemin à l'installation réelle du navigateur, puis lancer :
  npm test --prefix tests

Alternative sans navigateur local configuré : depuis le dossier tests,
exécuter « npx playwright install chromium », puis revenir à la racine et
lancer les tests sans variable BROWSER_PATH.

Le serveur de test utilise le port 5405. Les bases de test sont séparées,
sous tests/.data ; les résultats sont écrits dans tests/results.


11. COMMENT CONTINUER LE DÉVELOPPEMENT
------------------------------------------------------------
- Démarrer par une exploration de l'interface et des rôles existants.
- Vérifier la compilation avant et après une modification.
- Garder les contrôles de permission côté serveur : masquer un bouton
  ne constitue pas une autorisation.
- Pour une évolution du modèle de données, créer une nouvelle migration EF
  plutôt que modifier uniquement la base à la main.
- Conserver la logique de hachage et de 2FA fournie par Identity.
- Ne pas journaliser les mots de passe, clés TOTP ou codes de récupération.
- Se mettre d'accord sur les fichiers modifiés, idéalement avec Git,
  pour ne pas écraser le travail de l'autre membre du binôme.

Suites possibles, encore absentes ou à préparer :
- Envoi d'e-mails, confirmation d'adresse et récupération de mot de passe.
- Politique imposant la 2FA selon le rôle.
- Sauvegardes et politique de conservation du journal.
- Hébergement, configuration HTTPS et gestion des secrets de production.
- Multi-organisation, alertes SIEM ou journal externe si le périmètre le demande.
- Rapport académique : sujet, architecture, captures, scénario et limites.

L'application contient un backend fonctionnel. Cela ne signifie pas qu'elle
est certifiée pour un usage de production ou conforme à un référentiel précis.


12. PROBLÈMES COURANTS
------------------------------------------------------------
« dotnet » introuvable / framework non pris en charge :
Installer le SDK .NET 10 puis rouvrir le terminal et Visual Studio.

Restauration NuGet impossible :
Vérifier Internet, l'accès à NuGet et les messages de restauration.

Port 5291 déjà utilisé :
Arrêter l'ancienne instance ou lancer sur un autre port :
  dotnet run --project AccessGuard/AccessGuard.csproj --launch-profile http -- --urls http://localhost:5292
Puis ouvrir http://localhost:5292.

Fichiers de compilation verrouillés :
Arrêter l'application ou le débogage, puis recompiler.

Le navigateur ne peut pas joindre localhost :
Vérifier que le serveur tourne et lire l'adresse « Now listening on »
affichée dans le terminal ou la sortie de Visual Studio.

L'écran de création administrateur n'apparaît plus :
Un compte existe peut-être déjà dans cette base. Utiliser sa connexion.
L'initialisation ne permet pas d'ajouter librement un second administrateur.

Code Google Authenticator refusé :
Vérifier l'heure automatique du téléphone et du PC, le compte choisi et
la validité du code. Un code de récupération déjà utilisé ne fonctionne plus.

Une page est absente du dock ou affiche un refus d'accès :
Vérifier le rôle et les permissions du compte connecté.

IMPORTANT : localhost désigne toujours l'ordinateur qui ouvre l'adresse.
Le collègue verra son installation locale, pas celle qui tourne sur mon PC.
Le site n'est pas encore publié à une adresse Internet partagée.

FIN DU GUIDE
