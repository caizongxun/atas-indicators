using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using ATAS.Indicators;

namespace AtasIndicators
{
    [DisplayName("Auto Trade: Delta Turnaround + OI")]
    [Description("結合 Delta Turnaround 與 Dual OI 的自動交易訊號，支援自動下單")]
    public class AutoTradeDeltaTurnaroundOI : Indicator
    {
        // ── Data Series ────────────────────────────────────────────
        private readonly ValueDataSeries _buySignals;
        private readonly ValueDataSeries _sellSignals;

        // ── 系統變數與參數 ──────────────────────────────────────────
        private int _lastBar = -1;
        private bool _useAlerts = true;
        private int _orderQuantity = 1;

        [Display(Name = "啟用彈跳視窗警報", GroupName = "交易設定", Order = 1)]
        public bool UseAlerts
        {
            get => _useAlerts;
            set { _useAlerts = value; RecalculateValues(); }
        }

        [Display(Name = "每次進場口數", GroupName = "交易設定", Order = 2)]
        public int OrderQuantity
        {
            get => _orderQuantity;
            set { _orderQuantity = Math.Max(1, value); }
        }

        // ── Constructor ─────────────────────────────────────────────
        public AutoTradeDeltaTurnaroundOI() : base(true)
        {
            _buySignals = new ValueDataSeries("Buy Entry")
            {
                Color = Colors.Cyan,
                VisualType = VisualMode.UpArrow,
                Width = 3
            };

            _sellSignals = new ValueDataSeries("Sell Entry")
            {
                Color = Colors.Magenta,
                VisualType = VisualMode.DownArrow,
                Width = 3
            };

            DataSeries.Add(_buySignals);
            DataSeries.Add(_sellSignals);
        }

        // ── 計算邏輯 ────────────────────────────────────────────────
        protected override void OnCalculate(int bar, decimal value)
        {
            // 至少需要 4 根 K 棒的歷史資料才能進行前三根的比對與當前開盤的觸發
            if (bar < 3) return;

            // 初始化當前 K 棒的顯示訊號
            _buySignals[bar] = 0;
            _sellSignals[bar] = 0;

            // 換線偵測機制：確保只在「新 K 棒產生的第一筆 Tick」執行邏輯
            // 這保證了上一根 K 棒已經完全收盤，且進場點會貼近當前新 K 棒的開盤價
            if (bar == _lastBar) return;
            _lastBar = bar;

            // 定義已經完全收盤的 K 棒位置
            int prevBar = bar - 1;
            var candle = GetCandle(prevBar);
            var prev1Candle = GetCandle(prevBar - 1);
            var prev2Candle = GetCandle(prevBar - 2);

            // 空值與系統防呆檢查
            if (candle == null || prev1Candle == null || prev2Candle == null || InstrumentInfo == null) return;

            // ── 1. 基礎 K 線型態判斷 ──
            bool isBullish = candle.Close > candle.Open;
            bool isBearish = candle.Close < candle.Open;

            bool isPrev1Bullish = prev1Candle.Close > prev1Candle.Open;
            bool isPrev1Bearish = prev1Candle.Close < prev1Candle.Open;

            bool isPrev2Bullish = prev2Candle.Close > prev2Candle.Open;
            bool isPrev2Bearish = prev2Candle.Close < prev2Candle.Open;

            // ── 2. Delta Turnaround 邏輯 (掃掠流動性) ──
            bool isSweepLow = candle.Low <= prev1Candle.Low;
            bool isSweepHigh = candle.High >= prev1Candle.High;

            // ── 3. OI 變動邏輯 (Dual OI Analyzer 架構) ──
            decimal deltaOI = candle.OI - prev1Candle.OI;
            decimal delta = candle.Delta;

            // ── 組合做多條件 (Long Entry) ──
            // 條件 A (Delta Turnaround): 前兩根跌，當前漲 + 向下掃掠流動性 + 總 Delta 為正
            bool dtLong = isPrev2Bearish && isPrev1Bearish && isBullish && isSweepLow && (delta > 0);
            
            // 條件 B (Dual OI): 深綠色負軸 (OI 減少且 Delta 為正，代表上漲動能來自「空頭停損/回補」)
            bool oiLong = (deltaOI < 0) && (delta > 0);

            if (dtLong && oiLong)
            {
                // 將訊號標記在「當前新開 K 棒」的開盤價下方
                _buySignals[bar] = GetCandle(bar).Open - (10 * InstrumentInfo.TickSize);

                // 嚴格限制：只在當前最新一根 K 棒，且啟動警報/自動交易時執行
                if (bar == CurrentBar - 1 && _useAlerts)
                {
                    AddAlert("alert1", $"做多執行！空頭踩踏確認 (Entry Price: {GetCandle(bar).Open})");

                    // 呼叫 ATAS 底層帳戶管理介面進行市價做多
                    if (TradingManager != null && TradingManager.Portfolio != null)
                    {
                        var order = new Order
                        {
                            Portfolio = TradingManager.Portfolio, // 抓取目前圖表綁定的交易帳戶
                            Security = TradingManager.Security,   // 抓取目前圖表的交易商品
                            Direction = OrderDirections.Buy,      // 做多方向
                            Type = OrderTypes.Market,             // 市價單
                            Quantity = _orderQuantity             // 下單口數
                        };
                        TradingManager.OpenOrderAsync(order);
                    }
                }
            }

            // ── 組合做空條件 (Short Entry) ──
            // 條件 A (Delta Turnaround): 前兩根漲，當前跌 + 向上掃掠流動性 + 總 Delta 為負
            bool dtShort = isPrev2Bullish && isPrev1Bullish && isBearish && isSweepHigh && (delta < 0);
            
            // 條件 B (Dual OI): 紅色正軸 (OI 增加且 Delta 為負，代表下跌動能來自「空軍主動新建倉」)
            bool oiShort = (deltaOI > 0) && (delta < 0);

            if (dtShort && oiShort)
            {
                // 將訊號標記在「當前新開 K 棒」的開盤價上方
                _sellSignals[bar] = GetCandle(bar).Open + (10 * InstrumentInfo.TickSize);

                // 嚴格限制：只在當前最新一根 K 棒，且啟動警報/自動交易時執行
                if (bar == CurrentBar - 1 && _useAlerts)
                {
                    AddAlert("alert1", $"做空執行！空軍建倉壓制 (Entry Price: {GetCandle(bar).Open})");

                    // 呼叫 ATAS 底層帳戶管理介面進行市價做空
                    if (TradingManager != null && TradingManager.Portfolio != null)
                    {
                        var order = new Order
                        {
                            Portfolio = TradingManager.Portfolio,
                            Security = TradingManager.Security,
                            Direction = OrderDirections.Sell,     // 做空方向
                            Type = OrderTypes.Market,
                            Quantity = _orderQuantity
                        };
                        TradingManager.OpenOrderAsync(order);
                    }
                }
            }
        }
    }
}
