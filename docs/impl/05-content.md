# 資之詳 — Content Pipeline Specification

> 內容為血肉，格式為筋骨。筋骨不正，血肉難附。

## 1. JSON Schema 驗證

每內容型別有 JSON Schema，建置時驗證全檔。

### 1.1 房間段 Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "$id": "room-segment",
  "type": "object",
  "required": ["id", "template", "size", "category", "connections", "geometry", "tags"],
  "properties": {
    "id": { "type": "string", "pattern": "^[a-z0-9-]+$" },
    "template": { "enum": ["broken-engine","bloom-site","boneyard","sealed-vault","contested-ruin","underway","settlement-gone-wrong","ossuary"] },
    "size": { "enum": ["small","medium","large","setpiece"] },
    "category": { "enum": ["corridor","chamber","dead-end","puzzle","arena","treasure","connector"] },
    "connections": {
      "type": "object",
      "properties": {
        "north": {"enum":["open","closed","hidden","none"]},
        "south": {"enum":["open","closed","hidden","none"]},
        "east":  {"enum":["open","closed","hidden","none"]},
        "west":  {"enum":["open","closed","hidden","none"]}
      }
    },
    "geometry": {
      "type": "object",
      "required": ["grid"],
      "properties": {
        "grid": {
          "type": "array",
          "items": {
            "type": "array",
            "items": { "enum": [0,1,2,3,4] }
          }
        },
        "elevation": { "type": "integer" },
        "features": { "type": "array", "items": { "type": "string" } }
      }
    },
    "encounters": {
      "type": "object",
      "properties": {
        "primary": { "type": "string" },
        "optional": {
          "type": "object",
          "properties": {
            "id": { "type": "string" },
            "condition": { "type": "string" }
          }
        }
      }
    },
    "loot": {
      "type": "object",
      "properties": {
        "fixed": { "type": "array", "items": { "type": "string" } },
        "table": { "type": "string" }
      }
    },
    "interactables": {
      "type": "object",
      "additionalProperties": {
        "type": "object",
        "properties": {
          "classInteraction": {
            "type": "object",
            "properties": {
              "class": { "type": "string" },
              "action": { "type": "string" }
            }
          },
          "defaultInteraction": { "type": "string" }
        }
      }
    },
    "tags": {
      "type": "array",
      "items": { "type": "string" },
      "minItems": 1
    }
  }
}
```

### 1.2 敵人 Schema

```json
{
  "$id": "enemy",
  "required": ["id", "name", "category", "stats", "ai", "abilities"],
  "properties": {
    "id": { "type": "string" },
    "name": { "type": "string" },
    "category": { "enum": ["bloom","soldier","construct","unaccounted"] },
    "stats": {
      "type": "object",
      "properties": {
        "hp": { "type": "integer", "minimum": 1 },
        "speed": { "type": "integer" },
        "accuracy": { "type": "integer" },
        "evasion": { "type": "integer" },
        "armor": { "type": "integer" }
      }
    },
    "ai": { "enum": ["bloom_random","soldier_tactical","construct_guard","unaccounted_rulebreak"] },
    "abilities": {
      "type": "array",
      "items": { "type": "string" }  // ability IDs
    },
    "loot": { "type": "string" },   // loot table ID
    "resistances": { "type": "array", "items": { "type": "string" } },
    "immunities": { "type": "array", "items": { "type": "string" } }
  }
}
```

### 1.3 物品 Schema

```json
{
  "$id": "item",
  "required": ["id", "name", "type", "rarity"],
  "properties": {
    "id": { "type": "string" },
    "name": { "type": "string" },
    "type": { "enum": ["weapon","armor","consumable","component","key","document"] },
    "subtype": { "type": "string" },
    "rarity": { "enum": ["common","uncommon","rare","epic","unique"] },
    "stats": { "type": "object" },
    "effects": { "type": "array", "items": { "type": "string" } },
    "value": { "type": "integer", "minimum": 0 },
    "stackSize": { "type": "integer", "default": 1 },
    "requiredLevel": { "type": "integer", "default": 1 }
  }
}
```

## 2. 二進位資源包格式 (.rpk)

```
[Header]        64 bytes
  magic:        "RPK\0"          (4 bytes)
  version:      uint16           (2 bytes)
  entryCount:   uint32           (4 bytes)
  contentHash:  xxHash64         (8 bytes)
  reserved:     46 bytes padding

[Directory]     entryCount * 32 bytes
  idHash:       xxHash64(id string) (8 bytes)
  offset:       uint64           (8 bytes)
  length:       uint32           (4 bytes)
  typeTag:      uint16           (2 bytes)  // 1=segment,2=encounter,3=item,4=npc,5=document
  flags:        uint16           (2 bytes)
  reserved:     8 bytes

[Data]          連續 blob
  LZ4 壓縮或原始 bytes
```

讀取方式：
```csharp
public class ContentPackReader
{
    private readonly IReadOnlyDictionary<ulong, DirectoryEntry> _dir;
    private readonly ReadOnlyMemory<byte> _data;

    public ReadOnlySpan<byte> Get(ulong idHash)
    {
        var entry = _dir[idHash];
        return _data.Span.Slice((int)entry.Offset, entry.Length);
    }
}
```

開發模式：直讀 JSON，`ContentPackReader` 介面後備至 `JsonContentLoader`。

## 3. 內容熱重載 (開發)

```csharp
// RPC.Host 開發模式
if (_env.IsDevelopment)
{
    var watcher = new FileSystemWatcher("content/");
    watcher.Changed += (_, e) => {
        var changedId = Path.GetFileNameWithoutExtension(e.FullPath);
        _ws.Broadcast(new { t = MessageType.ContentReload, p = new { id = changedId } });
    };
}
```

前端收到 `ContentReload` 即重新請求該資源並刷新對應 UI。

## 4. 內容索引 (Phase 3 LLM 用)

建置時產生 `content-index.json`：

```json
{
  "segments": {
    "byId": { "broken-engine-corridor-1": { "template": "broken-engine", "tags": ["corridor","industrial"] } },
    "byTag": { "industrial": ["broken-engine-corridor-1", "..."] }
  },
  "encounters": { ... },
  "items": { ... },
  "npcs": { ... },
  "documents": { ... },
  "rumors": { ... }
}
```

LLM 生成時讀此索引，依 tag 選內容，回傳 ID 陣列。Engine 於執行期解析 ID 為 `.rpk` 條目。

## 5. Phase 1 內容量

| 型別 | 數量 | 說明 |
|---|---|---|
| Room segments | 12-15 | Broken Engine 模板：走廊、大廳、死胡同、控制室 |
| Enemies | 3 | 1 bloom, 1 soldier, 1 construct |
| Encounters | 8-10 | 難度分級，含 setpiece |
| Items | 18-22 | 武器×4、防具×6、消耗品×6、元件×4 |
| Classes | 4 | Bonewarden, Stillblade, Cauterist, Hollow |
| Abilities | 12-16 | 每職業 3-4 個 |
| NPCs | 4-6 | 酒館可招募 |
| Documents | 0 | Phase 1 無證據系統 |

## 6. 工具鏈

```
tools/content-pack/
├── ContentPackCompiler.csproj
├── Program.cs              # CLI: dotnet run -- compile ../content ../src/engine/Content/packs
├── SchemaValidator.cs      # 全 JSON 驗證
└── IndexBuilder.cs         # 產生 content-index.json
```

編譯命令：
```bash
dotnet run --project tools/content-pack -- compile content/ src/engine/RPC.Host/Content/
```

CI 檢查：
```bash
dotnet run --project tools/content-pack -- validate content/
```
