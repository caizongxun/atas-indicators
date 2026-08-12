using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using ATAS.Indicators;

namespace AtasIndicators
{
    [DisplayName("Advanced Dual OI Analyzer")]
    [Description("優化版 Dual OI 分析儀：包含極端換手(Churn)標記功能")]
    public class AdvancedDualOIAnalyzer : Indicator
    {
        // ── Data Series ────────────────────────────────────────────
        private readonly ValueDataSeries _newLongs;
        private readonly ValueDataSeries _newShorts;
        private readonly ValueDataSeries _shortCovering;
        private readonly ValueDataSeries _longLiquidation;
        private readonly ValueDataSeries _churnMarker;

        // ── 參數設定 ───────────────────────────────────────────────
        private int _maWindow = 20;
        private decimal _volMultiplier = 1.5m;
        private decimal _oiMultiplier = 0.5m;

        [Display(Name = "均值計算週期 (MA Window)", GroupName = "換手偵測設定", Order = 1)]
        public int MaWindow
        {
            get => _maWindow;
            set { _maWindow = Math.Max(5, value); RecalculateValues(); }
        }

        [Display(Name = "高量判定倍數 (Vol Multiplier)", GroupName = "換手偵測設定", Description = "當前成交量必須大於均量的幾倍", Order = 2)]
        public decimal VolMultiplier
        {
            get => _volMultiplier;
            set { _volMultiplier = Math.Max(0.1m, value); RecalculateValues(); }
        }

        [Display(Name = "低 OI 變化判定倍數 (OI Multiplier)", GroupName = "換手偵測設定", Description = "當前 OI 變化必須小於 OI 變動均值的幾倍", Order = 3)]
        public decimal OiMultiplier
        {
            get => _oiMultiplier;
            set { _oiMultiplier = Math.Max(0.1m, value); RecalculateValues(); }
        }

        // ── Constructor ─────────────────────────────────────────────
        public AdvancedDualOIAnalyzer() : base(true)
        {
            // 設定此指標繪製於獨立的副圖表 (Sub-window)
            Panel = IndicatorDataProvider.NewPanel;

            _newLongs = new ValueDataSeries("New Longs (建多倉)") 
            { Color = Colors.Lime, VisualType = VisualMode.Histogram, Width = 3 };
            
            _newShorts = new ValueDataSeries("New Shorts (建空倉)") 
            { Color = Colors.Red, VisualType = VisualMode.Histogram, Width = 3 };
            
            _shortCovering = new ValueDataSeries("Short Covering (空頭回補)") 
            { Color = Colors.DarkGreen, VisualType = VisualMode.Histogram, Width = 3 };
            
            _longLiquidation = new ValueDataSeries("Long Liquidation (多頭踩踏)") 
            { Color = Colors.DarkRed, VisualType = VisualMode.Histogram, Width = 3 };

            _churnMarker = new ValueDataSeries("激烈換手標記 (Churn)") 
            { 
                Color = Colors.Yellow, 
                VisualType = VisualMode.Square, 
                Width = 5,
                ShowZeroValue = false // <--- 關鍵修正：隱藏數值為 0 的標記，避免連成一條線
            };

            DataSeries.Add(_newLongs);
            DataSeries.Add(_newShorts);
            DataSeries.Add(_shortCovering);
            DataSeries.Add(_longLiquidation);
            DataSeries.Add(_churnMarker);
        }

        // ── 計算邏輯 ────────────────────────────────────────────────
        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0) return;

            var candle = GetCandle(bar);
            var prevCandle = GetCandle(bar - 1);

            decimal deltaOI = candle.OI - prevCandle.OI;
            decimal delta = candle.Delta;
            decimal vol = candle.Volume;

            // 1. 初始化清空
            _newLongs[bar] = 0;
            _newShorts[bar] = 0;
            _shortCovering[bar] = 0;
            _longLiquidation[bar] = 0;
            _churnMarker[bar] = 0;

            // 2. 基礎四象限柱體渲染
            if (deltaOI > 0 && delta > 0) 
                _newLongs[bar] = deltaOI;
            else if (deltaOI > 0 && delta < 0) 
                _newShorts[bar] = deltaOI;
            else if (deltaOI < 0 && delta > 0) 
                _shortCovering[bar] = deltaOI;
            else if (deltaOI < 0 && delta < 0) 
                _longLiquidation[bar] = deltaOI;

            // 3. 換手率 (Churn) 偵測邏輯
            if (bar >= _maWindow)
            {
                decimal sumVol = 0;
                decimal sumAbsOi = 0;

                // 計算過去 N 根的 Volume 與 OI 絕對變化量的平均
                for (int i = 0; i < _maWindow; i++)
                {
                    var c = GetCandle(bar - i);
                    var p = GetCandle(bar - i - 1);
                    sumVol += c.Volume;
                    sumAbsOi += Math.Abs(c.OI - p.OI);
                }

                decimal volMA = sumVol / _maWindow;
                decimal oiMA = sumAbsOi / _maWindow;

                if (volMA > 0 && oiMA > 0)
                {
                    // 核心定義：成交量極大 (高參與度) + OI 淨變化極小 (互相抵銷)
                    bool isHighVolume = vol > (volMA * _volMultiplier);
                    bool isLowOiChange = Math.Abs(deltaOI) < (oiMA * _oiMultiplier);

                    if (isHighVolume && isLowOiChange)
                    {
                        // 觸發標記！為了讓標記在圖表上明顯，且不被 0 軸擋住，給予一個微小的偏移量
                        _churnMarker[bar] = deltaOI == 0 ? 0.0001m : deltaOI;
                    }
                }
            }
        }
    }
}
