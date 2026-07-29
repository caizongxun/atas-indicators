using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using ATAS.Indicators;

namespace AtasIndicators
{
    [DisplayName("Order Flow Reversal Signal V2")]
    [Description("結合波段高低點、相對成交量與 Delta 比例，精準捕捉吸收與套牢的反轉信號")]
    public class OrderFlowReversalSignal : Indicator
    {
        private readonly ValueDataSeries _buySignals;
        private readonly ValueDataSeries _sellSignals;

        // ── 優化參數 ──────────────────────────────────────────────
        private int _swingLookback = 5;
        private int _volSmaPeriod = 10;
        private decimal _volMultiplier = 1.2m;
        private decimal _minDeltaRatio = 0.15m;
        private decimal _wickRatio = 0.5m;

        [Display(Name = "波段高低點偵測 (K棒數)", GroupName = "1. 市場結構", Order = 1)]
        public int SwingLookback
        {
            get => _swingLookback;
            set { _swingLookback = Math.Max(1, value); RecalculateValues(); }
        }

        [Display(Name = "均量計算週期", GroupName = "2. 成交量過濾", Order = 2)]
        public int VolSmaPeriod
        {
            get => _volSmaPeriod;
            set { _volSmaPeriod = Math.Max(1, value); RecalculateValues(); }
        }

        [Display(Name = "爆量倍數 (例如 1.2 = 均量的1.2倍)", GroupName = "2. 成交量過濾", Order = 3)]
        public decimal VolMultiplier
        {
            get => _volMultiplier;
            set { _volMultiplier = Math.Max(0.1m, value); RecalculateValues(); }
        }

        [Display(Name = "Delta 佔成交量最小比例 (0.1~1.0)", GroupName = "3. 訂單流失衡", Order = 4)]
        public decimal MinDeltaRatio
        {
            get => _minDeltaRatio;
            set { _minDeltaRatio = Math.Max(0.01m, Math.Min(1m, value)); RecalculateValues(); }
        }

        [Display(Name = "影線佔K棒最小比例 (0.1~0.9)", GroupName = "4. 型態過濾", Order = 5)]
        public decimal WickRatio
        {
            get => _wickRatio;
            set { _wickRatio = Math.Max(0.1m, Math.Min(0.9m, value)); RecalculateValues(); }
        }

        public OrderFlowReversalSignal() : base(true)
        {
            _buySignals = new ValueDataSeries("Buy Signal")
            {
                Color = Colors.LimeGreen,
                VisualType = VisualMode.UpArrow,
                Width = 3
            };

            _sellSignals = new ValueDataSeries("Sell Signal")
            {
                Color = Colors.Red,
                VisualType = VisualMode.DownArrow,
                Width = 3
            };

            DataSeries.Add(_buySignals);
            DataSeries.Add(_sellSignals);
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar < Math.Max(_swingLookback, _volSmaPeriod)) return;

            var candle = GetCandle(bar);
            if (candle.Ask == 0 && candle.Bid == 0) return;

            _buySignals[bar] = 0;
            _sellSignals[bar] = 0;

            decimal range = candle.High - candle.Low;
            if (range == 0 || candle.Volume == 0) return;

            // 1. 市場結構判定 (是否為局部高低點)
            bool isSwingLow = true;
            bool isSwingHigh = true;
            for (int i = 1; i <= _swingLookback; i++)
            {
                if (candle.Low > GetCandle(bar - i).Low) isSwingLow = false;
                if (candle.High < GetCandle(bar - i).High) isSwingHigh = false;
            }

            // 2. 相對成交量判定
            decimal sumVol = 0;
            for (int i = 1; i <= _volSmaPeriod; i++)
            {
                sumVol += GetCandle(bar - i).Volume;
            }
            decimal avgVol = sumVol / _volSmaPeriod;
            bool isHighVolume = candle.Volume >= (avgVol * _volMultiplier);

            // 3. Delta 比例與型態計算
            decimal deltaRatio = Math.Abs(candle.Delta) / candle.Volume;
            bool isHeavyDelta = deltaRatio >= _minDeltaRatio;

            decimal upperWick = candle.High - Math.Max(candle.Open, candle.Close);
            decimal lowerWick = Math.Min(candle.Open, candle.Close) - candle.Low;

            decimal tickSize = InstrumentInfo?.TickSize ?? 0.01m;

            // ── 做多信號：波段低點 + 爆量 + 主動賣單套牢(負Delta) + 長下影線 ──
            if (isSwingLow && isHighVolume && isHeavyDelta && candle.Delta < 0 && (lowerWick / range) >= _wickRatio)
            {
                _buySignals[bar] = candle.Low - (15 * tickSize);
            }

            // ── 做空信號：波段高點 + 爆量 + 主動買單套牢(正Delta) + 長上影線 ──
            if (isSwingHigh && isHighVolume && isHeavyDelta && candle.Delta > 0 && (upperWick / range) >= _wickRatio)
            {
                _sellSignals[bar] = candle.High + (15 * tickSize);
            }
        }
    }
}
