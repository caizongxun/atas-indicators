using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using ATAS.Indicators;

namespace AtasIndicators
{
    [DisplayName("Custom Big Trade")]
    [Description("標記圖表上出現極端主動買賣單的位置，作為官方 Big Trade 的替代方案")]
    public class CustomBigTrade : Indicator
    {
        // ── Data Series ────────────────────────────────────────────
        private readonly ValueDataSeries _bigBuys;
        private readonly ValueDataSeries _bigSells;

        // ── 參數 ────────────────────────────────────────────────────
        private decimal _volumeThreshold = 50000m; 

        [Display(Name = "大單觸發門檻 (Volume)", GroupName = "設定", Order = 1)]
        public decimal VolumeThreshold
        {
            get => _volumeThreshold;
            set { _volumeThreshold = Math.Max(1, value); RecalculateValues(); }
        }

        private int _dotSize = 8;
        [Display(Name = "圓點大小", GroupName = "設定", Order = 2)]
        public int DotSize
        {
            get => _dotSize;
            set 
            { 
                _dotSize = Math.Max(1, value); 
                _bigBuys.Width = _dotSize;
                _bigSells.Width = _dotSize;
                RecalculateValues(); 
            }
        }

        // ── Constructor ─────────────────────────────────────────────
        public CustomBigTrade() : base(true)
        {
            _bigBuys = new ValueDataSeries("Big Buy")
            {
                Color = Colors.LimeGreen,
                VisualType = VisualMode.Dots, 
                Width = _dotSize
            };

            _bigSells = new ValueDataSeries("Big Sell")
            {
                Color = Colors.Red,
                VisualType = VisualMode.Dots, 
                Width = _dotSize
            };

            DataSeries.Add(_bigBuys);
            DataSeries.Add(_bigSells);
        }

        // ── 計算邏輯 ────────────────────────────────────────────────
        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0) return;

            // 解決 CS8602 警告
            if (InstrumentInfo == null) return;

            var candle = GetCandle(bar);
            if (candle == null || (candle.Ask == 0 && candle.Bid == 0)) return;

            _bigBuys[bar] = 0;
            _bigSells[bar] = 0;

            if (candle.Ask >= _volumeThreshold)
            {
                _bigBuys[bar] = candle.High + (5 * InstrumentInfo.TickSize);
            }

            if (candle.Bid >= _volumeThreshold)
            {
                _bigSells[bar] = candle.Low - (5 * InstrumentInfo.TickSize);
            }
        }
    }
}
