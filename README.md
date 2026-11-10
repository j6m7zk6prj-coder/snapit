# Snapit

Capture d’écran pour Windows, simple et rapide, inspirée de macOS.
Un seul fichier `Snapit.exe`, rien à installer.

## Utilisation

| Raccourci | Action |
|---|---|
| **Ctrl + Maj + 4** | Zone : glissez pour sélectionner — ou cliquez sur une fenêtre pour la capturer |
| **Ctrl + Maj + 3** | Écran entier (l’écran où se trouve la souris) |
| **Ctrl + Maj + 5** | Ouvrir la fenêtre Snapit (captures récentes, minuteur 3/5/10 s) |
| **Impr. écran** | Zone ou fenêtre (si la touche est libre, voir plus bas) |
| **Échap** / clic droit | Annuler |

Après chaque capture :
- l’image est **copiée** dans le presse-papiers (Ctrl+V n’importe où) ;
- elle est **enregistrée** dans `Images\Captures d’écran` ;
- une **vignette** apparaît en bas à droite : **cliquez** pour annoter, **glissez-la** dans un mail / Discord / dossier, **clic droit** pour supprimer ou l’afficher dans l’Explorateur.

L’éditeur propose stylo (P), surligneur (H), flèche (A), rectangle (R), texte (T) et flou (B), avec Ctrl+Z, Ctrl+C, Ctrl+S. Fermer = modifications conservées.

Icône dans la zone de notification : clic gauche = ouvrir la fenêtre Snapit, clic droit = menu (réglages, dossier, quitter).

## Obtenir le .exe avec GitHub Actions

1. Créez un dépôt sur GitHub (ex. `snapit`).
2. Envoyez **tout le contenu** de ce dossier, y compris `.github/workflows/build.yml` :
   ```
   git init
   git add .
   git commit -m "Snapit"
   git branch -M main
   git remote add origin https://github.com/VOTRE-NOM/snapit.git
   git push -u origin main
   ```
   (Ou via le site : *Add file › Upload files* puis glissez le dossier entier.)
3. L’onglet **Actions** compile automatiquement (≈ 3 min). À chaque push, une nouvelle **Release** est publiée.
4. Lien de téléchargement direct, toujours vers la dernière version :
   ```
   https://github.com/VOTRE-NOM/snapit/releases/latest/download/Snapit.exe
   ```

Vous pouvez aussi relancer la compilation à la main : *Actions › Build Snapit.exe › Run workflow*.

## Bon à savoir

- **SmartScreen** : l’exe n’est pas signé, Windows peut afficher « Windows a protégé votre ordinateur ». Cliquez sur *Informations complémentaires › Exécuter quand même*.
- **Touche Impr. écran** : sous Windows 11 elle ouvre l’Outil Capture par défaut. Pour la donner à Snapit : *Paramètres › Accessibilité › Clavier* › désactiver « Utiliser la touche Impr. écran pour ouvrir la capture d’écran », puis redémarrez Snapit.
- Lancement au démarrage de Windows : à activer dans les Réglages.
- Compatible Windows 10 et 11 (64 bits), multi-écrans et écrans haute résolution.

## Compiler soi-même

Avec le SDK .NET 10 :
```
dotnet publish src/Snapit.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o out
```
