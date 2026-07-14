# C# / Unity Styleguide

## 1. Naming
| Element | Konvention | Beispiel |
|---|---|---|
| Klassen, Methoden, Properties, Enums | PascalCase | `PlayerController` |
| Lokale Variablen, Parameter | camelCase | `moveSpeed` |
| Private Felder | _camelCase | `_health` |
| Interfaces | IPascalCase | `IDamageable` |
| Konstanten | PascalCase | `MaxPlayers` |

## 2. Datei & Klassenaufbau
- 1 Klasse pro Datei, Dateiname = Klassenname
- Reihenfolge: Felder → Properties → Unity-Lifecycle → public Methoden → private Methoden
- Leere Unity-Callbacks (`Update()` ohne Inhalt) löschen

## 3. Unity-Spezifisch
- `[SerializeField] private` statt `public`
- `GetComponent<T>()` in `Awake()` cachen, nie in `Update()`
- Kein `GameObject.Find()` / String-Lookups
- Async-Strategie festlegen: Coroutines **oder** async/UniTask — nicht mischen

## 4. Multiplayer
- Server-authoritative — Client zeigt an, Server entscheidet
- Client-Input serverseitig validieren, nie blind übernehmen
- RPC-Naming: `DoXServerRpc()`, `DoXClientRpc()`
- Sync über NetworkVariables/SyncVars, kein RPC-Spam für State
- Ordnertrennung: `Server/`, `Client/`, `Shared/`

## 5. Projektstruktur
```
Assets/_Project/
  Scripts/{Server,Client,Shared}/
  Prefabs/
  Scenes/
```
- Namespace = Ordnerpfad
- Asset-Store-Imports bleiben außerhalb `_Project/`

## 6. Formatierung
- `.editorconfig` im Repo-Root
- IDE-Formatter aktivieren, keine manuelle Diskussion
