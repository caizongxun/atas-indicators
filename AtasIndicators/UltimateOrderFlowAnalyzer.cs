using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using ATAS.Indicators;

namespace AtasIndicators
{
    [DisplayName("Ultimate Order Flow Analyzer")]
    [Description("整合 Dual OI、換手吸收與努力結果不對稱的終極多空指標")]
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

        [Display(Name = "均值計算週期 (MA Window)", GroupName = "訊號過濾設定", Order = 1)]
        public int MaWindow
        {
            get => _maWindow;
            set { _maWindow = Math.Max(5, value); RecalculateValues(); }
        }

        [Display(Name = "爆量判定倍數 (Vol Multiplier)", GroupName = "訊號過濾設定", Order = 2)]
        public decimal VolMultiplier
        {
            get => _volMultiplier;
            set { _volMultiplier = Math.Max(0.1m, value); RecalculateValues(); }
        }

        [Display(Name = "低 OI 變化判定倍數 (OI Multiplier)", GroupName = "訊號過濾設定", Order = 3)]
        public decimal OiMultiplier
        {
            get => _oiMultiplier;
            set { _oiMultiplier = Math.Max(0.1m, value); RecalculateValues(); }
        }

        // ── Constructor ─────────────────────────────────────────────
        public UltimateOrderFlowAnalyzer() : base(true)
        {
            Panel = IndicatorDataProvider.NewPanel;

            _newLongs = new ValueDataSeries("New Longs (建多倉)") { Color = Colors.Lime, VisualType = VisualMode.Histogram, Width = 3 };
            _newShorts = new ValueDataSeries("New Shorts (建空倉)") { Color = Colors.Red, VisualType = VisualMode.Histogram, Width = 3 };
            _shortCovering = new ValueDataSeries("Short Covering (空頭回補)") { Color = Colors.DarkGreen, VisualType = VisualMode.Histogram, Width = 3 };
            _longLiquidation = new ValueDataSeries("Long Liquidation (多頭踩踏)") { Color = Colors.DarkRed, VisualType = VisualMode.Histogram, Width = 3 };

            _churnMarker = new ValueDataSeries("激烈換手標記 (Churn)") 
            { 
                Color = Colors.Yellow, 
                VisualType = VisualMode.Square, 
                Width = 4,
                ShowZeroValue = false 
            };

            _buySignals = new ValueDataSeries("做多訊號 (Buy)") 
            { 
                Color = Colors.Cyan, 
                VisualType = VisualMode.UpArrow, 
                Width = 4,
                ShowZeroValue = false 
            };

            _sellSignals = new ValueDataSeries("做空訊號 (Sell)") 
            { 
                Color = Colors.Magenta, 
                VisualType = VisualMode.DownArrow, 
                Width = 4,
                ShowZeroValue = false 
            };

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
            if (bar < 1) return;

            var candle = GetCandle(bar);
            var prevCandle = GetCandle(bar - 1);

            // 初始化所有數值
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

            // 1. 繪製基礎 OI 柱狀圖
            if (deltaOI > 0 && delta > 0) _newLongs[bar] = deltaOI;
            else if (deltaOI > 0 && delta < 0) _newShorts[bar] = deltaOI;
            else if (deltaOI < 0 && delta > 0) _shortCovering[bar] = deltaOI;
            else if (deltaOI < 0 && delta < 0) _longLiquidation[bar] = deltaOI;

            if (bar < _maWindow) return;

            // 2. 計算均量與均值
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

            // 3. 換手標記 (Yellow Marker)
            if (isHighVolume && isLowOiChange)
            {
                _churnMarker[bar] = deltaOI == 0 ? 0.0001m : deltaOI;
            }

            // 4. 解析微觀價格結構 (計算收盤價位於整根 K 棒的百分比位置)
            decimal range = candle.High - candle.Low;
            decimal closePercent = range == 0 ? 0.5m : (candle.Close - candle.Low) / range;
            
            bool isUpperWickRejection = closePercent <= 0.4m; // 收盤在底部 40% (長上影線)
            bool isLowerWickRejection = closePercent >= 0.6m; // 收盤在頂部 40% (長下影線)

            // 5. 終極訊號判斷邏輯
            
            // --- 做多條件 (Buy) ---
            // 條件 A: 底部吸收 (爆量 + 負 Delta + 長下影線)
            bool isBottomAbsorption = isHighVolume && (delta < 0) && isLowerWickRejection;
            // 條件 B: 踩踏枯竭 (多單停損 + 負 Delta + 長下影線)
            bool isLongLiquidationExhaustion = (deltaOI < 0) && (delta < 0) && isLowerWickRejection;

            if (isBottomAbsorption || isLongLiquidationExhaustion)
            {
                // 在 OI 柱狀圖下方繪製向上箭頭，偏移量確保視覺清晰
                decimal yPos = deltaOI < 0 ? deltaOI - (oiMA * 0.5m) : -(oiMA * 0.5m);
                _buySignals[bar] = yPos == 0 ? -0.0001m : yPos;
            }

            // --- 做空條件 (Sell) ---
            // 條件 A: 頂部派發 (爆量 + 正 Delta + 長上影線)
            bool isTopAbsorption = isHighVolume && (delta > 0) && isUpperWickRejection;
            // 條件 B: 軋空枯竭 (空單平倉 + 正 Delta + 長上影線)
            bool isShortCoveringExhaustion = (deltaOI < 0) && (delta > 0) && isUpperWickRejection;

            if (isTopAbsorption || isShortCoveringExhaustion)
            {
                // 在 OI 柱狀圖上方繪製向下箭頭
                decimal yPos = deltaOI > 0 ? deltaOI + (oiMA * 0.5m) : (oiMA * 0.5m);
                _sellSignals[bar] = yPos == 0 ? 0.0001m : yPos;
            }
        }
    }
}
