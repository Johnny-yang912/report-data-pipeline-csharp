# Report Data Pipeline（.NET 版）

製造報工資料的攝入 API。以 ASP.NET Core 重新實作 [電商資料攝入平台](https://github.com/Johnny-yang912/ecommerce-data-ingestion-platform)的攝入端，並將資料領域由電商訂單遷移至工單報工。

## 專案背景

原作品以 FastAPI + Celery + Redis 實作電商訂單的攝入流程，核心設計是「快速落地、背景處理、錯誤只標記不拒絕」。

本專案有兩個目標：

- **領域遷移**：將 Schema 由電商訂單改為製造業報工（工單、料號、機台、作業員、良品/不良數、不良代碼、班別），驗證同一套攝入架構能否套用於不同資料領域。
- **技術棧遷移**：以 .NET 重新實作核心流程，驗證架構設計不綁定特定語言或框架。

## 處理流程

```
POST /api/reports
    ↓
[Raw]  逐字保留                                   status: pending → 回 200
    ↓  Channel<int> 派工（滿載 DropWrite → 掃描補回）
[ChannelWorker]  CAS 認領 → 解析 → 清理            status: processing → processed/error/duplicate
    ↓
[ODS]  錯誤只標記不拒絕     ← IsCleaned / UnmappedFields / IsSchemaDrift

──────────────── 恢復路徑 ────────────────
[ScanWorker]  定期掃描
    ├── pending                    → 重新派工
    └── processing 且 ClaimedAt 逾時 → 重設為 pending → 重新派工
```

1. **快速落地**：API 收到請求後，原始 JSON 直接寫入 Raw 表，status 設為 `pending`，立即回傳 200。解析與清理不在請求路徑上。
2. **背景派工**：Raw Id 送入 `Channel<int>`，由 `ChannelWorker` 消費。
3. **CAS 搶占**：以條件更新將 status 由 `pending` 改為 `processing`，並記錄 `ClaimedAt`。更新筆數為 0 代表已被其他處理者取得，直接結束。
4. **解析、清理、寫入 ODS**：搶到的任務進行 JSON 解析與欄位清理，結果寫入 ODS 表。

## 設計重點

### 錯誤只標記，不拒絕

資料有問題時不回傳錯誤、也不丟棄，而是照樣寫入 ODS 並標記：

| 欄位 | 用途 |
|---|---|
| `IsCleaned` / `CleanErrorMessage` | 清理是否成功與失敗原因 |
| `UnmappedFields` | 無法對應到 Schema 的欄位，原樣保留 |
| `IsSchemaDrift` | 來源資料結構是否與預期不符 |

上游送來的資料是事實紀錄，拒收只會讓資料消失；保留並標記，下游才能決定如何處理，也能回頭追查。

### 狀態為唯一真相

Raw 落地即持久化，處理進度完全由 DB 的 status 表示。背景派工通道中遺失的任務不影響正確性，`ScanWorker` 會定期補回：

- **`pending`**：尚未被處理的資料
- **`processing` 且 `ClaimedAt` 逾時**：處理中崩潰的資料，重設為 `pending` 後重新處理。逾時門檻用於避免大量積壓時，仍在處理中的任務被誤判重設

### 冪等性

同一筆資料可能被處理兩次（例如掃描重設後，原處理者其實仍在執行）。重複處理由 CAS 搶占與 ODS 的唯一約束UNIQUE(ReportId) 共同擋住，與原作品相同。

## 與原作品的差異

| 項目 | 原作品 | 本版 |
|---|---|---|
| 技術棧 | Python / FastAPI | C# / ASP.NET Core |
| 資料領域 | 電商訂單 | 製造報工 |
| 任務派發 | Celery + Redis | `BackgroundService` + `Channel<int>` |
| claim 前崩潰的恢復 | broker 重投，秒級 | `ScanWorker` 掃描，掃描週期 |
| claim 後崩潰的恢復 | stale 掃描 | 相同 |
| 清理規則 | 完整業務規則 | 基本型別與格式清理 |
| 例外處理 | 依錯誤類型分類 | 統一捕捉 |
| 重試機制 | 有 | 尚未實作 |

### 任務派發：以 BackgroundService + Channel 取代 Celery + Redis

本版定位為單節點部署，以行程內的 `Channel<int>` 作為派工通道，由 `ChannelWorker` 消費。

原作品對 Worker 崩潰有兩層互補防護：崩在 claim commit 之前，由 broker 重投（`acks_late` + `reject_on_worker_lost`）秒級恢復；崩在 claim commit 之後，由 stale 掃描依 `ClaimedAt` 逾時救回。本版保留後者，前者因 Channel 位於行程記憶體中、無 Ack 語意而無法重現，改由 `ScanWorker` 的 `pending` 掃描涵蓋。

與原作品相比的代價：

- **claim 前崩潰的恢復由秒級變為掃描週期**：資料不會遺失，但恢復較慢
- **突發流量的緩衝能力較低**：Channel 容量有限，滿載時新任務不進入通道（`DropWrite`），改由下一輪掃描處理
- **Worker 無法獨立於 API 擴充**

未採用的替代方案：

- **Hangfire + SQL Server**：任務佇列的輪詢負載會落在與業務寫入相同的資料庫上，佇列不應佔用 DB
- **Hangfire + Redis**：需要付費的 Pro 版儲存套件
- **RabbitMQ**：可完整提供 Ack，但對單節點範圍而言，多一個需要維運的元件

## 本版範圍

本版聚焦於核心流程：快速落地、背景派工、CAS 搶占、標記不拒絕。業務層面的完整處理請參考原作品。


## 技術棧

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core（SQL Server）
- `BackgroundService` + `System.Threading.Channels`
- Swagger（開發環境）
- 測試：CAS 搶占行為測試（SQLite 測試資料庫）

## 專案結構

```
ReportPipeline.Api/
├── BackgroundService/   ChannelWorker、ScanWorker
├── Configurations/      EF Core 實體設定
├── Controllers/         API 端點
├── DB/                  AppDbContext
├── DTOs/                回應模型
├── Migrations/          EF Core migrations
├── Models/              Raw、ODS 實體
└── Process/             RawProcessor（CAS 搶占）、Cleaner（清理）
ReportPipeline.Api.Tests/
```

## 執行方式

**需求**：.NET 10 SDK、SQL Server（本機，Windows 驗證）

1. 確認 `ReportPipeline.Api/appsettings.Development.json` 的連線字串符合本機環境：
   ```json
   "DefaultConnection": "Server=localhost;Database=MyDatabase;Trusted_Connection=yes;TrustServerCertificate=True;"
   ```
   若使用 SQL 帳號驗證，請以 User Secrets 設定，不要寫入設定檔：
   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;User Id=...;Password=..." --project ReportPipeline.Api
   ```

2. 建立資料庫：
   ```bash
   dotnet tool install --global dotnet-ef
   dotnet ef database update --project ReportPipeline.Api
   ```

3. 啟動：
   ```bash
   dotnet run --project ReportPipeline.Api
   ```
   或在 Visual Studio 中將 `ReportPipeline.Api` 設為啟始專案後按 F5，會自動開啟 Swagger。

4. 執行測試：
   ```bash
   dotnet test
   ```
