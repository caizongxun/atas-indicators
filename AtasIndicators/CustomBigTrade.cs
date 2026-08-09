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
                VisualType = VisualMode.Dots, // 使用圓點顯示
                Width = _dotSize
            };

            _bigSells = new ValueDataSeries("Big Sell")
            {
                Color = Colors.Red,
                VisualType = VisualMode.Dots, // 使用圓點顯示
                Width = _dotSize
            };

            DataSeries.Add(_bigBuys);
            DataSeries.Add(_bigSells);
        }

        // ── 計算邏輯 ────────────────────────────────────────────────
        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0) return;

            var candle = GetCandle(bar);

            // 確保有足跡數據
            if (candle.Ask == 0 && candle.Bid == 0) return;

            // 初始化當前 K 棒的數值
            _bigBuys[bar] = 0;
            _bigSells[bar] = 0;

            // 判斷主動買單 (Ask) 是否超過大單門檻
            if (candle.Ask >= _volumeThreshold)
            {
                // 將圓點標記在 K 棒高點上方
                _bigBuys[bar] = candle.High + (5 * InstrumentInfo.TickSize);
            }

            // 判斷主動賣單 (Bid) 是否超過大單門檻
            if (candle.Bid >= _volumeThreshold)
            {
                // 將圓點標記在 K 棒低點下方
                _bigSells[bar] = candle.Low - (5 * InstrumentInfo.TickSize);
            }
        }
    }
}
