using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using ATAS.Indicators;

namespace AtasIndicators
{
    [DisplayName("Dual OI Analyzer")]
    [Description("同時追蹤多空雙方的未平倉量(OI)變化，精準辨識建倉與平倉行為")]
    public class DualOpenInterestAnalyzer : Indicator
    {
        // ── Data Series ────────────────────────────────────────────
        private readonly ValueDataSeries _newLongs;
        private readonly ValueDataSeries _newShorts;
        private readonly ValueDataSeries _shortCovering;
        private readonly ValueDataSeries _longLiquidation;

        // ── Constructor ─────────────────────────────────────────────
        public DualOpenInterestAnalyzer() : base(true)
        {
            // 將指標設定在獨立的副圖面板顯示
            Panel = IndicatorDataProvider.NewPanel;

            _newLongs = new ValueDataSeries("多頭建倉 (New Longs)")
            {
                Color = Colors.LimeGreen,
                VisualType = VisualMode.Histogram,
                Width = 3
            };

            _newShorts = new ValueDataSeries("空頭建倉 (New Shorts)")
            {
                Color = Colors.Red,
                VisualType = VisualMode.Histogram,
                Width = 3
            };

            _shortCovering = new ValueDataSeries("空頭回補 (Short Covering)")
            {
                Color = Colors.DarkGreen,
                VisualType = VisualMode.Histogram,
                Width = 3
            };

            _longLiquidation = new ValueDataSeries("多頭踩踏 (Long Liquidation)")
            {
                Color = Colors.DarkRed,
                VisualType = VisualMode.Histogram,
                Width = 3
            };

            DataSeries.Add(_newLongs);
            DataSeries.Add(_newShorts);
            DataSeries.Add(_shortCovering);
            DataSeries.Add(_longLiquidation);
        }

        // ── 計算邏輯 ────────────────────────────────────────────────
        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0) return;

            var candle = GetCandle(bar);
            var prevCandle = GetCandle(bar - 1);

            // 加入 null 檢查
            if (candle == null || prevCandle == null) return;

            // 修正為 OI
            decimal deltaOI = candle.OI - prevCandle.OI;
            decimal delta = candle.Delta;

            // 初始化當前 K 棒的數值
            _newLongs[bar] = 0;
            _newShorts[bar] = 0;
            _shortCovering[bar] = 0;
            _longLiquidation[bar] = 0;

            // 判斷建倉行為 (OI 增加)
            if (deltaOI > 0)
            {
                if (delta > 0)
                {
                    _newLongs[bar] = deltaOI;
                }
                else if (delta < 0)
                {
                    _newShorts[bar] = deltaOI;
                }
            }
            // 判斷平倉行為 (OI 減少)
            else if (deltaOI < 0)
            {
                if (delta > 0)
                {
                    _shortCovering[bar] = deltaOI;
                }
                else if (delta < 0)
                {
                    _longLiquidation[bar] = deltaOI;
                }
            }
        }
    }
}
