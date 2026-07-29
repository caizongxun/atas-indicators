# ATAS Custom Indicators

自定義 ATAS 平台指標，專注於 Order Flow / Footprint 分析。

## 指標列表

### FootprintDeltaAnalyzer
利用 ATAS 的 footprint 數據，分析每根 K 棒的主動買賣成交量（Ask/Bid），計算 Delta 與累積 Delta（CVD），並偵測 Delta 背離。

**功能：**
- 副圖顯示每棒 Delta（Ask - Bid）
- 柱狀圖分別顯示主動買/主動賣成交量
- 虛線追蹤累積 Delta（CVD）
- K 棒自動塗色反映主動方向
- 可調整的 Delta 背離偵測

## 建置環境

- Visual Studio 2022 以上
- .NET 8 SDK
- ATAS Platform 已安裝（取得 `ATAS.Indicators.dll`）

## 建置步驟

1. Clone 此 repo
2. 用 Visual Studio 開啟 `AtasIndicators.sln`
3. 右鍵 Dependencies → Add Reference → 選擇 `ATAS.Indicators.dll`
   - 預設路徑：`C:\Program Files (x86)\ATAS Platform\ATAS.Indicators.dll`
4. Build → 取得 `bin\Release\net8.0-windows\AtasIndicators.dll`

## 匯入 ATAS

- 開啟 ATAS → 指標視窗 → 左下角「Add custom indicator」→ 選擇編譯好的 `.dll`

## Git Pull 更新

```bash
git pull origin main
```
然後重新 Build 並重新載入 ATAS 中的指標。
