using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using ATAS.Indicators;

namespace AtasIndicators
{
    [DisplayName("Ultimate Order Flow Analyzer (PA Edition)")]
    [Description("整合 Dual OI、價格行為(前高低掃掠)與極端失衡的終極指標")]
    public class UltimateOrderFlowAnalyzer : Indicator
    {
        // ── Data Series ────────────────────────────────────────────
        private readonly ValueDataSeries _newLongs;
        private readonly ValueDataSeries _newShorts;
        private readonly ValueDataSeries _shortCovering;
        private readonly ValueDataSeries _longLiquidation;
        private readonly ValueDataSeries _churnMarker;
        private readonly ValueDataSeries _buySignals;
        private readonly ValueDataSeries _sellSignals;

        // ── 參數設定 ───────────────────────────────────────────────
        private int _maWindow = 20;
        private decimal _volMultiplier = 1.5m;
        private decimal _oiMultiplier = 0.5m;
        private int _paLookback = 10;
        private decimal _imbalanceThreshold = 0.2m; // Delta 佔 Volume 的 20%

        [Display(Name = "微觀：均值計算週期 (MA Window)", GroupName = "1. 訂單流過濾設定", Order = 1)]
        public int MaWindow
        {
            get => _maWindow;
            set { _maWindow = Math.Max(5, value); RecalculateValues(); }
        }

        [Display(Name = "微觀：爆量判定倍數 (Vol Multiplier)", GroupName = "1. 訂單流過濾設定", Order = 2)]
        public decimal VolMultiplier
        {
            get => _volMultiplier;
            set { _volMultiplier = Math.Max(0.1m, value); RecalculateValues(); }
        }

        [Display(Name = "宏觀：PA 前高低掃掠週期", GroupName = "2. 價格行為 (Price Action)", Description = "判斷流動性掃掠的歷史 K 棒數量", Order = 3)]
        public int PaLookback
        {
            get => _paLookback;
            set { _paLookback = Math.Max(3, value); RecalculateValues(); }
        }

        [Display(Name = "極端：Delta 失衡比例門檻", GroupName = "3. 爆倉/極端情緒過濾", Description = "Delta絕對值必須佔總成交量的比例 (0.2 = 20%)", Order = 4)]
        public decimal ImbalanceThreshold
        {
            get => _imbalanceThreshold;
            set { _imbalanceThreshold = Math.Max(0.05m, value); RecalculateValues(); }
        }

        // ── Constructor ─────────────────────────────────────────────
        public UltimateOrderFlowAnalyzer() : base(true)
        {
            Panel = IndicatorDataProvider.NewPanel;

            _newLongs = new ValueDataSeries("New Longs") { Color = Colors.Lime, VisualType = VisualMode.Histogram, Width = 3 };
            _newShorts = new ValueDataSeries("New Shorts") { Color = Colors.Red, VisualType = VisualMode.Histogram, Width = 3 };
            _shortCovering = new ValueDataSeries("Short Covering") { Color = Colors.DarkGreen, VisualType = VisualMode.Histogram, Width = 3 };
            _longLiquidation = new ValueDataSeries("Long Liquidation") { Color = Colors.DarkRed, VisualType = VisualMode.Histogram, Width = 3 };

            _churnMarker = new ValueDataSeries("換手標記 (Churn)") { Color = Colors.Yellow, VisualType = VisualMode.Square, Width = 4, ShowZeroValue = false };
            _buySignals = new ValueDataSeries("做多訊號 (Buy)") { Color = Colors.Cyan, VisualType = VisualMode.UpArrow, Width = 4, ShowZeroValue = false };
            _sellSignals = new ValueDataSeries("做空訊號 (Sell)") { Color = Colors.Magenta, VisualType = VisualMode.DownArrow, Width = 4, ShowZeroValue = false };

            DataSeries.Add(_newLongs);
            DataSeries.Add(_newShorts);
            DataSeries.Add(_shortCovering);
            DataSeries.Add(_longLiquidation);
            DataSeries.Add(_churnMarker);
            DataSeries.Add(_buySignals);
            DataSeries.Add(_sellSignals);
        }

        // ── 計算邏輯 ────────────────────────────────────────────────
        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar < _paLookback) return;

            var candle = GetCandle(bar);
            var prevCandle = GetCandle(bar - 1);

            _newLongs[bar] = 0;
            _newShorts[bar] = 0;
            _shortCovering[bar] = 0;
            _longLiquidation[bar] = 0;
            _churnMarker[bar] = 0;
            _buySignals[bar] = 0;
            _sellSignals[bar] = 0;

            if (candle == null || prevCandle == null) return;

            decimal deltaOI = candle.OI - prevCandle.OI;
            decimal delta = candle.Delta;
            decimal vol = candle.Volume;

            // 1. 繪製基礎 OI
            if (deltaOI > 0 && delta > 0) _newLongs[bar] = deltaOI;
            else if (deltaOI > 0 && delta < 0) _newShorts[bar] = deltaOI;
            else if (deltaOI < 0 && delta > 0) _shortCovering[bar] = deltaOI;
            else if (deltaOI < 0 && delta < 0) _longLiquidation[bar] = deltaOI;

            // 2. 計算均值與基礎過濾
            decimal sumVol = 0;
            decimal sumAbsOi = 0;

            for (int i = 0; i < _maWindow; i++)
            {
                var c = GetCandle(bar - i);
                var p = GetCandle(bar - i - 1);
                sumVol += c.Volume;
                sumAbsOi += Math.Abs(c.OI - p.OI);
            }

            decimal volMA = sumVol / _maWindow;
            decimal oiMA = sumAbsOi / _maWindow;

            bool isHighVolume = vol > (volMA * _volMultiplier);
            bool isLowOiChange = Math.Abs(deltaOI) < (oiMA * _oiMultiplier);
            
            // 爆倉替代特徵：強烈單向市價單佔比
            bool isDeltaImbalance = vol > 0 && (Math.Abs(delta) / vol) >= _imbalanceThreshold;

            if (isHighVolume && isLowOiChange)
            {
                _churnMarker[bar] = deltaOI == 0 ? 0.0001m : deltaOI;
            }

            // 3. 價格行為 (PA) 結構過濾 - 尋找流動性掃掠
            decimal highestHigh = decimal.MinValue;
            decimal lowestLow = decimal.MaxValue;
            for (int i = 1; i <= _paLookback; i++)
            {
                var pastC = GetCandle(bar - i);
                if (pastC.High > highestHigh) highestHigh = pastC.High;
                if (pastC.Low < lowestLow) lowestLow = pastC.Low;
            }

            // 確認當前 K 棒是否有跌破前低或突破前高
            bool isSweepLow = candle.Low <= lowestLow;
            bool isSweepHigh = candle.High >= highestHigh;

            // 4. 解析微觀價格結構 (收盤位置)
            decimal range = candle.High - candle.Low;
            decimal closePercent = range == 0 ? 0.5m : (candle.Close - candle.Low) / range;
            
            bool isUpperWickRejection = closePercent <= 0.45m; 
            bool isLowerWickRejection = closePercent >= 0.55m; 

            // 5. 終極訊號判斷邏輯 (嚴格結合 PA 掃掠與失衡)
            
            // --- 做多條件 (Buy) ---
            // 必須跌破前低 (Sweep Low) + 爆量吸收 或 多殺多踩踏 + 收下影線 + 極端賣壓失衡
            bool isBottomAbsorption = isHighVolume && (delta < 0) && isLowerWickRejection;
            bool isLongLiquidationExhaustion = (deltaOI < 0) && (delta < 0) && isLowerWickRejection;

            if (isSweepLow && isDeltaImbalance && (isBottomAbsorption || isLongLiquidationExhaustion))
            {
                decimal yPos = deltaOI < 0 ? deltaOI - (oiMA * 0.5m) : -(oiMA * 0.5m);
                _buySignals[bar] = yPos == 0 ? -0.0001m : yPos;
            }

            // --- 做空條件 (Sell) ---
            // 必須突破前高 (Sweep High) + 爆量派發 或 軋空枯竭 + 收上影線 + 極端買盤失衡
            bool isTopAbsorption = isHighVolume && (delta > 0) && isUpperWickRejection;
            bool isShortCoveringExhaustion = (deltaOI < 0) && (delta > 0) && isUpperWickRejection;

            if (isSweepHigh && isDeltaImbalance && (isTopAbsorption || isShortCoveringExhaustion))
            {
                decimal yPos = deltaOI > 0 ? deltaOI + (oiMA * 0.5m) : (oiMA * 0.5m);
                _sellSignals[bar] = yPos == 0 ? 0.0001m : yPos;
            }
        }
    }
}
